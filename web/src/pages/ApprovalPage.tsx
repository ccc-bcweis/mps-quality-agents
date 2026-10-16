// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { Navigate, useParams } from "react-router-dom";
import { InvestigationHistory } from "../history/InvestigationHistory";
import { useSetLifecycle } from "../context/ConsoleContext";

export { OVERRIDE_MIN } from "../components/DecisionPanel";

/**
 * Decisions now sit on the Investigation page at the gate result (#179). This route lists investigations to decide,
 * and an old /approvals/:id link opens that investigation at its Decision section.
 */
export function ApprovalPage() {
  const { investigationId = "" } = useParams();
  useSetLifecycle("approval", false);
  if (investigationId) return <Navigate replace to={`/investigations/${investigationId}#decision`} />;
  return (
    <div>
      <div className="mb-4">
        <h1 className="text-mps-display font-semibold">Decisions</h1>
        <p className="max-w-3xl text-mps-secondary">Past investigations, newest first. Open one to approve, reject, or ask for more evidence.</p>
      </div>
      <InvestigationHistory detail="approval" />
    </div>
  );
}
