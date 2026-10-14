---
id: fmea-etch-esc
title: FMEA for MPS-E7 ESC temperature
toolFamily: Etch
revision: C
kind: fmea
untrusted: false
---
# FMEA for MPS-E7 ESC temperature

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

## Oscillation

Failure mode: ESC temperature oscillation on one chamber during oxide etch. One cause to review is an edge ring change. A preventive maintenance visit in the same week is a common confounder and needs its own timestamp.

## Drift

Failure mode: etch rate drifting for weeks after a clean. Review RF hours since the last clean before citing a single lot or a single calibration record.

Severity for a one-chamber oscillation is high when the trace leaves the recipe window. Detection uses the chamber-to-chamber comparison.
