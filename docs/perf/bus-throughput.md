# Bus throughput

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

Measured on 2026-10-15 by `NominalWeekPublishesEveryPlantAtTheChosenStride` in `tests/Api.Tests/BusThroughputTests.cs`. The test calls `INominalHistorySeeder.SeedAsync(NominalWindow.LastWeek)`. The figures below are that run, after the stride was pinned.

## What was seeded

The window ends at the simulator clock and reaches back seven days. Every placed tool at Northwater, Ironwood, and Lumen is included: 32 tools and 574 parameters. Readings go through the in-process bus into the telemetry table. They are not copied into the event log, so events per second and rows per second are the same number.

The last hour is one point every 25 seconds. Older points are one every 900 seconds. Those two numbers are constants. An 8,000-row sample is still timed so the note can show a one-series rate, and the full-rate totals below are projected from the series count. The sample does not change the stride.

| Measure | Result |
|---|---|
| Tools | 32 |
| Parameters | 574 |
| Rows inserted | 466,662 |
| Seed time | 7.02 seconds |
| Events per second | 66,442 |
| Rows per second | 66,442 |
| Sample, 8,000 rows on one series | 36,401 rows per second |
| Database size | 131,798,200 bytes |

Database size is the database file plus its write-ahead log file after the insert. Each series holds 813 points (466,662 divided by 574).

## Chart reads

One tool and one parameter. Three warmup reads, then 20 timed reads. The figure is the 95th percentile. The index is tool, parameter, and time. The pages are already cached.

| Window | 95th percentile |
|---|---|
| 15 minutes | 0.036 milliseconds |
| 1 hour | 0.060 milliseconds |
| 7 days | 0.237 milliseconds |

## Full rate, not inserted

These totals are 574 parameters times one row per second. The seeder did not insert them.

| Window | Projected rows |
|---|---|
| 1 hour | 2,066,400 |
| 24 hours | 49,593,600 |
| 7 days | 347,155,200 |

## Why the last hour is not one reading per second

One hour at one reading per second for all 574 series is 2,066,400 rows. At the measured insert rate, that hour plus the 900-second older points is about 2.45 million rows and about 37 seconds. That misses the 20-second seed target. The pinned history is 466,662 rows and finished in 7.02 seconds. Chart reads are under 100 milliseconds.

A 5-second last hour, with the same older stride, would be about 797,000 rows and about 12 seconds at this insert rate. It was not adopted. The 25-second hour already meets the seed and chart targets.

The stride used to follow the 8,000-row sample and an 18-second budget. On a quiet run that rule picked these same 25-second and 900-second strides (466,662 rows in 7.08 seconds, sample 34,624 rows per second). During the full test suite the sample fell to 22,340 rows per second, and the same rule would have stretched older points to 1,800 seconds (274,946 rows in 6.97 seconds). The stride is now a constant so the history other lanes seed does not change with machine load.

`INominalHistorySeeder.SeedAsync` is the call other lanes use. `NominalWindow.LastWeek` is this history. `LastHour` uses the 25-second stride for the whole hour. `LastDay` uses 25 seconds for the last hour and 900 seconds before that.
