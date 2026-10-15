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
        docker.Should().Contain("mcr.microsoft.com/dotnet/aspnet:8.0");
        docker.Should().Contain("./wwwroot");
        docker.Should().Contain("USER ${APP_UID:-1654}");
        docker.Should().NotContain("groundtruth");
        docker.Should().NotContain("AZURE_OPENAI_API_KEY");
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

        apps.Should().Contain("minReplicas: 0");
        apps.Should().Contain("maxReplicas: 3");
        apps.Should().Contain("targetPort: 8080");
        apps.Should().Contain("external: true");
        apps.Should().Contain("Microsoft.App/containerApps/authConfigs@2024-03-01");
        apps.Should().Contain("unauthenticatedClientAction: 'RedirectToLoginPage'");
        apps.Should().Contain("entraClientId");
        apps.Should().Contain("openIdIssuer");
        apps.Should().Contain("name: 'Mps__Model__Mode'");
        apps.Should().Contain("value: 'Live'");
        apps.Should().Contain("name: 'Mps__Model__DailyCapUsd'");
        apps.Should().Contain("value: '2'");
        apps.Should().NotContain("AZURE_OPENAI_API_KEY");
        apps.Should().Contain("secretRef: 'appinsights'");

        openai.Should().Contain("customSubDomainName: name");
        openai.Should().Contain("name: 'gpt-4o-mini'");
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
        workflow.Should().Contain("\"302\"");
        workflow.Should().NotContain("pull_request:");
        workflow.Should().NotContain("push:");
        workflow.Should().Contain("github.sha");
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
