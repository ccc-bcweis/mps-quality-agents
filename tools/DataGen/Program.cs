// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

using Mps.Simulator.Faults;
using Mps.Simulator.Fleet;
using Mps.Simulator.Roi;
using Mps.Simulator.Seed;
using Mps.Simulator.Sites;

var seed = EnterpriseSeedGenerator.DemoSeed;
var fleetDirectory = Path.Combine("data", "fleet");
var faultDirectory = Path.Combine("data", "faults");
var output = Path.Combine("data", "seed", "enterprise.json");
string? roiDirectory = null;

try
{
    for (var index = 0; index < args.Length; index++)
    {
        switch (args[index])
        {
            case "--seed":
                seed = int.Parse(Take(args, ref index, "--seed"), CultureInfo.InvariantCulture);
                break;
            case "--fleet":
                fleetDirectory = Take(args, ref index, "--fleet");
                break;
            case "--faults":
                faultDirectory = Take(args, ref index, "--faults");
                break;
            case "--out":
                output = Take(args, ref index, "--out");
                break;
            case "--roi":
                roiDirectory = Take(args, ref index, "--roi");
                break;
            default:
                throw new InvalidOperationException($"Unknown argument {args[index]}.");
        }
    }
}
catch (Exception exception) when (exception is InvalidOperationException or FormatException)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine("Usage: DataGen --seed 20261014 --fleet data/fleet --faults data/faults --out data/seed/enterprise.json [--roi data/roi]");
    return 1;
}

var fleet = FleetCatalogLoader.Load(fleetDirectory);
var knownTools = File.Exists(Path.Combine(fleetDirectory, "sites.yaml"))
    ? SiteLayoutLoader.Load(Path.Combine(fleetDirectory, "sites.yaml")).Families
    : null;
var catalog = FaultCatalogLoader.Load(faultDirectory, fleet, knownTools);
SeedJson.Write(EnterpriseSeedGenerator.Generate(fleet, catalog, seed), output);
Console.WriteLine($"Wrote {output}");
if (roiDirectory is not null)
{
    RoiCsv.Write(RoiCalculator.Calculate(new RoiAssumptions { Seed = seed }), roiDirectory);
    Console.WriteLine($"Wrote {roiDirectory}");
}

return 0;

static string Take(string[] args, ref int index, string name)
{
    if (index + 1 >= args.Length)
    {
        throw new InvalidOperationException($"Missing value for {name}.");
    }

    index++;
    return args[index];
}
