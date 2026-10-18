// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Architecture.Tests;

/// <summary>
/// Locks the Z1 deploy shape. Bicep is compiled in CI. These checks fail the build when a required token disappears.
/// </summary>
public sealed class DeployShapeTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void AppImageBuildsTheConsoleInLiveModeAndRunsAsNonRoot()
    {
        var docker = Read("deploy/app.Dockerfile");

        docker.Should().Contain("node:22-bookworm-slim");
        docker.Should().Contain("VITE_USE_MSW=false");
        docker.Should().Contain("dotnet publish src/Mps.Api/Mps.Api.csproj");
        docker.Should().Contain("mcr.microsoft.com/dotnet/aspnet:10.0");
        docker.Should().Contain("./wwwroot");
        docker.Should().Contain("USER ${APP_UID:-1654}");
        docker.Should().NotContain("groundtruth");
        docker.Should().NotContain("AZURE_OPENAI_API_KEY");
    }

    [Fact]
    public void LocalComposeRunsTheHostedImage()
    {
        var compose = Read("docker-compose.yml");

        compose.Should().Contain("dockerfile: deploy/app.Dockerfile", "a clone-and-run reviewer gets the same console and API the hosted demo serves");
        compose.Should().Contain("\"127.0.0.1:8080:8080\"", "local sign-in mode has no login, so the port stays on this machine");
        compose.Should().Contain("Mps__Model__Mode: ${MODEL_MODE:-}");
        compose.Should().NotContain("web.Dockerfile");
        File.Exists(Path.Combine(Root, "deploy", "web.Dockerfile")).Should().BeFalse("the placeholder console image was retired");
    }

    [Fact]
    public void ApiImageDoesNotCopyGroundTruth()
    {
        Read("deploy/api.Dockerfile").Should().NotContain("groundtruth");
        Read(".dockerignore").Should().Contain("data/groundtruth");
    }

    [Fact]
    public void BicepDeploysOneAppBehindEntraWithOptionalSql()
    {
        var main = Read("infra/main.bicep");
        var apps = Read("infra/modules/container-apps.bicep");
        var acr = Read("infra/modules/acr.bicep");
        var openai = Read("infra/modules/openai.bicep");
        var parameters = Read("infra/main.bicepparam");

        main.Should().Contain("param deploySql bool = false");
        main.Should().Contain("param deployStaticWeb bool = false");
        main.Should().Contain("output appFqdn string");
        main.Should().Contain("output acrLoginServer string");
        main.Should().Contain("output openAiEndpoint string");
        main.Should().Contain("modules/acr.bicep");
        parameters.Should().Contain("param deploySql = false");
        parameters.Should().Contain("param deployStaticWeb = false");

        acr.Should().Contain("name: 'Basic'");
        acr.Should().Contain("adminUserEnabled: false");
        acr.Should().Contain("7f951dda-4ed3-4680-a7ca-43fe172d538d");

        apps.Should().Contain("minReplicas: 1");
        apps.Should().Contain("maxReplicas: 1");
        apps.Should().Contain("cpu: json(cpu)");
        apps.Should().Contain("memory: memory");
        apps.Should().NotContain("json('0.25')");
        apps.Should().NotContain("'0.5Gi'");
        main.Should().Contain("param cpu string = '0.5'");
        main.Should().Contain("param memory string = '1Gi'");
        main.Should().Contain("cpu: cpu");
        main.Should().Contain("memory: memory");
        parameters.Should().Contain("param cpu = '0.5'");
        parameters.Should().Contain("param memory = '1Gi'");
        apps.Should().Contain("targetPort: 8080");
        apps.Should().Contain("external: true");
        apps.Should().Contain("Microsoft.App/containerApps/authConfigs@2024-03-01");
        apps.Should().Contain("unauthenticatedClientAction: 'AllowAnonymous'", "the app gates the API and sends signed-out browsers to the landing page");
        apps.Should().Contain("redirectToProvider: 'azureactivedirectory'");
        apps.Should().Contain("entraClientId");
        apps.Should().Contain("openIdIssuer");
        apps.Should().Contain("name: 'Mps__Model__Mode'");
        apps.Should().Contain("value: 'azure'");
        apps.Should().Contain("name: 'Mps__Model__DailyCapUsd'");
        apps.Should().Contain("value: '2'");
        apps.Should().NotContain("AZURE_OPENAI_API_KEY");
        apps.Should().Contain("secretRef: 'appinsights'");

        openai.Should().Contain("customSubDomainName: name");
        openai.Should().Contain("name: 'gpt-5.4-mini'");
        openai.Should().Contain("name: 'GlobalStandard'");
        openai.Should().Contain("param capacity int = 20");
        openai.Should().Contain("disableLocalAuth: true");
        openai.Should().Contain("5e0bd9bd-7b93-4f28-af87-19fc36ad61bd");
        openai.Should().NotContain("apiKey");
    }

    [Fact]
    public void DeployWorkflowIsManualOidcAndSmokesForAuth()
    {
        var workflow = Read(".github/workflows/deploy.yml");

        workflow.Should().Contain("workflow_dispatch:");
        workflow.Should().Contain("imageTag:");
        workflow.Should().Contain("environment: azure-demo");
        workflow.Should().Contain("id-token: write");
        workflow.Should().Contain("azure/login@a641126d1b8aa4d1fa005f4f92df94a3a4c4c906");
        workflow.Should().Contain("az deployment group create");
        workflow.Should().Contain("az acr build");
        workflow.Should().Contain("deploy/app.Dockerfile");
        workflow.Should().Contain("mps-app:");
        workflow.Should().Contain("vars.RESOURCE_GROUP");
        workflow.Should().Contain("vars.ENTRA_CLIENT_ID");
        workflow.Should().Contain("vars.ENTRA_ISSUER");
        workflow.Should().Contain("vars.BUDGET_EMAIL");
        workflow.Should().Contain("secrets.AZURE_CLIENT_ID");
        workflow.Should().Contain("\"401\"");
        workflow.Should().Contain("302 $base/landing.html", "a signed-out browser is sent to the landing page");
        workflow.Should().NotContain("pull_request:");
        workflow.Should().NotContain("push:");
        workflow.Should().Contain("github.sha");
        workflow.Should().NotContain("cpu=");
        workflow.Should().NotContain("memory=");
    }

    [Fact]
    public void CustomDomainStaysOnlyWhenBothTheNameAndCertificateArePassed()
    {
        var main = Read("infra/main.bicep");
        var apps = Read("infra/modules/container-apps.bicep");
        var parameters = Read("infra/main.bicepparam");
        var workflow = Read(".github/workflows/deploy.yml");
        var script = Read("deploy/deploy.sh");
        var readme = Read("README.md");
        var entra = Read("infra/ENTRA.md");

        main.Should().Contain("param customDomainName string = ''");
        main.Should().Contain("param customDomainCertificateId string = ''");
        main.Should().Contain("customDomainName: customDomainName");
        main.Should().Contain("customDomainCertificateId: customDomainCertificateId");
        parameters.Should().Contain("param customDomainName = ''");
        parameters.Should().Contain("param customDomainCertificateId = ''");

        apps.Should().Contain("bindingType: 'SniEnabled'");
        apps.Should().Contain("customDomains: customDomains");
        apps.Should().Contain("Set both customDomainName and customDomainCertificateId, or leave both empty.");

        workflow.Should().Contain("CUSTOM_DOMAIN: ${{ vars.CUSTOM_DOMAIN }}");
        workflow.Should().Contain("CUSTOM_DOMAIN_CERT_ID: ${{ vars.CUSTOM_DOMAIN_CERT_ID }}");
        Occurrences(workflow, "customDomainName=\"$CUSTOM_DOMAIN\"").Should().Be(2, "the bootstrap deploy and the app deploy both keep the binding");
        Occurrences(workflow, "customDomainCertificateId=\"$CUSTOM_DOMAIN_CERT_ID\"").Should().Be(2);

        script.Should().Contain("customDomainName=\"$CUSTOM_DOMAIN\"");
        script.Should().Contain("customDomainCertificateId=\"$CUSTOM_DOMAIN_CERT_ID\"");

        readme.Should().Contain("The TXT record proves you own the domain.");
        readme.Should().Contain("properties.customDomainConfiguration.customDomainVerificationId");
        readme.Should().Contain("az containerapp hostname add");
        readme.Should().Contain("az containerapp hostname bind");
        readme.Should().Contain("--validation-method CNAME");
        readme.Should().Contain("https://<domain>/.auth/login/aad/callback");
        readme.Should().Contain("--managed-certificates-only");
        entra.Should().Contain("The TXT record proves you own the domain.");
        entra.Should().Contain("--managed-certificates-only");
    }

    private static int Occurrences(string text, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MPS.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("MPS.sln was not found above the test output directory.");
    }
}
