// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Collections;
using System.Globalization;
using System.Text;

using Mps.Domain.Evidence;
using Mps.Domain.Identity;

using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Agents.Explain;

/// <summary>
/// Plain sentences for evidence. Loads data/why-it-matters templates and fleet sensor ids when they exist.
/// A missing or unreadable file falls back to built-in sentences.
/// </summary>
public sealed class WhyItMattersCatalog
{
    public const string GenericSentence = "This record is part of the evidence gathered for the incident.";

    private static readonly Lazy<WhyItMattersCatalog> SharedCatalog = new(Discover);

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private readonly IReadOnlyList<Template> _templates;
    private readonly IReadOnlyList<Sensor> _sensors;

    private WhyItMattersCatalog(IReadOnlyList<Template> templates, IReadOnlyList<Sensor> sensors)
    {
        _templates = templates;
        _sensors = sensors;
    }

    public static WhyItMattersCatalog Shared => SharedCatalog.Value;

    public static WhyItMattersCatalog Empty { get; } = new([], []);

    public static WhyItMattersCatalog Load(string? dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory) || !Directory.Exists(dataDirectory))
        {
            return Empty;
        }

        var templates = new List<Template>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in TemplateFiles(dataDirectory))
        {
            if (seen.Add(file))
            {
                TryReadTemplates(file, templates);
            }
        }

        var sensors = new List<Sensor>();
        var fleet = Path.Combine(dataDirectory, "fleet");
        if (Directory.Exists(fleet))
        {
            foreach (var file in Directory.GetFiles(fleet, "*.yaml").Order(StringComparer.Ordinal))
            {
                TryReadSensors(file, sensors);
            }
        }

        return new WhyItMattersCatalog(templates, sensors);
    }

    public CitedEvidence ExplainItem(EvidenceItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var cited = Explain(item.Id, Wire(item.Source), ParameterOf(item), item.Title, item.ExternalRef, preferredSignalId: null);
        return LimitSentence(item) is { } measured ? new CitedEvidence(cited.EvidenceId, measured, cited.SignalId) : cited;
    }

    public string? SignalIdFor(EvidenceItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Source is not (EvidenceSource.Telemetry or EvidenceSource.Spc))
        {
            return null;
        }

        return SignalFor(item.Source, ParameterOf(item), item.Title, item.ExternalRef, preferred: null);
    }

    public CitedEvidence Explain(
        EvidenceId id,
        string? sourceName,
        string? parameter,
        string? title,
        string? externalRef,
        string? preferredSignalId)
    {
        if (!TryParseSource(sourceName, out var source))
        {
            return new CitedEvidence(id, GenericSentence, null);
        }

        var sentence = SentenceFor(source, parameter, title, externalRef);
        if (!PlainSentence.IsValid(sentence))
        {
            sentence = GenericSentence;
        }

        var signal = source is EvidenceSource.Telemetry or EvidenceSource.Spc
            ? SignalFor(source, parameter, title, externalRef, preferredSignalId)
            : null;
        return new CitedEvidence(id, sentence, signal);
    }

    public static string? ParameterOf(EvidenceItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Source is not (EvidenceSource.Telemetry or EvidenceSource.Spc) || string.IsNullOrWhiteSpace(item.ExternalRef))
        {
            return null;
        }

        var parts = item.ExternalRef.Split(':');
        return parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2]) ? parts[2].Trim() : null;
    }

    public static string? Kebab(string? parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter))
        {
            return null;
        }

        var builder = new StringBuilder();
        var pendingHyphen = false;
        foreach (var character in parameter.Trim())
        {
            if (!char.IsLetterOrDigit(character))
            {
                pendingHyphen = builder.Length > 0;
                continue;
            }

            if (pendingHyphen)
            {
                builder.Append('-');
                pendingHyphen = false;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    private string SentenceFor(EvidenceSource source, string? parameter, string? title, string? externalRef)
    {
        var template = Match(source, parameter, title, externalRef);
        if (template is not null)
        {
            return template.Sentence;
        }

        if (source is EvidenceSource.Telemetry or EvidenceSource.Spc)
        {
            return SensorSentence(parameter);
        }

        return SourceSentence(source, title, externalRef);
    }

    private string SignalFor(EvidenceSource source, string? parameter, string? title, string? externalRef, string? preferred)
    {
        var cleaned = CleanSignal(preferred);
        if (cleaned is not null)
        {
            return cleaned;
        }

        var template = Match(source, parameter, title, externalRef);
        if (template?.SignalId is not null)
        {
            return template.SignalId;
        }

        var sensor = MatchSensor(parameter);
        return sensor ?? Kebab(parameter) ?? "sensor";
    }

    private Template? Match(EvidenceSource source, string? parameter, string? title, string? externalRef)
    {
        Template? best = null;
        var bestScore = 0;
        foreach (var template in _templates)
        {
            if (!template.Matches(source, parameter, title, externalRef, out var score))
            {
                continue;
            }

            if (score > bestScore)
            {
                best = template;
                bestScore = score;
            }
        }

        return best;
    }

    private string? MatchSensor(string? parameter)
    {
        var key = Normalize(parameter);
        if (key.Length == 0)
        {
            return null;
        }

        foreach (var sensor in _sensors)
        {
            var idKey = Normalize(sensor.Id);
            var parameterKey = Normalize(sensor.Parameter);
            if (parameterKey.Length > 0 && parameterKey == key)
            {
                return sensor.Id;
            }

            if (idKey == key)
            {
                return sensor.Id;
            }

            if (key.Length >= 4 && parameterKey.Length >= 4 && (parameterKey.Contains(key, StringComparison.Ordinal) || key.Contains(parameterKey, StringComparison.Ordinal)))
            {
                return sensor.Id;
            }

            if (key.Length >= 4 && idKey.Length >= 4 && (idKey.Contains(key, StringComparison.Ordinal) || key.Contains(idKey, StringComparison.Ordinal)))
            {
                return sensor.Id;
            }
        }

        return null;
    }

    /// <summary>
    /// The sentence for a sensor reading, from its own numbers: out of spec when the value is outside the recipe spec,
    /// out of control when it is inside spec but a control rule broke. Null when the pack stored no limits.
    /// </summary>
    public static string? LimitSentence(EvidenceItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Source is not (EvidenceSource.Telemetry or EvidenceSource.Spc) || item.Reading is not { } reading)
        {
            return null;
        }

        var (controlLow, controlHigh) = reading.ControlLimits(Mps.Application.Spc.SpcRuleSettings.Shared.ControlLimitSigma);
        var state = LimitState.Of(reading.Value, reading.SpecLow, reading.SpecHigh, controlLow, controlHigh, reading.Center is not null);
        var latest = item.Source == EvidenceSource.Telemetry ? "The latest reading" : "This reading";
        return state switch
        {
            LimitState.OutOfSpec => latest + " is outside its recipe spec, so it is out of spec and can change the result on the wafer.",
            LimitState.OutOfControl => latest + " is still inside its recipe spec but broke a control rule, so it is out of control: statistically unusual, not a spec violation.",
            LimitState.InControl => latest + " is inside its recipe spec and its control limits.",
            _ => null,
        };
    }

    /// <summary>Used only when the pack stored no limits, so it names no limit and claims no direction.</summary>
    private static string SensorSentence(string? parameter)
    {
        _ = parameter;
        return "This sensor's readings are part of the signal under investigation; open them to compare against the recipe spec.";
    }

    /// <summary>Why a record a person added in the evidence loop matters.</summary>
    public const string UserEvidenceSentence = "A person on the line added this; the agents weigh it with the plant records.";

    private static string SourceSentence(EvidenceSource source, string? title, string? externalRef)
    {
        var haystack = (title ?? string.Empty) + " " + (externalRef ?? string.Empty);
        var calibration = haystack.Contains("calibrat", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains("align", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains("overdue", StringComparison.OrdinalIgnoreCase);
        var notice = haystack.Contains("notice", StringComparison.OrdinalIgnoreCase);
        return source switch
        {
            EvidenceSource.Plm => "An engineering change landed just before this signal moved, which can change the process result.",
            EvidenceSource.Cmms when calibration => "A calibration or alignment record is overdue, which can make a sensor read the wrong value.",
            EvidenceSource.Cmms => "Maintenance in this window can move a sensor or change an alignment.",
            EvidenceSource.Erp when notice => "A supplier notice in this window says a material lot may be outside its specification.",
            EvidenceSource.Erp => "A new material lot arrived in this window, which can change the process on every tool that used it.",
            EvidenceSource.Mes => "The run record shows which recipe and material were in the chamber when the signal moved.",
            EvidenceSource.Qms => "A quality record in this window describes a defect that may match this signal.",
            EvidenceSource.Document => "A procedure in the document set describes how this signal should behave.",
            EvidenceSource.Ticketing => "The ticket records the same incident the sensors and records are being checked against.",
            EvidenceSource.ManualMeasurement => Mps.Application.Printouts.ManualMeasurement.WhyItMatters,
            EvidenceSource.UserProvided => UserEvidenceSentence,
            _ => GenericSentence,
        };
    }

    private static WhyItMattersCatalog Discover()
    {
        var data = FindDataDirectory();
        return data is null ? Empty : Load(data);
    }

    internal static string? FindDataDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("Mps__DataDirectory");
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(Path.Combine(configured, "metadata")))
        {
            return configured;
        }

        var seeds = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var seed in seeds)
        {
            if (string.IsNullOrWhiteSpace(seed))
            {
                continue;
            }

            var dir = new DirectoryInfo(seed);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "data");
                if (Directory.Exists(Path.Combine(candidate, "metadata")))
                {
                    return candidate;
                }

                dir = dir.Parent;
            }
        }

        return null;
    }

    private static IEnumerable<string> TemplateFiles(string dataDirectory)
    {
        string[] names =
        [
            "why-it-matters.yaml",
            "why-it-matters.yml",
            Path.Combine("why-it-matters", "templates.yaml"),
            Path.Combine("explanations", "why-it-matters.yaml"),
        ];
        foreach (var name in names)
        {
            var path = Path.Combine(dataDirectory, name);
            if (File.Exists(path))
            {
                yield return path;
            }
        }

        var folder = Path.Combine(dataDirectory, "why-it-matters");
        if (!Directory.Exists(folder))
        {
            yield break;
        }

        foreach (var file in Directory.GetFiles(folder, "*.yaml").Order(StringComparer.Ordinal))
        {
            yield return file;
        }
    }

    private static void TryReadTemplates(string path, List<Template> sink)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        List<TemplateDto>? rows;
        try
        {
            if (text.TrimStart().StartsWith('-'))
            {
                rows = Deserializer.Deserialize<List<TemplateDto>>(text);
            }
            else
            {
                var file = Deserializer.Deserialize<TemplateFile>(text);
                rows = file?.Templates ?? file?.Items;
            }
        }
        catch (Exception exception) when (exception is YamlException or InvalidOperationException)
        {
            return;
        }

        if (rows is null)
        {
            return;
        }

        foreach (var row in rows)
        {
            if (row is null)
            {
                continue;
            }

            var sentence = FirstText(row.WhyItMatters, row.Text, row.Sentence);
            if (!PlainSentence.IsValid(sentence))
            {
                continue;
            }

            var signal = CleanSignal(FirstText(row.SignalId, row.SensorId));
            var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddSource(sources, row.Source);
            if (row.Sources is not null)
            {
                foreach (var source in row.Sources)
                {
                    AddSource(sources, source);
                }
            }

            var parameters = new HashSet<string>(StringComparer.Ordinal);
            AddParameter(parameters, row.Parameter);
            if (row.Parameters is not null)
            {
                foreach (var parameter in row.Parameters)
                {
                    AddParameter(parameters, parameter);
                }
            }

            sink.Add(new Template(sentence!, signal, sources, parameters, BlankToNull(row.TitleContains), BlankToNull(row.ExternalRefContains)));
        }
    }

    private static void TryReadSensors(string path, List<Sensor> sink)
    {
        try
        {
            var root = Deserializer.Deserialize<object>(File.ReadAllText(path));
            CollectSensors(root, sink, 0);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or YamlException or InvalidOperationException)
        {
            return;
        }
    }

    private static void CollectSensors(object? node, List<Sensor> sink, int depth)
    {
        if (node is null || depth > 12)
        {
            return;
        }

        if (node is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
                if (key is not null && key.Equals("sensors", StringComparison.OrdinalIgnoreCase))
                {
                    ReadSensorList(entry.Value, sink);
                }
                else
                {
                    CollectSensors(entry.Value, sink, depth + 1);
                }
            }

            return;
        }

        if (node is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                CollectSensors(item, sink, depth + 1);
            }
        }
    }

    private static void ReadSensorList(object? node, List<Sensor> sink)
    {
        if (node is not IEnumerable enumerable || node is string)
        {
            return;
        }

        foreach (var item in enumerable)
        {
            if (item is not IDictionary map)
            {
                continue;
            }

            var id = StringOf(map, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            sink.Add(new Sensor(id.Trim(), StringOf(map, "parameter") ?? StringOf(map, "type")));
        }
    }

    private static string? StringOf(IDictionary map, string name)
    {
        foreach (DictionaryEntry entry in map)
        {
            var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
            if (key is not null && key.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                var text = Convert.ToString(entry.Value, CultureInfo.InvariantCulture);
                return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            }
        }

        return null;
    }

    private static void AddSource(HashSet<string> sink, string? source)
    {
        if (!string.IsNullOrWhiteSpace(source))
        {
            sink.Add(source.Trim());
        }
    }

    private static void AddParameter(HashSet<string> sink, string? parameter)
    {
        var key = Normalize(parameter);
        if (key.Length > 0)
        {
            sink.Add(key);
        }
    }

    private static string? FirstText(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string? BlankToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? CleanSignal(string? signalId)
    {
        if (string.IsNullOrWhiteSpace(signalId))
        {
            return null;
        }

        var trimmed = signalId.Trim();
        if (trimmed.Length is < 1 or > 80 || trimmed.Any(char.IsWhiteSpace))
        {
            return null;
        }

        return trimmed;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static string Wire(EvidenceSource source)
    {
        return source switch
        {
            EvidenceSource.Telemetry => "telemetry",
            EvidenceSource.Spc => "spc",
            EvidenceSource.Mes => "mes",
            EvidenceSource.Qms => "qms",
            EvidenceSource.Plm => "plm",
            EvidenceSource.Erp => "erp",
            EvidenceSource.Cmms => "cmms",
            EvidenceSource.Document => "document",
            EvidenceSource.Ticketing => "ticketing",
            EvidenceSource.ManualMeasurement => "manualMeasurement",
            EvidenceSource.UserProvided => "userProvided",
            _ => "document",
        };
    }

    private static bool TryParseSource(string? name, out EvidenceSource source)
    {
        switch (name?.Trim().ToLowerInvariant())
        {
            case "telemetry":
                source = EvidenceSource.Telemetry;
                return true;
            case "spc":
                source = EvidenceSource.Spc;
                return true;
            case "mes":
                source = EvidenceSource.Mes;
                return true;
            case "qms":
                source = EvidenceSource.Qms;
                return true;
            case "plm":
                source = EvidenceSource.Plm;
                return true;
            case "erp":
                source = EvidenceSource.Erp;
                return true;
            case "cmms":
                source = EvidenceSource.Cmms;
                return true;
            case "document":
                source = EvidenceSource.Document;
                return true;
            case "ticketing":
                source = EvidenceSource.Ticketing;
                return true;
            case "manualmeasurement":
                source = EvidenceSource.ManualMeasurement;
                return true;
            case "userprovided":
                source = EvidenceSource.UserProvided;
                return true;
            default:
                source = default;
                return false;
        }
    }

    private sealed class Template
    {
        public Template(
            string sentence,
            string? signalId,
            HashSet<string> sources,
            HashSet<string> parameters,
            string? titleContains,
            string? externalRefContains)
        {
            Sentence = sentence;
            SignalId = signalId;
            Sources = sources;
            Parameters = parameters;
            TitleContains = titleContains;
            ExternalRefContains = externalRefContains;
        }

        public string Sentence { get; }

        public string? SignalId { get; }

        public HashSet<string> Sources { get; }

        public HashSet<string> Parameters { get; }

        public string? TitleContains { get; }

        public string? ExternalRefContains { get; }

        public bool Matches(EvidenceSource source, string? parameter, string? title, string? externalRef, out int score)
        {
            score = 0;
            if (Sources.Count > 0)
            {
                if (!Sources.Contains(Wire(source)))
                {
                    return false;
                }

                score++;
            }

            if (Parameters.Count > 0)
            {
                if (!Parameters.Contains(Normalize(parameter)))
                {
                    return false;
                }

                score++;
            }

            if (TitleContains is not null)
            {
                if (title is null || title.Contains(TitleContains, StringComparison.OrdinalIgnoreCase) == false)
                {
                    return false;
                }

                score++;
            }

            if (ExternalRefContains is not null)
            {
                if (externalRef is null || externalRef.Contains(ExternalRefContains, StringComparison.OrdinalIgnoreCase) == false)
                {
                    return false;
                }

                score++;
            }

            return score > 0;
        }
    }

    private sealed class TemplateFile
    {
        public List<TemplateDto>? Templates { get; set; }

        public List<TemplateDto>? Items { get; set; }
    }

    private sealed class TemplateDto
    {
        public string? WhyItMatters { get; set; }

        public string? Text { get; set; }

        public string? Sentence { get; set; }

        public string? SignalId { get; set; }

        public string? SensorId { get; set; }

        public string? Source { get; set; }

        public List<string>? Sources { get; set; }

        public string? Parameter { get; set; }

        public List<string>? Parameters { get; set; }

        public string? TitleContains { get; set; }

        public string? ExternalRefContains { get; set; }
    }

    private sealed class Sensor
    {
        public Sensor(string id, string? parameter)
        {
            Id = id;
            Parameter = parameter;
        }

        public string Id { get; }

        public string? Parameter { get; }
    }
}
