// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { splitGlossary } from "../glossary/split";
import { Term } from "./Term";

export function Explain({ text, spellOut = true }: { text: string; spellOut?: boolean }) {
  const parts = splitGlossary(text);
  if (parts.every((part) => part.kind === "text")) return text;
  return (
    <>
      {parts.map((part, index) =>
        part.kind === "term" ? (
          <Term key={`${part.term}-${index}`} id={part.term} spellOut={spellOut} />
        ) : (
          <span key={`text-${index}`}>{part.text}</span>
        ),
      )}
    </>
  );
}
