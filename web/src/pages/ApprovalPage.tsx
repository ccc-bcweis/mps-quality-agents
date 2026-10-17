// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { Navigate, useParams } from "react-router-dom";

export { OVERRIDE_MIN } from "../components/DecisionPanel";

/**
 * There is no Decisions tab: the decision sits on the Investigation page (ADR-014). Old links still work:
 * /approvals opens the investigation list and /approvals/:id opens that investigation at its Decision section.
 */
export function ApprovalPage() {
  const { investigationId = "" } = useParams();
  if (investigationId) return <Navigate replace to={`/investigations/${investigationId}#decision`} />;
  return <Navigate replace to="/investigations" />;
}
