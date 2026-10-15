# Enterprise seed

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

`enterprise.json` is synthetic MES, QMS, PLM, ERP, and CMMS history for seed `20261014`. `tools/DataGen` regenerates it from `data/fleet` and `data/faults`.

`materialUses` tells the mock which tools consumed a material lot. The ERP response does not include that field. Runs, lot references, and material uses carry a `siteId`. A material lot itself has no single site, because one lot can be consumed at more than one plant. Root-cause labels live only in `data/groundtruth` and are not in this file.

All company names, lots, and measurements are fictional. Values are illustrative.
