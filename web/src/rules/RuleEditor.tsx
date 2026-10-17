// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useState } from "react";
import type { BusinessRule, RuleSetting } from "../api/types";
import { Explain } from "../components/Explain";
import { btn, btnOk, btnPrimary, panel } from "../components/ui";

/** Plain names for the rule actions in business-rules.yaml. */
const ACTION_TOGGLES: { type: string; label: string }[] = [
  { type: "open-ticket", label: "Open a ticket" },
  { type: "auto-investigate", label: "Start an investigation" },
  { type: "escalate-to-human", label: "Ask a person to review" },
];

export const NOT_SAVED_NOTICE = "Changes are not saved in this environment. The values shown are the ones the system is running with.";

type Draft = Record<string, string>;

function settingDraft(settings: RuleSetting[]): Draft {
  return Object.fromEntries(settings.map((setting) => [setting.id, setting.value]));
}

function ruleDraft(rules: BusinessRule[]): Draft {
  const draft: Draft = {};
  for (const rule of rules) {
    for (const condition of rule.conditions ?? []) draft[`${rule.id}:${condition.field}`] = condition.value;
    for (const toggle of ACTION_TOGGLES) draft[`${rule.id}:action:${toggle.type}`] = String(rule.actions.some((action) => action.type === toggle.type));
    const priority = rule.actions.find((action) => action.type === "set-priority")?.priority;
    const queue = rule.actions.find((action) => action.type === "route-to-queue")?.queue;
    if (priority) draft[`${rule.id}:priority`] = priority;
    if (queue) draft[`${rule.id}:queue`] = queue;
  }
  return draft;
}

const input = "w-24 rounded border border-mps-line bg-mps-canvas px-2 py-1 font-mono text-mps-body text-mps-text disabled:opacity-70";
const textInput = "w-40 rounded border border-mps-line bg-mps-canvas px-2 py-1 text-mps-body text-mps-text disabled:opacity-70";

function Toggle({ label, checked, disabled, onChange }: { label: string; checked: boolean; disabled: boolean; onChange: (next: boolean) => void }) {
  return (
    <label className="inline-flex items-center gap-2">
      <input type="checkbox" role="switch" aria-label={label} checked={checked} disabled={disabled} onChange={(event) => onChange(event.target.checked)} className="h-4 w-4 accent-mps-nominal" />
      <span>{label}</span>
    </label>
  );
}

/**
 * Rules and thresholds as fields. The values come from the loaded configuration; Edit unlocks the fields, and Save
 * says plainly that this environment does not keep changes, then shows the configured values again.
 */
export function RuleEditor({ rules, settings, lifecycle }: { rules: BusinessRule[]; settings: RuleSetting[]; lifecycle: { id: string; whenPlain: string; thenPlain: string; why: string }[] }) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState<Draft>({});
  const [notice, setNotice] = useState<string | null>(null);
  const base: Draft = { ...settingDraft(settings), ...ruleDraft(rules) };
  const value = (key: string) => (editing && key in draft ? draft[key] : base[key]) ?? "";
  const set = (key: string, next: string) => setDraft((current) => ({ ...current, [key]: next }));
  const groups = [...new Set(settings.map((setting) => setting.group))];

  function startEdit() {
    setDraft({});
    setNotice(null);
    setEditing(true);
  }
  function save() {
    setEditing(false);
    setDraft({});
    setNotice(NOT_SAVED_NOTICE);
  }
  function cancel() {
    setEditing(false);
    setDraft({});
  }

  return (
    <>
      <div className="mt-4 flex flex-wrap items-center gap-2">
        {editing ? (
          <>
            <button type="button" className={btnOk} onClick={save}>
              Save
            </button>
            <button type="button" className={btn} onClick={cancel}>
              Cancel
            </button>
          </>
        ) : (
          <button type="button" className={btnPrimary} onClick={startEdit}>
            Edit
          </button>
        )}
        {notice ? (
          <p role="status" data-testid="rules-notice" className="text-mps-secondary">
            {notice}
          </p>
        ) : null}
      </div>

      <section aria-label="Thresholds and settings" className={`${panel} mt-4`}>
        <h2 className="text-mps-title font-semibold">Thresholds and settings</h2>
        <div className="mt-3 grid gap-4 lg:grid-cols-2">
          {groups.map((group) => (
            <fieldset key={group} className="rounded border border-mps-line p-3">
              <legend className="px-1 font-semibold">
                <Explain text={group} spellOut={false} />
              </legend>
              <div className="grid gap-2">
                {settings
                  .filter((setting) => setting.group === group)
                  .map((setting) =>
                    setting.kind === "toggle" ? (
                      <Toggle key={setting.id} label={setting.label} checked={value(setting.id) === "true"} disabled={!editing} onChange={(next) => set(setting.id, String(next))} />
                    ) : (
                      <label key={setting.id} className="flex flex-wrap items-center gap-2">
                        <span className="min-w-[14rem] text-mps-secondary">{setting.label}</span>
                        <input type="number" aria-label={setting.label} className={input} value={value(setting.id)} disabled={!editing} onChange={(event) => set(setting.id, event.target.value)} />
                        {setting.unit ? <span className="text-mps-muted">{setting.unit}</span> : null}
                      </label>
                    ),
                  )}
              </div>
            </fieldset>
          ))}
        </div>
      </section>

      <section aria-label="Business rules" className={`${panel} mt-4`}>
        <h2 className="text-mps-title font-semibold">Rules</h2>
        <p className="mt-1 text-mps-secondary">The first rule that matches wins, lowest order first.</p>
        <table className="mt-3 w-full table-fixed text-left">
          <thead>
            <tr className="text-mps-micro text-mps-muted">
              <th className="w-1/4 py-2 pr-3 font-semibold">When</th>
              <th className="w-1/4 py-2 pr-3 font-semibold">Then</th>
              <th className="w-1/4 py-2 pr-3 font-semibold">Why</th>
              <th className="w-1/4 py-2 font-semibold">Settings</th>
            </tr>
          </thead>
          <tbody>
            {rules.map((rule) => (
              <tr key={rule.id} className="border-t border-mps-line align-top" data-rule={rule.id}>
                <td className="break-words py-2 pr-3">{rule.whenPlain}</td>
                <td className="break-words py-2 pr-3">{rule.thenPlain}</td>
                <td className="break-words py-2 pr-3">{rule.why}</td>
                <td className="py-2">
                  <div className="grid gap-1">
                    {(rule.conditions ?? []).map((condition) => {
                      const key = `${rule.id}:${condition.field}`;
                      return condition.kind === "toggle" ? (
                        <Toggle key={key} label={condition.label} checked={value(key) === "true"} disabled={!editing} onChange={(next) => set(key, String(next))} />
                      ) : (
                        <label key={key} className="flex flex-wrap items-center gap-2">
                          <span className="text-mps-secondary">{condition.label}</span>
                          <input
                            type={condition.kind === "number" ? "number" : "text"}
                            aria-label={`${rule.name}: ${condition.label}`}
                            className={condition.kind === "number" ? input : textInput}
                            value={value(key)}
                            disabled={!editing}
                            onChange={(event) => set(key, event.target.value)}
                          />
                        </label>
                      );
                    })}
                    {ACTION_TOGGLES.map((toggle) => {
                      const key = `${rule.id}:action:${toggle.type}`;
                      return <Toggle key={key} label={toggle.label} checked={value(key) === "true"} disabled={!editing} onChange={(next) => set(key, String(next))} />;
                    })}
                    {base[`${rule.id}:priority`] ? (
                      <label className="flex flex-wrap items-center gap-2">
                        <span className="text-mps-secondary">Priority</span>
                        <input aria-label={`${rule.name}: Priority`} className={textInput} value={value(`${rule.id}:priority`)} disabled={!editing} onChange={(event) => set(`${rule.id}:priority`, event.target.value)} />
                      </label>
                    ) : null}
                    {base[`${rule.id}:queue`] ? (
                      <label className="flex flex-wrap items-center gap-2">
                        <span className="text-mps-secondary">Queue</span>
                        <input aria-label={`${rule.name}: Queue`} className={textInput} value={value(`${rule.id}:queue`)} disabled={!editing} onChange={(event) => set(`${rule.id}:queue`, event.target.value)} />
                      </label>
                    ) : null}
                  </div>
                </td>
              </tr>
            ))}
            {lifecycle.map((rule) => (
              <tr key={rule.id} className="border-t border-mps-line align-top">
                <td className="break-words py-2 pr-3">{rule.whenPlain}</td>
                <td className="break-words py-2 pr-3">{rule.thenPlain}</td>
                <td className="break-words py-2 pr-3">{rule.why}</td>
                <td className="py-2 text-mps-muted">Fixed by the case lifecycle.</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </>
  );
}
