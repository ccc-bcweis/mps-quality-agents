// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { expect, test, type Page } from "@playwright/test";

async function expand(page: Page) {
  const harness = page.getByRole("banner", { name: "StatusNeo demo harness" });
  await expect(harness).toBeVisible();
  const toggle = harness.getByRole("button", { name: "Expand harness" });
  if (await toggle.isVisible()) await toggle.click();
  await expect(harness.getByRole("button", { name: "Collapse harness" })).toBeVisible();
  return harness;
}

async function labelFaults(page: Page): Promise<string[]> {
  return page.locator("[data-region='harness']").evaluate((root) => {
    const faults: string[] = [];
    const view = root.ownerDocument.defaultView;
    if (!view) return faults;
    const nodes = [...root.querySelectorAll("button, a, label, p, span, h2")].filter((node) => node instanceof HTMLElement);
    function shownBox(node: HTMLElement): DOMRect | null {
      const style = view.getComputedStyle(node);
      if (style.display === "none" || style.visibility === "hidden") return null;
      if (node.closest("select") || node.closest("details:not([open])")) return null;
      if (node.clientWidth < 2 || node.clientHeight < 2) return null;
      let top = node.getBoundingClientRect().top;
      let right = node.getBoundingClientRect().right;
      let bottom = node.getBoundingClientRect().bottom;
      let left = node.getBoundingClientRect().left;
      let parent = node.parentElement;
      while (parent) {
        const parentStyle = view.getComputedStyle(parent);
        const clips =
          parentStyle.overflowY === "auto" ||
          parentStyle.overflowY === "scroll" ||
          parentStyle.overflowY === "hidden" ||
          parentStyle.overflowX === "hidden" ||
          parentStyle.overflow === "hidden";
        if (clips) {
          const bounds = parent.getBoundingClientRect();
          top = Math.max(top, bounds.top);
          left = Math.max(left, bounds.left);
          right = Math.min(right, bounds.right);
          bottom = Math.min(bottom, bounds.bottom);
        }
        parent = parent.parentElement;
      }
      if (right - left < 2 || bottom - top < 2) return null;
      return new DOMRect(left, top, right - left, bottom - top);
    }
    const visible = nodes
      .map((node) => ({ node, box: shownBox(node) }))
      .filter((item): item is { node: HTMLElement; box: DOMRect } => item.box !== null && (item.node.textContent ?? "").trim().length > 0);
    for (const item of visible) {
      const text = (item.node.textContent ?? "").replace(/\s+/g, " ").trim();
      if (item.node.scrollWidth > item.node.clientWidth + 1) faults.push(`clipped: ${text.slice(0, 80)}`);
      if (item.box.left < -1 || item.box.right > view.innerWidth + 1) faults.push(`off screen: ${text.slice(0, 80)}`);
    }
    for (let i = 0; i < visible.length; i += 1) {
      for (let j = i + 1; j < visible.length; j += 1) {
        const a = visible[i];
        const b = visible[j];
        if (a.node.contains(b.node) || b.node.contains(a.node)) continue;
        const left = a.box;
        const right = b.box;
        const overlapX = Math.min(left.right, right.right) - Math.max(left.left, right.left);
        const overlapY = Math.min(left.bottom, right.bottom) - Math.max(left.top, right.top);
        if (overlapX > 2 && overlapY > 2) {
          const aText = (a.node.textContent ?? "").replace(/\s+/g, " ").trim().slice(0, 40);
          const bText = (b.node.textContent ?? "").replace(/\s+/g, " ").trim().slice(0, 40);
          faults.push(`overlaps "${aText}" and "${bText}"`);
        }
      }
    }
    return faults;
  });
}

test("remembers an open harness in this browser", async ({ page }) => {
  await page.goto("/");
  const harness = page.getByRole("banner", { name: "StatusNeo demo harness" });
  await expect(harness.getByRole("button", { name: "Expand harness" })).toHaveAttribute("aria-expanded", "false");
  await expect(harness.getByRole("status", { name: "Harness status" })).toContainText(/Live · SC-1 · Offline (saved answers)/);
  await harness.getByRole("button", { name: "Expand harness" }).click();
  await page.reload();
  await expect(harness.getByRole("button", { name: "Collapse harness" })).toHaveAttribute("aria-expanded", "true");
  await expect(harness.getByRole("combobox", { name: "Scenario" })).toBeVisible();
  await harness.getByRole("button", { name: "Collapse harness" }).click();
  await page.reload();
  await expect(harness.getByRole("button", { name: "Expand harness" })).toBeVisible();
  await expect(harness.getByRole("combobox", { name: "Scenario" })).toHaveCount(0);
});

test("keeps an open harness within a third of a short screen", async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto("/");
  const harness = await expand(page);
  const box = await harness.boundingBox();
  expect(box).not.toBeNull();
  expect(box!.height).toBeLessThanOrEqual(267);
  const scrolling = page.locator("[data-region='console'] [data-scroll='content']");
  await expect(scrolling).toBeVisible();
  const overflow = await scrolling.evaluate((node) => getComputedStyle(node).overflowY);
  expect(overflow).toBe("auto");
});

test("harness labels stay clear", async ({ page }) => {
  const width = page.viewportSize()?.width ?? 1280;
  await page.setViewportSize({ width, height: 800 });
  await page.goto("/");
  await expand(page);
  expect(await labelFaults(page)).toEqual([]);
  const harness = page.getByRole("banner", { name: "StatusNeo demo harness" });
  await harness.getByRole("combobox", { name: "Scenario" }).selectOption("SC-4");
  await harness.getByRole("group", { name: "Plant data" }).getByRole("button", { name: "Replay" }).click();
  const banner = page.getByRole("status", { name: "Replay banner" });
  await expect(banner).toContainText("recorded run for SC-4");
  await expect(banner).toBeVisible();
  expect(await labelFaults(page)).toEqual([]);
});
