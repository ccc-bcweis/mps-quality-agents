// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import path from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test, type Page } from "@playwright/test";

const sample = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../data/samples/oscilloscope-e01.pdf");
const widths = [1280, 1600, 1920];

async function openTickets(page: Page, width: number) {
  await page.setViewportSize({ width, height: 1000 });
  await page.goto("/tickets");
  await expect(page.getByRole("heading", { name: "Tickets" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Operator report" })).toBeVisible();
}

async function layoutFaults(page: Page): Promise<string[]> {
  return page.locator("#main").evaluate((main) => {
    const faults: string[] = [];
    const mainBox = main.getBoundingClientRect();
    const panels = main.querySelectorAll("form, section");
    panels.forEach((panel) => {
      const box = panel.getBoundingClientRect();
      if (box.right > mainBox.right + 1 || box.left < mainBox.left - 1) faults.push("panel outside the page");
      panel.querySelectorAll("h2, p, span, button").forEach((node) => {
        if (!(node instanceof HTMLElement)) return;
        if (node.closest("select")) return;
        const style = getComputedStyle(node);
        if (style.display === "none" || style.display === "inline" || style.display === "contents" || style.visibility === "hidden") return;
        if (node.clientWidth < 2 || node.clientHeight < 2) return;
        const text = (node.textContent ?? "").replace(/\s+/g, " ").trim();
        if (!text) return;
        if (node.scrollWidth > node.clientWidth + 1 || node.scrollHeight > node.clientHeight + 1) {
          faults.push(`clipped ${node.tagName}: ${text.slice(0, 90)}`);
        }
      });
    });
    const headings = [...main.querySelectorAll("h1, h2")].filter((node) => node instanceof HTMLElement);
    for (let left = 0; left < headings.length; left += 1) {
      for (let right = left + 1; right < headings.length; right += 1) {
        const a = headings[left].getBoundingClientRect();
        const b = headings[right].getBoundingClientRect();
        const overlap = a.left < b.right - 2 && a.right > b.left + 2 && a.top < b.bottom - 2 && a.bottom > b.top + 2;
        if (overlap) faults.push(`overlap ${(headings[left].textContent ?? "").trim()} / ${(headings[right].textContent ?? "").trim()}`);
      }
    }
    return faults;
  });
}

test("tickets upload a printout and the parameter list follows the tool", async ({ page }) => {
  for (const width of widths) {
    await openTickets(page, width);
    const form = page.locator("form").filter({ has: page.getByRole("heading", { name: "Operator report" }) });
    await form.getByLabel("Tool").selectOption("E-01");
    const parameter = form.getByLabel("Parameter");
    await expect(parameter.getByRole("option", { name: /ESC temperature \(C\), spec 40-60/ }).first()).toBeAttached();
    await expect(parameter.getByRole("option", { name: /Film thickness/ })).toHaveCount(0);
    await form.getByLabel("Tool").selectOption("D-01");
    await expect(parameter.getByRole("option", { name: /ESC temperature/ })).toHaveCount(0);
    await expect(parameter.getByRole("option", { name: /RF power \(W\), spec 300-600/ }).first()).toBeAttached();
    await expect(form.locator("[data-selected-sensor]")).toBeVisible();
    const faults = await layoutFaults(page);
    expect(faults, `${width}px ${faults.join(" | ")}`).toEqual([]);
  }

  await page.setViewportSize({ width: 1280, height: 1000 });
  await page.getByLabel("Printout").setInputFiles(sample);
  await expect(page.getByLabel("Printed parameter")).toHaveValue("rfPower");
  await expect(page.getByLabel("Value")).toHaveValue("2140");
  for (const width of widths) {
    await page.setViewportSize({ width, height: 1000 });
    const faults = await layoutFaults(page);
    expect(faults, `draft ${width}px ${faults.join(" | ")}`).toEqual([]);
  }

  await page.getByRole("button", { name: "Confirm reading" }).click();
  await expect(page.getByText("An oscillation was applied to Source RF power on E-01 and an alert was opened.")).toBeVisible();
  await expect(page.getByText("Manual measurement: oscilloscope on E-01, rfPower 2140 W.")).toBeVisible();

  await page.getByRole("link", { name: "Alerts" }).click();
  await expect(page.getByRole("heading", { name: "Alerts" })).toBeVisible();
  await expect(page.getByText("Manual measurement outside the printed spec")).toBeVisible();
  await expect(page.getByRole("link", { name: "TKT-1300" })).toBeVisible();
});
