# ROI assumptions

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

These figures are illustrative and synthetic. They describe one fictional year at a 3-plant Meridian Precision Systems. They are not a forecast and they are not measured from the telemetry seed.

`Mps.Simulator.Roi.RoiCalculator` is the source. `tools/DataGen --roi data/roi` writes `data/roi/baseline.csv`, `data/roi/projection.csv`, and `data/roi/sensitivity.csv`. The same assumptions always produce the same bytes. Seed `20261014` is stamped on every row and does not scale the dollars. Changing the incident count changes labor by the formula below.

The console ROI screen and deck slides 3 and 9 (`docs/PLAN.md` section 7, and `docs/deck/roi-slides.md`) can read the three CSV files directly. The $180,000 platform line is this illustrative model. It is not the hosted demo's Azure budget (`mpsdemo-monthly`, $40 a month). Dollars are whole numbers with no thousands separators. Day and hour counts use two decimal places. Rates use four. Payback months use two decimal places and are blank when gross savings are zero.

## Inputs from the plan

| Input | Value | Source |
|---|---:|---|
| Revenue | 1200000000 USD | PLAN section 6.5 |
| COPQ rate | 8% of revenue | PLAN section 6.5 |
| Quality incidents | 1400 per year | PLAN section 6.5 |
| MTTR-Q | 9.5 days | PLAN section 6.5 |
| Time to root-cause hypothesis | 4 days | PLAN section 6.5 |
| Repeat-defect rate | 18% | PLAN section 6.5 |
| CAPA cycle time | 62 days | PLAN section 6.5 |
| Engineer loaded cost | 95 USD per hour | PLAN section 6.5 |
| Hours per investigation | 22 | PLAN section 6.5 |
| Tool downtime | 12000 USD per hour | PLAN section 6.5 |

## Lane assumptions

PLAN names the cost-to-run category and the downtime lever. It does not give an AI price, a platform price, a capture fraction, or the split of the non-labor COPQ. This lane uses the following. They are not contract changes.

| Assumption | Value | Role |
|---|---:|---|
| AI cost | 4 USD per investigation | Cost to run |
| Platform | 180000 USD per year | Cost to run |
| Downtime capture | 0.35 | Share of downtime dollars that earlier detection can avoid |
| Scrap share of the remainder | 0.50 | Remainder is COPQ minus investigation labor |
| Warranty share of the remainder | 0.22 | Same remainder |
| Downtime bucket | residual | Makes the four COPQ buckets sum to COPQ |

## Formulas

Investigation labor = incidents × 22 × 95.

COPQ = revenue × 0.08, rounded to the nearest dollar, midpoint away from zero.

Remainder = COPQ − labor.

Scrap = truncate(remainder × 0.50) toward zero.

Warranty = truncate(remainder × 0.22) toward zero.

Downtime dollars = remainder − scrap − warranty.

Downtime hours = downtime dollars / 12000. The CSV shows this at two decimal places. Savings use the unrounded hours and dollars, and the dollar result is rounded afterward.

Repeat incidents = incidents × 0.18.

Cost to run = (incidents × 4) + 180000. A projection row pays it only when gross savings are above zero. The current-state row pays 0, so its payback cell is blank. `baseline.csv` still publishes 185600 as the annual cost if the platform is adopted.

Labor savings = labor × the investigation-hour reduction.

Repeat-scrap savings = repeat incidents × (scrap / incidents) × the repeat reduction.

The repeat lever is relative. An 18% rate with a 25% relative reduction becomes 13.5%. The lever applies to scrap only. Warranty and field cost stay at the baseline. CAPA cycle time is published for the deck and does not change the dollar savings.

Downtime savings = downtime dollars × the hypothesis reduction × capture.

Hours avoided = downtime hours × the hypothesis reduction × capture.

Each savings term is rounded to the nearest dollar before the terms are added. Gross is that sum. Net = gross − the run cost on that row.

Payback months = cost to run × 12 / gross.

MTTR-Q projected = 9.5 − (4 × the hypothesis reduction). The hypothesis interval sits inside MTTR-Q. The other 5.5 days stay.

CAPA cycle projected = 62 × (1 − the CAPA reduction).

Investigation hours projected = 22 × (1 − the investigation reduction).

## Cases

| Case | Investigation hours | Hypothesis time | Repeat rate | CAPA cycle | Capture |
|---|---:|---:|---:|---:|---:|
| baseline | 0% | 0% | 0% | 0% | 0 |
| conservative | −30% | −50% | −15% | −20% | 0.35 |
| expected | −45% | −70% | −25% | −35% | 0.35 |

## Worked year

| Bucket | USD |
|---|---:|
| COPQ | 96000000 |
| Investigation labor | 2926000 |
| Scrap | 46537000 |
| Warranty | 20476280 |
| Downtime | 26060720 |

| Case | Labor saved | Repeat scrap saved | Downtime saved | Gross | Run cost | Net | Payback months |
|---|---:|---:|---:|---:|---:|---:|---:|
| baseline | 0 | 0 | 0 | 0 | 0 | 0 | blank |
| conservative | 877800 | 1256499 | 4560626 | 6694925 | 185600 | 6509325 | 0.33 |
| expected | 1316700 | 2094165 | 6384876 | 9795741 | 185600 | 9610141 | 0.23 |

Downtime hours in the baseline are 2171.73. Hours avoided are 380.05 in the conservative case and 532.07 in the expected case. Projected MTTR-Q is 7.50 and 6.70 days. Projected CAPA cycle time is 49.60 and 40.30 days.

## Sensitivity

One expected-case lever moves. The others stay on the expected case.

| Lever | Values |
|---|---|
| investigation | 0.30, 0.45, 0.60 |
| hypothesis | 0.50, 0.70, 0.80 |
| repeat | 0.15, 0.25, 0.35 |
| capture | 0.25, 0.35, 0.45 |

The expected value of each lever reproduces the expected gross, net, and payback.

## CSV columns

`baseline.csv`: seed, label, scope, revenueUsd, copqRate, copqUsd, laborUsd, scrapUsd, warrantyUsd, downtimeUsd, incidents, mttrDays, hypothesisDays, repeatRate, capaCycleDays, investigationHours, engineerUsdPerHour, downtimeUsdPerHour, downtimeHours, repeatIncidents, aiUsdPerInvestigation, platformUsdPerYear, downtimeCapture, runCostUsd.

`projection.csv`: seed, scenario, investigationHours, hypothesisDays, repeatRate, capaCycleDays, mttrDays, laborSavingsUsd, repeatScrapSavingsUsd, downtimeSavingsUsd, grossSavingsUsd, runCostUsd, netSavingsUsd, paybackMonths, downtimeHoursAvoided.

`sensitivity.csv`: seed, lever, value, grossSavingsUsd, netSavingsUsd, paybackMonths.

The label cell is `illustrative, synthetic`.
