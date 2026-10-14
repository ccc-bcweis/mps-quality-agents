// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { MATRIX_CLASSES, markLabel, matrixFor } from "../investigation/matrix";

export function EvidenceMatrix({ investigationId }: { investigationId: string }) {
  const rows = matrixFor(investigationId);
  if (rows.length === 0) return null;
  return (
    <div>
      <h2 className="mb-2 text-mps-title font-semibold">Root-cause evidence matrix</h2>
      <p className="mb-2 text-mps-secondary">Each row says whether that evidence supports a class or rules it out. An em dash is still open.</p>
      <div className="overflow-x-auto">
        <table aria-label="Root-cause evidence matrix" className="w-full text-left">
          <thead>
            <tr className="font-mono text-mps-micro uppercase text-mps-muted">
              <th className="py-1 pr-2 font-medium">Evidence</th>
              {MATRIX_CLASSES.map((column) => (
                <th key={column.id} className="py-1 pr-2 font-medium">
                  {column.label}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.id} className="border-t border-mps-line">
                <th scope="row" className="py-1.5 pr-2 font-medium">
                  {row.label}
                  {row.note ? <span className="mt-1 block font-normal text-mps-secondary">{row.note}</span> : null}
                </th>
                {MATRIX_CLASSES.map((column) => (
                  <td key={column.id} className="py-1.5 pr-2 font-mono text-mps-micro">
                    {markLabel(row.cells[column.id])}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
