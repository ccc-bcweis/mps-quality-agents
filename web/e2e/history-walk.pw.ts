// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { expect, test, type Page } from "@playwright/test";

const forbidden = /no such record|did not load|was not accepted|was not found/i;

const consoleTabs: { name: string; heading: RegExp }[] = [
  { name: "Sites", heading: /^Sites$/ },
  { name: "Fleet", heading: /Fleet overview/ },
  { name: "Tool", heading: /Tool E-02/ },
  { name: "Alerts", heading: /^Alerts$/ },
  { name: "Tickets", heading: /^Tickets$/ },
  { name: "Investigation", heading: /^Investigation$/ },
  { name: "Approval", heading: /^Approval$/ },
  { name: "CAPA", heading: /^CAPA$/ },
];

const harnessLinks: { name: string; heading: RegExp }[] = [
  { name: "Fault injector", heading: /^Fault injector$/ },
  { name: "Return on investment", heading: /ROI/ },
  { name: "Metrics", heading: /Metrics \/ eval/ },
];

const historyTables = ["Alerts", "Tickets", "Investigations", "Approvals", "Corrective actions"];

async function assertNoError(page: Page) {
  await expect(page.locator("body")).not.toContainText(forbidden);
}

async function assertHistoryTablesFit(page: Page) {
  const problems = await page.evaluate((names) => {
    const found: string[] = [];
    for (const name of names) {
      const table = document.querySelector(`table[aria-label="${name}"]`);
      if (!table) continue;
      for (const cell of table.querySelectorAll("th, td")) {
        const style = getComputedStyle(cell);
        const hidesOverflow = style.overflowX === "hidden" || style.overflow === "hidden" || style.textOverflow === "ellipsis";
        if (hidesOverflow && cell.scrollWidth > cell.clientWidth + 1) {
          found.push(`${name} clips "${(cell.textContent ?? "").trim().slice(0, 40)}"`);
        }
      }
      for (const row of table.querySelectorAll("tr")) {
        const boxes = Array.from(row.children).map((cell) => {
          const rect = cell.getBoundingClientRect();
          return { left: rect.left, right: rect.right, top: rect.top, bottom: rect.bottom, text: (cell.textContent ?? "").trim().slice(0, 24) };
        });
        for (let i = 0; i < boxes.length; i += 1) {
          for (let j = i + 1; j < boxes.length; j += 1) {
            const a = boxes[i];
            const b = boxes[j];
            const overlapX = Math.min(a.right, b.right) - Math.max(a.left, b.left);
            const overlapY = Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top);
            if (overlapX > 2 && overlapY > 2) found.push(`${name} overlaps "${a.text}" and "${b.text}"`);
          }
        }
      }
    }
    return found;
  }, historyTables);
  expect(problems).toEqual([]);
}

test("walks every console tab and harness link after a scenario", async ({ page }) => {
  await page.goto("/");
  const harness = page.getByRole("banner", { name: "StatusNeo demo harness" });
  await expect(harness).toBeVisible();
  await harness.getByRole("button", { name: "Expand harness" }).click();
  await expect(harness.getByRole("option", { name: /SC-1/ })).toBeAttached();
  await harness.getByRole("combobox", { name: "Scenario" }).selectOption("SC-1");
  await harness.getByRole("button", { name: "Run scenario" }).click();

  const consoleRegion = page.getByRole("region", { name: "Meridian Precision Systems console" });
  await expect(consoleRegion.getByRole("heading", { name: /Tool E-02/ })).toBeVisible();
  await expect(consoleRegion.getByRole("status", { name: "Streaming trace" })).toBeVisible();
  await assertNoError(page);
  await assertHistoryTablesFit(page);

  const nav = consoleRegion.getByRole("navigation", { name: "Console" });
  for (const tab of consoleTabs) {
    await nav.getByRole("link", { name: tab.name, exact: true }).click();
    await expect(consoleRegion.getByRole("heading", { name: tab.heading })).toBeVisible();
    await assertNoError(page);
    if (tab.name === "CAPA") await expect(consoleRegion.locator("main")).toContainText("No CAPA yet");
    await assertHistoryTablesFit(page);
  }

  for (const link of harnessLinks) {
    await harness.getByRole("link", { name: link.name, exact: true }).click();
    await expect(page.getByRole("heading", { name: link.heading })).toBeVisible();
    await assertNoError(page);
  }
});
