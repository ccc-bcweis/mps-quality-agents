// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { expect, test, type Locator, type Page } from "@playwright/test";

const FOCUS = "01ARZ3NDEKTSV4RRFFQ69G5FAV";

async function layoutFaults(root: Locator): Promise<string[]> {
  return root.evaluate((panel) => {
    const faults: string[] = [];
    const box = panel.getBoundingClientRect();
    if (box.right > window.innerWidth + 1 || box.left < -1) faults.push("panel outside the page");
    panel.querySelectorAll("h2, p, button, li, summary").forEach((node) => {
      if (!(node instanceof HTMLElement)) return;
      const style = getComputedStyle(node);
      if (style.display === "none" || style.visibility === "hidden") return;
      if (node.clientWidth < 2 || node.clientHeight < 2) return;
      const text = (node.textContent ?? "").replace(/\s+/g, " ").trim();
      if (!text) return;
      if (node.scrollWidth > node.clientWidth + 1 || node.scrollHeight > node.clientHeight + 1) {
        faults.push(`clipped ${node.tagName}: ${text.slice(0, 90)}`);
      }
    });
    const labels = [...panel.querySelectorAll("h2, p, button")].filter((node) => node instanceof HTMLElement);
    for (let left = 0; left < labels.length; left += 1) {
      for (let right = left + 1; right < labels.length; right += 1) {
        const a = labels[left].getBoundingClientRect();
        const b = labels[right].getBoundingClientRect();
        const overlap = a.left < b.right - 2 && a.right > b.left + 2 && a.top < b.bottom - 2 && a.bottom > b.top + 2;
        if (overlap) {
          faults.push(`overlap ${(labels[left].textContent ?? "").trim().slice(0, 40)} / ${(labels[right].textContent ?? "").trim().slice(0, 40)}`);
        }
      }
    }
    return faults;
  });
}

async function openCause(page: Page) {
  await page.goto("/investigations/2c6e1a40-7b2d-4f1e-8a33-91d0c5b6e7f8");
  await expect(page.getByRole("heading", { name: "Investigation" })).toBeVisible();
  await page.getByRole("button", { name: /Show the cause for ESC temperature/ }).click();
  const dialog = page.getByRole("dialog", { name: "Causal trace" });
  await expect(dialog).toBeVisible();
  const timeline = dialog.getByRole("list", { name: "Cause timeline" });
  await expect(timeline).toContainText("Chuck temperature was 53.1.");
  await expect(timeline).toContainText("Value 53.1, limit 40 to 60");
  await expect(timeline).toContainText("The business rule Edge ring temperature fired.");
  await expect(timeline).not.toContainText(FOCUS);
  return dialog;
}

test("an evidence item opens a causal trace that fits the page", async ({ page }) => {
  const dialog = await openCause(page);
  const faults = await layoutFaults(dialog);
  expect(faults, faults.join(" | ")).toEqual([]);
  await dialog.getByRole("button", { name: "Close" }).click();
  await expect(dialog).toHaveCount(0);
});
