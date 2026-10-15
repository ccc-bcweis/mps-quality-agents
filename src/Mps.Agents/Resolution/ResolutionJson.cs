// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Mps.Agents.Explain;
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
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
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
            ["whyItMatters"] = claim.Explanations.Select(item => new Dictionary<string, object?>
            {
                ["evidenceId"] = item.EvidenceId.ToString(),
                ["whyItMatters"] = item.WhyItMatters,
                ["signalId"] = item.SignalId,
            }).ToArray(),
            ["evidenceMatrix"] = claim.EvidenceMatrix.Select(item => new Dictionary<string, object?>
            {
                ["category"] = Wire(item.Category),
                ["evidenceId"] = item.EvidenceId.ToString(),
                ["stance"] = WireStance(item.Stance),
                ["whyItMatters"] = item.WhyItMatters,
            }).ToArray(),
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

            if (root.TryGetProperty("outcome", out var outcome) && Str(outcome) == "escalate")
            {
                escalateReason = root.TryGetProperty("reason", out var reason) ? Str(reason) : null;
                if (string.IsNullOrWhiteSpace(escalateReason))
                {
                    error = "escalation is missing a reason";
                    return false;
                }

                return true;
            }

            if (!root.TryGetProperty("category", out var categoryElement) || !TryCategory(Str(categoryElement), out var category))
            {
                error = "resolution category is missing";
                return false;
            }

            var cause = root.TryGetProperty("cause", out var causeElement) ? Str(causeElement) : null;
            if (string.IsNullOrWhiteSpace(cause))
            {
                error = "resolution cause is missing";
                return false;
            }

            if (TryCategory(cause.Trim(), out _))
            {
                error = "resolution cause repeats the class name; describe the record, tool, and mechanism";
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

            if (!TryReadExplanations(root, out var explanations, out error))
            {
                return false;
            }

            if (!TryReadMatrix(root, out var matrix, out error))
            {
                return false;
            }

            claim = new ResolutionClaim(
                category,
                cause,
                confidence,
                evidenceIds,
                ReadStrings(root, "counterEvidence"),
                ReadStrings(root, "actions"),
                [],
                explanations,
                matrix);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
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
            var value = Str(item);
            if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty)
            {
                error = "resolution evidence id is not a GUID";
                return [];
            }

            ids.Add(new EvidenceId(parsed));
        }

        return ids;
    }

    private static bool TryReadExplanations(JsonElement root, out List<CitedEvidence> explanations, out string? error)
    {
        explanations = [];
        error = null;
        if (!root.TryGetProperty("whyItMatters", out var array))
        {
            return true;
        }

        if (array.ValueKind != JsonValueKind.Array)
        {
            error = "whyItMatters is not an array";
            return false;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                error = "whyItMatters entry is not an object";
                return false;
            }

            if (!TryEvidenceId(item, out var evidenceId))
            {
                error = "whyItMatters evidence id is not a GUID";
                return false;
            }

            var text = item.TryGetProperty("whyItMatters", out var textElement) ? Str(textElement) : null;
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "whyItMatters text is missing";
                return false;
            }

            string? signalId = null;
            if (item.TryGetProperty("signalId", out var signalElement) && signalElement.ValueKind == JsonValueKind.String)
            {
                signalId = Str(signalElement);
            }

            explanations.Add(new CitedEvidence(evidenceId, text, signalId));
        }

        return true;
    }

    private static bool TryReadMatrix(JsonElement root, out List<EvidenceMatrixEntry> matrix, out string? error)
    {
        matrix = [];
        error = null;
        if (!root.TryGetProperty("evidenceMatrix", out var array))
        {
            return true;
        }

        if (array.ValueKind != JsonValueKind.Array)
        {
            error = "evidenceMatrix is not an array";
            return false;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                error = "evidenceMatrix entry is not an object";
                return false;
            }

            if (!item.TryGetProperty("category", out var categoryElement) || !TryCategory(Str(categoryElement), out var category))
            {
                error = "evidenceMatrix category is missing";
                return false;
            }

            if (!TryEvidenceId(item, out var evidenceId))
            {
                error = "evidenceMatrix evidence id is not a GUID";
                return false;
            }

            if (!item.TryGetProperty("stance", out var stanceElement) || !TryStance(Str(stanceElement), out var stance))
            {
                error = "evidenceMatrix stance is missing";
                return false;
            }

            var why = item.TryGetProperty("whyItMatters", out var whyElement) && whyElement.ValueKind == JsonValueKind.String
                ? Str(whyElement)
                : null;
            matrix.Add(new EvidenceMatrixEntry(category, evidenceId, stance, why));
        }

        return true;
    }

    private static bool TryEvidenceId(JsonElement item, out EvidenceId evidenceId)
    {
        evidenceId = default;
        if (!item.TryGetProperty("evidenceId", out var idElement))
        {
            return false;
        }

        var value = Str(idElement);
        if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty)
        {
            return false;
        }

        evidenceId = new EvidenceId(parsed);
        return true;
    }

    private static bool TryStance(string? value, out EvidenceStance stance)
    {
        switch (value)
        {
            case "supports":
                stance = EvidenceStance.Supports;
                return true;
            case "rulesOut":
                stance = EvidenceStance.RulesOut;
                return true;
            case "neutral":
                stance = EvidenceStance.Neutral;
                return true;
            default:
                stance = default;
                return false;
        }
    }

    private static string WireStance(EvidenceStance stance)
    {
        return stance switch
        {
            EvidenceStance.Supports => "supports",
            EvidenceStance.RulesOut => "rulesOut",
            EvidenceStance.Neutral => "neutral",
            _ => throw new ArgumentOutOfRangeException(nameof(stance)),
        };
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
            var value = Str(item);
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

    /// <summary>Models sometimes return an object or number where a string belongs. Take the text instead of throwing.</summary>
    private static string? Str(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            case JsonValueKind.Object:
                foreach (var name in new[] { "text", "summary", "description", "cause", "action", "value", "reason", "id" })
                {
                    if (element.TryGetProperty(name, out var inner) && inner.ValueKind == JsonValueKind.String)
                    {
                        return inner.GetString();
                    }
                }

                return element.GetRawText();
            default:
                return element.GetRawText();
        }
    }
}
