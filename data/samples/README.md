# Bench printouts

Synthetic instrument printouts for the document-intake demo. All makers, tools, and readings are fictional.

Regenerate them with:

```
dotnet run --project tools/Printouts -- data/samples
```

The tool writes one PDF per instrument and a PNG scan of the thermocouple logger sheet. `manifest.json` records the file hash and the reading the mock model returns for a scan.

| File | Instrument | Maker |
|---|---|---|
| `oscilloscope-e01.pdf` | Oscilloscope | Northline Bench Instruments |
| `rf-power-meter-e02.pdf` | RF power meter | Calder Ridge Metrology |
| `particle-counter-d01.pdf` | Particle counter | Hearthline Particle Works |
| `thermocouple-logger-e01.pdf` | Thermocouple logger | Sable Fork Instruments |
| `thermocouple-logger-e01.png` | Same sheet, scanned | Sable Fork Instruments |
