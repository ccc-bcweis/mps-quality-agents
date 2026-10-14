// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Demo fleet for the console. Shapes match contracts/openapi.yaml.
// S1 ids match contracts/msw/handlers.ts. Ticket numbers follow the wireframes.

import type { Alert, EvidenceItem, Incident, Investigation, Ticket, Tool } from "../api/types";

export const S1_INCIDENT_ID = "8d0a1c4e-1b7a-4e5a-9c1d-0a6b2e3f4a5b";
export const S1_INVESTIGATION_ID = "2c6e1a40-7b2d-4f1e-8a33-91d0c5b6e7f8";
export const S1_EVIDENCE_ID = "11111111-1111-4111-8111-111111111111";
export const S1_HYPOTHESIS_ID = "22222222-2222-4222-8222-222222222222";
export const S1_ALERT_ID = "33333333-3333-4333-8333-333333333333";
export const S1_TICKET_ID = "44444444-4444-4444-8444-444444444444";

export const S2_INCIDENT_ID = "a1111111-1111-4111-8111-111111111111";
export const S2_INVESTIGATION_ID = "a2222222-2222-4222-8222-222222222222";
export const S2_ALERT_ID = "a3333333-3333-4333-8333-333333333333";
export const S2_TICKET_ID = "a4444444-4444-4444-8444-444444444444";

export const NCR_INCIDENT_ID = "b1111111-1111-4111-8111-111111111111";
export const NCR_TICKET_ID = "b3333333-3333-4333-8333-333333333333";
export const SUPPLIER_INCIDENT_ID = "b2222222-2222-4222-8222-222222222222";
export const SUPPLIER_TICKET_ID = "b4444444-4444-4444-8444-444444444444";

const TEL_ID = "11111111-1111-4111-8111-111111111112";
const MES_ID = "11111111-1111-4111-8111-111111111113";
const PM_ID = "11111111-1111-4111-8111-111111111114";
const CAL_ID = "11111111-1111-4111-8111-111111111115";
const ERP_ID = "11111111-1111-4111-8111-111111111116";
const DOC_ID = "11111111-1111-4111-8111-111111111117";
const H2_ID = "22222222-2222-4222-8222-222222222223";
const H3_ID = "22222222-2222-4222-8222-222222222224";

function chambers(names: string[]): Tool["chambers"] {
  return names.map((name, index) => ({ id: `C${index + 1}`, name }));
}

export const tools: Tool[] = [
  { id: "D-01", name: "Deposition 01", family: "deposition", model: "MPS-D7", bay: "Bay A", chambers: chambers(["Chamber A", "Chamber B"]) },
  { id: "D-02", name: "Deposition 02", family: "deposition", model: "MPS-D7", bay: "Bay A", chambers: chambers(["Chamber A", "Chamber B"]) },
  { id: "E-01", name: "Etch 01", family: "etch", model: "MPS-E7", bay: "Bay A", chambers: chambers(["Chamber A", "Chamber B"]) },
  { id: "E-02", name: "Etch 02", family: "etch", model: "MPS-E7", bay: "Bay A", chambers: chambers(["Chamber A", "Chamber B"]) },
  { id: "S-01", name: "Strip 01", family: "strip", model: "MPS-S7", bay: "Bay B", chambers: chambers(["Chamber A"]) },
  { id: "S-02", name: "Strip 02", family: "strip", model: "MPS-S7", bay: "Bay B", chambers: chambers(["Chamber A"]) },
  { id: "C-01", name: "Clean 01", family: "clean", model: "MPS-C7", bay: "Bay B", chambers: chambers(["Chamber A"]) },
  { id: "C-02", name: "Clean 02", family: "clean", model: "MPS-C7", bay: "Bay B", chambers: chambers(["Chamber A"]) },
];

export const alerts: Alert[] = [
  {
    id: S2_ALERT_ID,
    toolId: "D-01",
    chamberId: "C1",
    parameter: "chamberPressure",
    severity: "high",
    rule: "westernElectric1",
    raisedAt: "2026-10-14T14:40:00Z",
    correlationId: "corr-s2",
  },
  {
    id: S1_ALERT_ID,
    toolId: "E-02",
    chamberId: "C2",
    parameter: "escTemperature",
    severity: "medium",
    rule: "westernElectric2",
    raisedAt: "2026-10-14T14:59:00Z",
    correlationId: "corr-s1",
  },
];

export const incidents: Incident[] = [
  {
    id: S1_INCIDENT_ID,
    toolId: "E-02",
    chamberId: "C2",
    parameter: "escTemperature",
    channel: "iotSpcAlert",
    severity: "medium",
    summary: "ESC temperature oscillation after a recipe revision.",
    correlationId: "corr-s1",
    openedAt: "2026-10-14T15:00:00Z",
    stage: "recommendationReady",
  },
  {
    id: S2_INCIDENT_ID,
    toolId: "D-01",
    chamberId: "C1",
    parameter: "chamberPressure",
    channel: "iotSpcAlert",
    severity: "high",
    summary: "Chamber pressure step. PLM and MES revisions disagree.",
    correlationId: "corr-s2",
    openedAt: "2026-10-14T14:41:00Z",
    stage: "escalated",
  },
  {
    id: NCR_INCIDENT_ID,
    toolId: "C-01",
    chamberId: "C1",
    parameter: "particleAdders",
    channel: "qmsNonconformance",
    severity: "low",
    summary: "NCR-772 residue count, closed, unrelated.",
    correlationId: "corr-ncr-772",
    openedAt: "2026-10-10T11:00:00Z",
    stage: "closed",
  },
  {
    id: SUPPLIER_INCIDENT_ID,
    toolId: "E-01",
    chamberId: null,
    parameter: "gasFlow",
    channel: "supplierQualityNotice",
    severity: "medium",
    summary: "Supplier notice: gas lot NF-3317 moisture out of spec.",
    correlationId: "corr-sup-3317",
    openedAt: "2026-10-13T09:12:00Z",
    stage: "ticketOpened",
  },
];

export const tickets: Ticket[] = [
  {
    id: S1_TICKET_ID,
    number: "TKT-1044",
    incidentId: S1_INCIDENT_ID,
    toolId: "E-02",
    title: "ESC temperature oscillation after a recipe revision.",
    openedAt: "2026-10-14T15:00:00Z",
  },
  {
    id: S2_TICKET_ID,
    number: "TKT-1042",
    incidentId: S2_INCIDENT_ID,
    toolId: "D-01",
    title: "Chamber pressure step. PLM and MES revisions disagree.",
    openedAt: "2026-10-14T14:41:00Z",
  },
  {
    id: NCR_TICKET_ID,
    number: "TKT-1033",
    incidentId: NCR_INCIDENT_ID,
    toolId: "C-01",
    title: "NCR-772 residue count, closed, unrelated.",
    openedAt: "2026-10-10T11:00:00Z",
  },
  {
    id: SUPPLIER_TICKET_ID,
    number: "TKT-1038",
    incidentId: SUPPLIER_INCIDENT_ID,
    toolId: "E-01",
    title: "Supplier notice: gas lot NF-3317 moisture out of spec.",
    openedAt: "2026-10-13T09:12:00Z",
  },
];

const s1Evidence: EvidenceItem[] = [
  {
    id: TEL_ID,
    source: "telemetry",
    title: "ESC temperature series",
    excerpt: "Western Electric rule 2 on chamber B only. E-01 on the same recipe stays in control.",
    observedAt: "2026-10-14T14:08:00Z",
    toolId: "E-02",
    externalRef: "TEL-E02-ESC",
  },
  {
    id: S1_EVIDENCE_ID,
    source: "plm",
    title: "ECO-4471 edge ring rev C",
    excerpt: "Edge ring revision C became effective before the oscillation.",
    observedAt: "2026-10-12T08:00:00Z",
    toolId: "E-02",
    externalRef: "ECO-4471",
  },
  {
    id: MES_ID,
    source: "mes",
    title: "Run header L-4419",
    excerpt: "Recipe ETCH-OX-14, edge ring revision C, chamber B.",
    observedAt: "2026-10-14T13:40:00Z",
    toolId: "E-02",
    externalRef: "L-4419",
  },
  {
    id: PM_ID,
    source: "cmms",
    title: "Preventive maintenance the same week",
    excerpt: "A preventive maintenance was performed the same week on E-02.",
    observedAt: "2026-10-11T16:00:00Z",
    toolId: "E-02",
    externalRef: "PM-3381",
  },
  {
    id: CAL_ID,
    source: "cmms",
    title: "ESC thermocouple calibration",
    excerpt: "Calibration is current and not overdue.",
    observedAt: "2026-09-02T10:00:00Z",
    toolId: "E-02",
    externalRef: "CAL-ESC-02",
  },
  {
    id: ERP_ID,
    source: "erp",
    title: "Shared gas lot",
    excerpt: "The same gas lot is on E-01 and E-02. E-01 is nominal.",
    observedAt: "2026-10-09T08:00:00Z",
    toolId: "E-02",
    externalRef: "NF3-220",
  },
  {
    id: DOC_ID,
    source: "document",
    title: "Service bulletin on ring gap",
    excerpt: "Revision C can couple ESC heat when the gap is under spec.",
    observedAt: "2026-08-01T00:00:00Z",
    toolId: "E-02",
    externalRef: "SB-441",
  },
];

export const investigations: Investigation[] = [
  {
    id: S1_INVESTIGATION_ID,
    incidentId: S1_INCIDENT_ID,
    stage: "recommendationReady",
    correlationId: "corr-s1",
    escalationReason: null,
    evidence: s1Evidence,
    hypotheses: [
      {
        id: S1_HYPOTHESIS_ID,
        category: "engineeringChange",
        cause: "Edge ring revision C changed the thermal path.",
        confidence: 0.74,
        evidenceIds: [S1_EVIDENCE_ID],
        counterEvidence: ["A preventive maintenance was performed the same week."],
        actions: ["Hold the affected lots and confirm the installed ring revision."],
      },
      {
        id: H2_ID,
        category: "equipmentCalibrationOrAlignment",
        cause: "Calibration offset would shift the mean. The series oscillates and the calibration is current.",
        confidence: 0.11,
        evidenceIds: [CAL_ID],
        counterEvidence: ["Calibration record CAL-ESC-02 is current."],
        actions: [],
      },
      {
        id: H3_ID,
        category: "supplyChainMaterial",
        cause: "A gas lot shared with a nominal tool does not explain a single-chamber signal.",
        confidence: 0.07,
        evidenceIds: [ERP_ID],
        counterEvidence: ["E-01 uses the same lot and is in control."],
        actions: [],
      },
    ],
  },
  {
    id: S2_INVESTIGATION_ID,
    incidentId: S2_INCIDENT_ID,
    stage: "escalated",
    correlationId: "corr-s2",
    escalationReason:
      "PLM says showerhead revision C. The MES run header says revision B. The calibration record is missing. No root cause is asserted.",
    evidence: [
      {
        id: "c1111111-1111-4111-8111-111111111111",
        source: "plm",
        title: "Showerhead revision C",
        excerpt: "PLM config lists showerhead revision C on D-01.",
        observedAt: "2026-10-08T12:00:00Z",
        toolId: "D-01",
        externalRef: "PLM-SH-C",
      },
      {
        id: "c2222222-2222-4222-8222-222222222222",
        source: "mes",
        title: "MES run header revision B",
        excerpt: "The run header for the pressure step records showerhead revision B.",
        observedAt: "2026-10-14T14:20:00Z",
        toolId: "D-01",
        externalRef: "MES-D01-B",
      },
      {
        id: "c3333333-3333-4333-8333-333333333333",
        source: "spc",
        title: "Chamber pressure step",
        excerpt: "One point beyond 3 sigma. The latest reading is 6.52 Torr, outside the 2–6 Torr window.",
        observedAt: "2026-10-14T14:40:00Z",
        toolId: "D-01",
        externalRef: "SPC-D01-P",
      },
    ],
    hypotheses: [],
  },
];

export function investigationFor(id: string): Investigation | undefined {
  return investigations.find((item) => item.id === id || item.incidentId === id);
}
