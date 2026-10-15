// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

/**
 * Match copy that a glossary term splits across elements.
 * Testing Library reads only an element's own text nodes, so a phrase such as
 * "No CAPA yet" is invisible to getByText once CAPA is its own node.
 * The innermost element whose full textContent still matches is the one the reader sees.
 */
export function matchCopy(expected: string | RegExp) {
  const pattern = typeof expected === "string" ? null : new RegExp(expected.source, expected.flags);
  function hit(text: string): boolean {
    return pattern ? pattern.test(text) : text === expected;
  }
  return (_content: string, element: Element | null) => {
    if (!element) return false;
    const text = element.textContent ?? "";
    if (!hit(text)) return false;
    return Array.from(element.children).every((child) => !hit(child.textContent ?? ""));
  };
}
