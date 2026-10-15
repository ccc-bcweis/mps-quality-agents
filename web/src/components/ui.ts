// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

export const btn =
  "inline-flex items-center justify-center rounded border border-mps-nominal/60 bg-mps-raised px-3 py-1.5 text-mps-body text-mps-nominal no-underline hover:border-mps-nominal hover:bg-mps-canvas focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-mps-nominal disabled:cursor-not-allowed disabled:opacity-40";

export const btnPrimary = `${btn} border-mps-nominal text-mps-nominal`;

export const btnOk = `${btn} border-transparent bg-mps-ok-solid text-white hover:border-transparent`;

export const btnDanger = `${btn} border-transparent bg-mps-alarm-solid text-white hover:border-transparent`;

export const field =
  "mt-1 w-full rounded border border-mps-line bg-mps-canvas px-2 py-1.5 font-mono text-mps-body text-mps-text focus-visible:outline focus-visible:outline-2 focus-visible:outline-mps-nominal";

export const panel = "rounded border border-mps-line bg-mps-panel p-4 shadow-panel";

/** Compact colored button for navigation inside lists and record rows. */
export const btnSm =
  "inline-flex items-center justify-center rounded border border-mps-nominal/60 bg-mps-raised px-2 py-0.5 text-mps-label text-mps-nominal no-underline hover:border-mps-nominal hover:bg-mps-canvas focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-mps-nominal";
