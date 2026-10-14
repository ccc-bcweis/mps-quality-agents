// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Mps.Agents.Grounding;
using Mps.Agents.Ranking;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Agents.Resolution;

/// <summary>
/// Strict JSON for the resolution agent. Citations are evidence ids.
/// </summary>
public static class ResolutionJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string SerializeRequest(ResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return JsonSerializer.Serialize(request, Options);
    }

    public static bool TryReadRequest(string text, out ResolutionRequest? request)
    {
        request = null;
        if (!TrySlice(text, out var json))
        {
            return false;
        }

        try
        {
            request = JsonSerializer.Deserialize<ResolutionRequest>(json, Options);
            return request is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string SerializeDecision(RankDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.Escalate)
        {
            return JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["outcome"] = "escalate",
                ["reason"] = decision.Reason ?? "escalate",
            });
        }

        var claim = decision.Claim!;
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["category"] = Wire(claim.Category!.Value),
            ["cause"] = claim.Cause,
            ["confidence"] = claim.Confidence,
            ["evidenceIds"] = claim.EvidenceIds.Select(id => id.ToString()).ToArray(),
            ["counterEvidence"] = claim.CounterEvidence,
            ["actions"] = claim.Actions,
        });
    }

    public static bool TryParseClaim(string? text, out ResolutionClaim? claim, out string? escalateReason, out string? error)
    {
        claim = null;
        escalateReason = null;
        error = null;
        if (!TrySlice(text, out var json))
        {
            error = "resolution reply is not JSON";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "resolution reply is not a JSON object";
                return false;
            }

            if (root.TryGetProperty("outcome", out var outcome) && outcome.GetString() == "escalate")
            {
                escalateReason = root.TryGetProperty("reason", out var reason) ? reason.GetString() : null;
                if (string.IsNullOrWhiteSpace(escalateReason))
                {
                    error = "escalation is missing a reason";
                    return false;
                }

                return true;
            }

            if (!root.TryGetProperty("category", out var categoryElement) || !TryCategory(categoryElement.GetString(), out var category))
            {
                error = "resolution category is missing";
                return false;
            }

            var cause = root.TryGetProperty("cause", out var causeElement) ? causeElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(cause))
            {
                error = "resolution cause is missing";
                return false;
            }

            if (!root.TryGetProperty("confidence", out var confidenceElement) || !confidenceElement.TryGetDouble(out var confidence))
            {
                error = "resolution confidence is missing";
                return false;
            }

            var evidenceIds = ReadIds(root, "evidenceIds", out var idError);
            if (idError is not null)
            {
                error = idError;
                return false;
            }

            claim = new ResolutionClaim(
                category,
                cause,
                confidence,
                evidenceIds,
                ReadStrings(root, "counterEvidence"),
                ReadStrings(root, "actions"),
                []);
            return true;
        }
        catch (JsonException)
        {
            error = "resolution reply is not JSON";
            return false;
        }
    }

    private static bool TrySlice(string? text, out string json)
    {
        json = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var start = text.IndexOf('{', StringComparison.Ordinal);
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return false;
        }

        json = text.Substring(start, (end - start) + 1);
        return true;
    }

    private static List<EvidenceId> ReadIds(JsonElement root, string name, out string? error)
    {
        error = null;
        var ids = new List<EvidenceId>();
        if (!root.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return ids;
        }

        foreach (var item in array.EnumerateArray())
        {
            var value = item.GetString();
            if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty)
            {
                error = "resolution evidence id is not a GUID";
                return [];
            }

            ids.Add(new EvidenceId(parsed));
        }

        return ids;
    }

    private static List<string> ReadStrings(JsonElement root, string name)
    {
        var values = new List<string>();
        if (!root.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return values;
        }

        foreach (var item in array.EnumerateArray())
        {
            var value = item.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value.Trim());
            }
        }

        return values;
    }

    private static bool TryCategory(string? value, out RootCauseCategory category)
    {
        switch (value)
        {
            case "engineeringChange":
                category = RootCauseCategory.EngineeringChange;
                return true;
            case "equipmentCalibrationOrAlignment":
                category = RootCauseCategory.EquipmentCalibrationOrAlignment;
                return true;
            case "supplyChainMaterial":
                category = RootCauseCategory.SupplyChainMaterial;
                return true;
            case "processDrift":
                category = RootCauseCategory.ProcessDrift;
                return true;
            default:
                category = default;
                return false;
        }
    }

    private static string Wire(RootCauseCategory category)
    {
        return category switch
        {
            RootCauseCategory.EngineeringChange => "engineeringChange",
            RootCauseCategory.EquipmentCalibrationOrAlignment => "equipmentCalibrationOrAlignment",
            RootCauseCategory.SupplyChainMaterial => "supplyChainMaterial",
            RootCauseCategory.ProcessDrift => "processDrift",
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };
    }
}
