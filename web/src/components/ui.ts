// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

/** Shape shared by every button. Colors are added per kind, so no two background classes fight over one button. */
const btnBase =
  "inline-flex items-center justify-center rounded border px-3 py-1.5 text-mps-body no-underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-mps-nominal disabled:cursor-not-allowed disabled:opacity-40";

export const btn = `${btnBase} border-mps-nominal/60 bg-mps-raised text-mps-nominal hover:border-mps-nominal hover:bg-mps-canvas`;

export const btnPrimary = `${btnBase} border-mps-nominal bg-mps-raised text-mps-nominal hover:bg-mps-canvas`;

/** Solid green: the approving action. */
export const btnOk = `${btnBase} border-transparent bg-mps-ok-solid font-semibold text-white hover:brightness-110`;

/** Solid blue: asks for more before deciding. */
export const btnInfo = `${btnBase} border-transparent bg-mps-info-solid text-white hover:brightness-110`;

/** Solid red: rejects or overrides. */
export const btnDanger = `${btnBase} border-transparent bg-mps-alarm-solid text-white hover:brightness-110`;

export const field =
  "mt-1 w-full rounded border border-mps-line bg-mps-canvas px-2 py-1.5 font-mono text-mps-body text-mps-text focus-visible:outline focus-visible:outline-2 focus-visible:outline-mps-nominal";

export const panel = "rounded border border-mps-line bg-mps-panel p-4 shadow-panel";

/** Compact colored button for navigation inside lists and record rows. */
export const btnSm =
  "inline-flex items-center justify-center rounded border border-mps-nominal/60 bg-mps-raised px-2 py-0.5 text-mps-label text-mps-nominal no-underline hover:border-mps-nominal hover:bg-mps-canvas focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-mps-nominal";
