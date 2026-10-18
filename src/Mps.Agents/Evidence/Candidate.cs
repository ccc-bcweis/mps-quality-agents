// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Agents.Evidence;

/// <summary>
/// A pack record that can support a root-cause class.
/// Kind is eco, workOrder, calibration, materialLot, supplierNotice, alignment, or userEvidence.
/// </summary>
public sealed record Candidate(EvidenceId Id, RootCauseCategory Class, string Kind);
