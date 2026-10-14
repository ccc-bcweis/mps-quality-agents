# Tool and fleet figures

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

`fleet.yaml` is a synthetic 90-day snapshot for the eight tools. The seed is `20261014`. Planned hours are 2160. The ideal run rate is 12 wafers/hour, which is one wafer every 300 seconds. All figures are illustrative.

Each tool draws five values from a seeded generator keyed by `kpi|<tool id>`. Values are rounded to two decimals, half away from zero.

- Performance is 0.90 plus 0.08 times the first draw. Run rate is 12 times that performance, so it lands between 10.80 and 11.76 wafers/hour.
- Uptime is 92 plus 6 times the second draw, in percent.
- First-pass yield is 94 plus 5 times the third draw, in percent.
- OEE is uptime, times run rate over 12, times first-pass yield, expressed as a percent of a perfect hour.
- Failures are 3 plus the whole number of the fourth draw times 4, so 3, 4, 5, or 6.
- MTTR is 1.5 plus 4.5 times the fifth draw, in hours. Repair hours are failures times MTTR.
- Operating hours are 2160 times uptime over 100. MTBF is operating hours divided by failures.

The fleet row sums run rate, operating hours, failures, and repair hours. Uptime is total operating hours divided by 2160 times the tool count. First-pass yield is weighted by run rate. MTBF is total operating hours divided by total failures. MTTR is total repair hours divided by total failures. Fleet OEE uses the same product as a tool, with fleet uptime, the average tool run rate, and the weighted first-pass yield.

The live sensor limits in the fleet files are a separate spec band. They are not these measured figures.

Regenerate by running the simulator test with `MPS_EXPORT_KPI` set to `1`.
