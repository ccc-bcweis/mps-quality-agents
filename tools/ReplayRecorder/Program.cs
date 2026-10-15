// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Mps.Api.Demo;
using Mps.Api.Hosting;
using Mps.Simulator.Scenarios;

var repo = FindRepo();
var database = Path.Combine(Path.GetTempPath(), "mps-replay-recorder-" + Guid.NewGuid().ToString("N") + ".db");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = repo,
});
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.WebHost.UseSetting("Otel:Enabled", "false");
builder.WebHost.UseSetting("Mps:Simulation:Enabled", "false");
builder.WebHost.UseSetting("ConnectionStrings:Mps", "Data Source=" + database);
builder.AddQualityApi();
var app = builder.Build();
app.UseQualityApi();
await app.StartAsync().ConfigureAwait(false);
try
{
    var requested = args.Where(arg => !arg.StartsWith('-')).ToArray();
    var ids = requested.Length == 0
        ? ScriptedScenarioCatalog.All.Select(scenario => scenario.Id).ToArray()
        : requested;
    foreach (var id in ids)
    {
        if (!ScriptedScenarioCatalog.TryGet(id, out var scenario))
        {
            Console.Error.WriteLine("Unknown scenario " + id);
            Environment.ExitCode = 1;
            continue;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var trigger = scope.ServiceProvider.GetRequiredService<ScenarioTrigger>();
        var recorder = app.Services.GetRequiredService<ScenarioReplayRecorder>();
        var count = await recorder.CaptureAsync(
            scenario.Id,
            async cancellationToken =>
            {
                var run = await trigger.RunAsync(scenario.Id, cancellationToken).ConfigureAwait(false);
                if (run is null)
                {
                    throw new InvalidOperationException("Scenario " + scenario.Id + " did not run.");
                }
            },
            CancellationToken.None).ConfigureAwait(false);
        Console.WriteLine(scenario.Id + " " + count);
    }
}
finally
{
    await app.StopAsync().ConfigureAwait(false);
    try
    {
        if (File.Exists(database))
        {
            File.Delete(database);
        }
    }
    catch (IOException)
    {
        // The temp database can be removed later. It is not a recording.
    }
}

static string FindRepo()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "data", "seed", "enterprise.json"))
            && File.Exists(Path.Combine(dir.FullName, "MPS.sln")))
        {
            return dir.FullName;
        }
    }

    throw new DirectoryNotFoundException("The repository root was not found from " + AppContext.BaseDirectory);
}
