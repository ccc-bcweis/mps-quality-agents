# Architecture drafts, Baseline v2 (Deliverable 03)

Editable drafts of the logical architecture, the physical architecture and the data model (entity-relationship style). They show the target design; `*` marks items the running demo implements in a thinner form (see the notes band on each sheet).

| Sheet | SVG (editable text) | draw.io | PNG | ARCH D PDF |
|---|---|---|---|---|
| Logical | logical-archD.svg | logical.drawio | logical-archD.png | logical-archD.pdf |
| Physical | physical-archD.svg | physical.drawio | physical-archD.png | physical-archD.pdf |
| Data model | data-model-archD.svg | data-model.drawio | data-model-archD.png | data-model-archD.pdf |

`architecture-v2-drafts.pptx` holds all three as native, editable PowerPoint shapes. `overlap-check.json` is the automated check (box overlap, text fit, edge-through-box, band intrusion); all empty = clean.
Regenerate: edit `model.py`, then `bash build.sh` (needs Python Pillow, python-pptx, Chrome, IBM Plex fonts in ../fonts).
The model label is gpt-5.4-mini (the old gpt-4o label is retired). The running-demo notes say one replica on the aspnet 10.0 image, a 20-second failover to saved answers, a rolling window of 600 readings per sensor, and a manual workflow_dispatch deploy. A merge does not deploy. CauseGuard may mention a ruled-out record and must not cite it as support. A refused answer is kept on escalation. Recipe-step limits follow the step at each reading.
