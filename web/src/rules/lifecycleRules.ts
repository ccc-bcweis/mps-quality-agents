// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

/** How a case closes. Enforced by the server; listed here so the Rules table shows the whole path. */

const WORDS: Record<string, string> = { "1": "one", "2": "two", "3": "three", "4": "four" };

/** The lifecycle rules, with the alert-clearing numbers taken from the server's settings ("—" until they load). */
export function lifecycleRules(clearReadings = "—", clearSigma = "—") {
  return LIFECYCLE_RULES.map((rule) =>
    rule.id === "life-alert-clear"
      ? { ...rule, whenPlain: `When ${clearReadings} readings in a row stay inside ${WORDS[clearSigma] ?? clearSigma} sigma after an alert.` }
      : rule,
  );
}

const LIFECYCLE_RULES = [
  {
    id: "life-capa-on-approval",
    whenPlain: "When a person approves the recommendation.",
    thenPlain: "Open a corrective action (CAPA) for that tool.",
    why: "Every approved cause gets a tracked fix.",
  },
  {
    id: "life-containment",
    whenPlain: "When the corrective action moves to Containment.",
    thenPlain: "Remove the fault on that tool so its readings return to nominal, and record it in the trace.",
    why: "Containment stops the problem before the full fix is done.",
  },
  {
    id: "life-alert-clear",
    whenPlain: "When the configured number of readings in a row stay inside the clearing band after an alert.",
    thenPlain: "Clear the alert.",
    why: "A single good reading is not enough to call the signal recovered.",
  },
  {
    id: "life-verification",
    whenPlain: "When someone moves the corrective action to Verification.",
    thenPlain: "Allow it only if no alert on that signal is still active.",
    why: "Verification means the readings are back in control.",
  },
  {
    id: "life-close",
    whenPlain: "When the corrective action is closed.",
    thenPlain: "Close the incident and its ticket. The investigation keeps its Approved result.",
    why: "The case is done only when the fix is verified and closed.",
  },
];
