// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { splitGlossary } from "../glossary/split";
import { Term } from "./Term";
import { plainParameters } from "../labels";

export function Explain({ text: raw, spellOut = true }: { text: string; spellOut?: boolean }) {
  const text = plainParameters(raw);
  const parts = splitGlossary(text);
  if (parts.every((part) => part.kind === "text")) return text;
  // One inline wrapper: inside a flex row or button, loose text and term spans would each become a flex item
  // and lose the spaces between them ("ApproveCAPA").
  return (
    <span>
      {parts.map((part, index) =>
        part.kind === "term" ? (
          <Term key={`${part.term}-${index}`} id={part.term} spellOut={spellOut} />
        ) : (
          <span key={`text-${index}`}>{part.text}</span>
        ),
      )}
    </span>
  );
}
