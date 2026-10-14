# Design questions from GB-2 round 2

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

Lane `lane/2b-why`, commit `97cabc2`. No contract file was edited. The agent stores `whyItMatters`, `signalId`, and the evidence matrix on `InvestigationSession`. The questions below are for Bob, GB-1, and GB-3.

## 1. Domain and API do not carry the v1.1 explanation fields

**Context.** `contracts/openapi.yaml` v1.1 has `EvidenceItem.whyItMatters` (max 240), `EvidenceItem.signalId`, `EvidenceMatrixEntry` (`category`, `evidenceId`, `stance` of `supports` / `rulesOut` / `neutral`, optional `whyItMatters`), and `Investigation.evidenceMatrix`. `Mps.Domain.Evidence.EvidenceItem` and `InvestigationSnapshot` do not have those members. `Mps.Api` `ContractMapper` does not write them. `IInvestigationWorkflow` still returns `InvestigationSnapshot`.

This lane kept the values on `InvestigationSession.Explanations` and `InvestigationSession.EvidenceMatrix`. A supervisor reading the HTTP investigation resource does not see them.

**Options.**

1. Add optional properties on `EvidenceItem` and `InvestigationSnapshot`, and map them in the API. Old callers that ignore unknown JSON fields stay valid.
2. Leave the fields on the session and add a separate read model for the console.
3. Remove the fields from the OpenAPI. That would reopen contracts v1.1.

**Recommendation.** Option 1. GB-3 maps `session.Explanations` onto each evidence item and `session.EvidenceMatrix` onto the investigation. `signalId` is set only for telemetry and SPC. This lane does not own those projects.

**Blocking?** Blocking for the console display of `whyItMatters` and the matrix. Not blocking the agent work in this lane.

## 2. whyItMatters template files

**Context.** `WhyItMattersCatalog.Load` looks for a `data/` directory by walking up from the process current directory and from `AppContext.BaseDirectory`. It reads these files when they exist:

- `data/why-it-matters.yaml`
- `data/why-it-matters.yml`
- `data/why-it-matters/templates.yaml` and every `*.yaml` in `data/why-it-matters/`
- `data/explanations/why-it-matters.yaml`

The document is a top-level list, or an object with `templates` or `items`. Each row may set `whyItMatters`, `text`, or `sentence`; `signalId` or `sensorId`; `source` or `sources`; `parameter` or `parameters`; `titleContains`; `externalRefContains`. Every constraint that is set must match. The row with the highest number of constraints wins. A row with no constraints is ignored. A sentence that is blank, longer than 240 characters, not a single sentence, or that contains an unexplained acronym is skipped. A missing file, an unreadable file, or YAML that does not parse is ignored, and the built-in sentence is used.

Fleet sensor ids come from `data/fleet/*.yaml`. The loader walks the document for a key named `sensors`. Each entry needs `id` and `parameter` or `type`.

**Options.**

1. GB-1 writes `data/why-it-matters.yaml` in the shape above, and adds a `sensors` list to the fleet files.
2. GB-1 uses a different path or field name. This lane would then need a follow-up to read it. The built-in sentences keep the demo working until then.

**Recommendation.** Option 1. One sentence per row, with `source` and `parameter` set so a broader row does not hide a specific one.

**Blocking?** No. A missing or invalid file falls back to the built-in sentences.

## 3. signalId before fleet sensors exist

**Context.** For telemetry and SPC, `signalId` is chosen in this order: a valid `signalId` on the matching template, a fleet sensor whose `parameter` or `id` equals the evidence parameter, a fleet sensor whose key contains the parameter when both keys are at least 4 characters, the parameter in kebab case, then the literal `sensor`. A record that is not a sensor has a null `signalId`. The fleet YAML in this worktree has operating windows and no `sensors` list, so `esc_temp` becomes `esc-temp`.

**Options.**

1. Keep the kebab-case fallback until GB-1 adds `sensors`.
2. Leave `signalId` null when no fleet sensor matches, and let grounding repair fail closed.

**Recommendation.** Option 1. The validator requires a `signalId` on every cited sensor. A stable fallback keeps S1 grounded before the fleet list lands. GB-1 sensor ids will replace the kebab value when the files appear, because a fleet match is preferred over kebab case.

**Blocking?** No.

## 4. Acronyms in the cause line

**Context.** `PlainSentence` checks `whyItMatters` only. It rejects the denylist (`spc`, `capa`, `pm`, `oee`, `mtbf`, `mttr`, `ec`, `ecn`, `eco`, `ucl`, `lcl`, `cpk`, `fdc`, `ncr`, `copq`, `rf`, `sccm`, `mtorr`, `mes`, `qms`, `plm`, `erp`, `cmms`, `esc`, `mfc`, `nf3`, `pecvd`, `pvd`, `diw`, `sc1`, `sc2`, `sih4`, `n2o`, `cf4`, `chf3`) and any token of two or more letters that is all capitals. ADR-003 asks for language a supervisor can read. The ranked cause for S1 still says ECO-4471, and the counter-evidence still names WO-19. Those strings are identifiers. The plain sentence sits on the evidence row.

**Options.**

1. Keep identifiers in `cause` and `counterEvidence`. Require plain language only on `whyItMatters`.
2. Run `cause` and `counterEvidence` through `PlainSentence` as well, and leave the identifier on the cited evidence id.

**Recommendation.** Option 1 for the pitch. The ranker tests and the spoken cause name ECO-4471. Option 2 is a product change if Bob wants the headline itself to pass the same rule.

**Blocking?** No.
