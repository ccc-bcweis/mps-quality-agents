# Return on investment: slide content

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

Content for the two return-on-investment slides (K4), from the financial dataset in `data/roi/` (D6). The method and every formula are in `docs/roi-assumptions.md`. The deck itself is K1 and needs Bryan's sign-off. Every figure is **illustrative, synthetic**: one fictional year at a three-plant Meridian Precision Systems. Say so out loud. No figure is a forecast or a price.

The console shows the same numbers on the harness **Return on investment** panel, so the slide and the screen agree. These figures are the fictional year. They are not the hosted demo's Azure bill.

The hosted demo is a separate, smaller fact, for the speaker if someone asks what this build costs to leave running:

| Item | As built |
|---|---|
| Model | Azure OpenAI `gpt-5.4-mini` (`MODEL_MODE=azure`). The console shows a read-only model mode chip. There is no Replay/Live toggle for the answer engine. Offline mode costs $0. A live call times out at 20 seconds and fails over to saved answers |
| App spend cap | $2 per day in the gateway, separate from the Azure budget |
| Azure budget | `mpsdemo-monthly`, $40 a month, alerts at 80% of actual spend and 100% of the forecast. October spend is $4.13 month to date as of 2026-10-18 about 05:00 Eastern Time (Azure Cost Management, resource group rg-mps-demo) |
| Host | Azure Container Apps, East US 2, one replica, 0.25 vCPU and 0.5 GiB |
| Evidence the demo is real | 8 scenarios, SC-1 through SC-8. Replay eval: 29 cases, all passed on CI run 39110761511 (category accuracy, safe escalation, injection resistance, cause in the top 2, and citation precision all 100%). 1,144 automated tests passing. CI wall time 5 minutes 13 seconds |

## Slide 3 panel: what poor quality costs today

Title: Poor quality costs this fab group about $96M a year (illustrative).

| Figure | Value |
|---|---:|
| Revenue | $1.2B |
| Cost of poor quality, 8% of revenue | $96.0M |
| Scrap | $46.5M |
| Downtime, about 2,172 tool hours | $26.1M |
| Warranty and field cost | $20.5M |
| Investigation labor, 1,400 incidents × 22 hours × $95 | $2.9M |
| Time to a root-cause hypothesis | 4 days |
| Incident resolution time (MTTR-Q) | 9.5 days |
| Repeat-defect rate | 18% |
| CAPA cycle time | 62 days |

Speaker note: the labor line is small. The money is in scrap from repeat defects and in downtime while a cause is unknown, so faster, better-grounded hypotheses matter more than saved engineer hours.

## Slide 9: what the agents could return

Title: About $9.6M net a year in the expected case (illustrative).

| | Conservative | Expected |
|---|---:|---:|
| Engineer hours per investigation | 22 → 15.4 | 22 → 12.1 |
| Time to hypothesis | 4 → 2.0 days | 4 → 1.2 days |
| Repeat-defect rate | 18% → 15.3% | 18% → 13.5% |
| CAPA cycle time | 62 → 49.6 days | 62 → 40.3 days |
| MTTR-Q | 9.5 → 7.5 days | 9.5 → 6.7 days |
| Labor saved | $0.88M | $1.32M |
| Repeat scrap avoided | $1.26M | $2.09M |
| Downtime avoided | $4.56M (380 hours) | $6.38M (532 hours) |
| **Gross savings** | **$6.69M** | **$9.80M** |
| Cost to run ($4 per investigation + $180K platform) | $0.19M | $0.19M |
| **Net savings** | **$6.51M** | **$9.61M** |
| Payback | about 10 days | about 7 days |

Sensitivity, one lever at a time around the expected case (net savings):

| Lever | Low | Expected | High |
|---|---:|---:|---:|
| Investigation-hour reduction 30% / 45% / 60% | $9.17M | $9.61M | $10.05M |
| Hypothesis-time reduction 50% / 70% / 80% | $7.79M | $9.61M | $10.52M |
| Repeat-defect reduction 15% / 25% / 35% | $8.77M | $9.61M | $10.45M |
| Downtime capture 0.25 / 0.35 / 0.45 | $7.79M | $9.61M | $11.43M |

Speaker note: the result is most sensitive to how much downtime earlier detection really avoids. That is why the first 30 days measure the real baseline before any number goes in a business case.

Footer on both slides: Illustrative, synthetic. Seed 20261014. Source `data/roi/`.
