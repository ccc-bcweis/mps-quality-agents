// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { renderConsole } from "../test/render";
import { server } from "../test/server";

const T0 = "2026-09-01T00:00:00Z";

function points(base: number) {
  return [0, 1, 2].map((index) => ({ at: `2026-09-01T00:00:0${index}Z`, value: base + index * 0.1 }));
}

// Live (Oct 17): Bias RF reflected and the tool counters showed "Waiting for readings..." forever. These sensors have
// no process parameter, so the server streams them under their sensor id; the chart looked for a series named "".
describe("tool sensors with no process parameter", () => {
  it.each([
    ["E-01-A-RF-BIAS-RFL", "E-01-A", 10.5, /SPC chart of .*Mean 10\.50/],
    ["E-01-B-RF-BIAS-RFL", "E-01-B", 10.5, /SPC chart of .*Mean 10\.50/],
    ["E-01-RUN-RATE", "E-01", 12, /SPC chart of .*Mean 12\.00/],
    ["E-01-THROUGHPUT", "E-01", 11.4, /SPC chart of .*Mean 11\.40/],
    ["E-01-YIELD", "E-01", 97, /SPC chart of .*Mean 97\.00/],
    ["E-01-OUTPUT", "E-01", 12, /Trend of .*running count/],
  ])("charts %s from the series keyed by its sensor id", async (sensorId, chamberId, center, figure) => {
    server.use(
      http.get("/api/tools/:toolId/live", () =>
        HttpResponse.json({
          toolId: "E-01",
          siteId: "northwater",
          simulatedNow: T0,
          status: "nominal",
          readings: [],
          series: [{ parameter: sensorId, chamberId, center, sigma: 1, points: points(center) }],
        }),
      ),
    );
    renderConsole("/tools/E-01");
    await userEvent.click(await screen.findByRole("button", { name: new RegExp(sensorId) }));

    expect(await screen.findByRole("figure", { name: figure })).toBeInTheDocument();
    expect(screen.queryByRole("figure", { name: /Waiting for readings/ })).not.toBeInTheDocument();
    expect(screen.getByText(/n = 3/)).toBeInTheDocument();
    expect(screen.queryByRole("figure", { name: /Waiting for readings/ })).not.toBeInTheDocument();
  });
});
