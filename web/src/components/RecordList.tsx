// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { panel } from "./ui";

export type HistoryRow = {
  id: string;
  status: ReactNode;
  toolName: string;
  toolId?: string;
  time: string;
  detail: ReactNode;
};

export function HistoryNote({ text }: { text: string }) {
  return (
    <p className="mb-3 rounded border border-mps-line bg-mps-panel px-3 py-2" role="status">
      {text}
    </p>
  );
}

/** Past records, newest first. Cells wrap so labels stay visible at 1280. */
export function RecordList({ label, rows, empty }: { label: string; rows: HistoryRow[]; empty: string }) {
  if (rows.length === 0) return <p className="text-mps-muted">{empty}</p>;
  return (
    <div className={`${panel} overflow-x-auto`}>
      <table className="w-full text-left" aria-label={label}>
        <thead>
          <tr className="text-mps-micro text-mps-muted">
            <th className="py-1 pr-3 font-medium">Status</th>
            <th className="py-1 pr-3 font-medium">Tool</th>
            <th className="py-1 pr-3 font-medium">Time</th>
            <th className="py-1 font-medium">Detail</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.id} className="border-t border-mps-line align-top">
              <td className="break-words py-1.5 pr-3">{row.status}</td>
              <td className="break-words py-1.5 pr-3">
                {row.toolId ? <Link to={`/tools/${row.toolId}`}>{row.toolName}</Link> : row.toolName}
              </td>
              <td className="break-words py-1.5 pr-3 font-mono text-mps-micro">{row.time}</td>
              <td className="break-words py-1.5">{row.detail}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
