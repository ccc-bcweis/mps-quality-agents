// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Mps.Api.Persistence;

namespace Mps.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath;
    private readonly string _replayDirectory;
    private readonly bool _deleteDatabase;

    public ApiFactory()
        : this(null, true)
    {
    }

    internal ApiFactory(string? databasePath, bool deleteDatabase = true)
    {
        _databasePath = string.IsNullOrWhiteSpace(databasePath)
            ? Path.Combine(Path.GetTempPath(), "mps-api-" + Guid.NewGuid().ToString("N") + ".db")
            : databasePath;
        _deleteDatabase = deleteDatabase;
        _replayDirectory = Path.Combine(Path.GetTempPath(), "mps-replay-" + Guid.NewGuid().ToString("N"));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Otel:Enabled", "false");
        builder.UseSetting("Mps:Simulation:Enabled", "false");
        builder.UseSetting("Mps:Simulation:RealTime", "false");
        builder.UseSetting("ConnectionStrings:Mps", "Data Source=" + _databasePath);
        builder.UseSetting("Mps:Replay:Directory", _replayDirectory);

        // The lifecycle tests were written against the fixed placeholder investigation, so they pin it. It is also explicit
        // so a developer's .env (for example an OPENAI_API_KEY) never sends tests to a live model. OfflineScenarioTests and
        // ModelModeTests override this to run the real agents offline.
        builder.UseSetting("Mps:Model:Mode", "placeholder");
        builder.ConfigureServices(services =>
        {
            var descriptors = services.Where(service =>
                    service.ServiceType == typeof(DbContextOptions<QualityDbContext>)
                    || service.ServiceType == typeof(QualityDbContext))
                .ToList();
            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<QualityDbContext>(options => options.UseSqlite("Data Source=" + _databasePath));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (_deleteDatabase)
        {
            TryDelete(_databasePath);
            TryDelete(_databasePath + "-wal");
            TryDelete(_databasePath + "-shm");
        }
        try
        {
            if (Directory.Exists(_replayDirectory))
            {
                Directory.Delete(_replayDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // The host may still be releasing a recording. The temp name will not be reused.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // The host may still be releasing the file. The temp name will not be reused.
        }
    }
}
