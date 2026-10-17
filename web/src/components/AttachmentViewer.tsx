// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useId, useRef } from "react";
import { btn, btnPrimary } from "./ui";

/** PDFs open in the browser's own viewer; images show as an image. Decided from the file name the server kept. */
function attachmentKind(fileName: string | null | undefined): "pdf" | "image" {
  return /\.pdf$/i.test(fileName ?? "") ? "pdf" : "image";
}

/** In-app viewer for an uploaded printout, with a way out to a new tab. */
export function AttachmentViewer({ url, fileName, title, onClose }: { url: string; fileName?: string | null; title: string; onClose: () => void }) {
  const titleId = useId();
  const close = useRef<HTMLButtonElement>(null);
  const kind = attachmentKind(fileName);

  useEffect(() => {
    close.current?.focus();
  }, []);

  return (
    <div className="fixed inset-0 z-overlay flex items-center justify-center bg-black/70 p-4" onClick={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        className="flex h-[85vh] w-full max-w-4xl flex-col rounded border border-mps-line bg-mps-panel p-4 shadow-overlay"
        onClick={(event) => event.stopPropagation()}
        onKeyDown={(event) => {
          if (event.key === "Escape") onClose();
        }}
      >
        <div className="mb-3 flex flex-wrap items-center gap-2">
          <h2 id={titleId} className="min-w-0 flex-1 break-words text-mps-title font-semibold">
            {title}
          </h2>
          <a className={btnPrimary} href={url} target="_blank" rel="noreferrer">
            Open in new tab
          </a>
          <button ref={close} type="button" className={btn} onClick={onClose}>
            Close
          </button>
        </div>
        <div className="min-h-0 flex-1 overflow-auto rounded border border-mps-line bg-mps-canvas">
          {kind === "pdf" ? (
            <iframe title={title} src={url} className="h-full w-full" />
          ) : (
            <img src={url} alt={title} className="mx-auto max-h-full max-w-full object-contain" />
          )}
        </div>
      </div>
    </div>
  );
}
