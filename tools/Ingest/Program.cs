// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Connectors.Docs;
using Mps.Simulator.Docs;
using Mps.Simulator.Fleet;

var fleetDirectory = Path.Combine("data", "fleet");
var templateDirectory = Path.Combine("data", "docs", "templates");
var corpusDirectory = Path.Combine("data", "docs", "corpus");
var indexPath = Path.Combine("data", "index", "embeddings.json");

try
{
    for (var index = 0; index < args.Length; index++)
    {
        switch (args[index])
        {
            case "--fleet":
                fleetDirectory = Take(args, ref index, "--fleet");
                break;
            case "--templates":
                templateDirectory = Take(args, ref index, "--templates");
                break;
            case "--corpus":
                corpusDirectory = Take(args, ref index, "--corpus");
                break;
            case "--index":
                indexPath = Take(args, ref index, "--index");
                break;
            default:
                throw new InvalidOperationException($"Unknown argument {args[index]}.");
        }
    }
}
catch (InvalidOperationException exception)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine("Usage: Ingest --fleet data/fleet --templates data/docs/templates --corpus data/docs/corpus --index data/index/embeddings.json");
    return 1;
}

var fleet = FleetCatalogLoader.Load(fleetDirectory);
DocumentCorpusGenerator.Write(fleet, templateDirectory, corpusDirectory);
DocumentIndex.BuildFromDirectory(corpusDirectory).Write(indexPath);
Console.WriteLine($"Wrote {corpusDirectory} and {indexPath}");
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
