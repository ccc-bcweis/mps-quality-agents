# Bench printouts

Synthetic instrument printouts for the document-intake demo. All makers, tools, and readings are fictional.

Regenerate them with:

```
dotnet run --project tools/Printouts -- data/samples
```

The tool writes one PDF per instrument and a PNG scan of the thermocouple logger sheet. `manifest.json` records the file hash and the reading the mock model returns for a scan.

Each sheet has an instrument header, serial number, report and calibration dates, a trace graphic with the spec limits, a reading table, and a signature line. Every reading is out of spec.

| File | Instrument | Tool / chamber | Reading | Spec |
|---|---|---|---|---|
| `oscilloscope-e01.pdf` | Oscilloscope | E-01 A, RF power | 2140 W | 1000–2000 W |
| `rf-power-meter-e02.pdf` | RF power meter | E-02 A, bias RF power | 510 W | 200–500 W |
| `particle-counter-d01.pdf` | Particle counter | D-01 A, particle adders | 18 | 0–10 |
| `thermocouple-logger-e01.pdf` | Thermocouple logger | E-01 A, ESC temperature | 68 °C | 40–60 °C |
| `thermocouple-logger-e01.png` | Same sheet, scanned | E-01 A, ESC temperature | 68 °C | 40–60 °C |
