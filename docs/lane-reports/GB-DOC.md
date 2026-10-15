# Lane report: document intake

Branch `lane/doc`. Local commits only. Nothing was pushed. Issue #110.

| Slice | Commit | What landed |
|---|---|---|
| Sample printouts and the text parser | `866872e` | Four synthetic PDFs, one PNG scan, `tools/Printouts`, and PdfPig text extraction. |
| Confirm as evidence and a fault | `8d8b527` | A confirmed reading is evidence with source manual measurement, linked to the tool and the incident. Out of spec also injects a fault. |
| Inspect and confirm API | `d9777d9` | `POST /api/printouts/inspect` and `POST /api/printouts/confirm`. Contract 1.3.0. Text first, then the model gateway for a scan. Mock mode is keyed by the file hash. |
| Scenario SC-6 | `9387c99` | The oscilloscope sheet on E-01. Confirm applies one oscillation. The catalog signal is not applied again. |
| Tickets panels | `1bf3c04` | Upload telemetry sits beside Operator report. The parameter list is that tool's sensor channels. |
| Browser check | `6c9c9ef` | Playwright uploads the oscilloscope sample, confirms it, and shows the alert. Labels are checked at 1280, 1600, and 1920. |

## How it was verified

- `dotnet format MPS.sln` exited 0 and did not change any source file.
- `dotnet build MPS.sln` exited 0 with 0 warnings.
- `dotnet test MPS.sln --no-build` failed one glossary check because a decision-record status line used a lane tag that is not a glossary term. The wording was changed, and `dotnet test tests/Simulator.Tests --filter GlossaryLoadsEveryRequiredTerm` then passed. The first full run, aside from that one failure, was:
  - Application.Tests 178, Domain.Tests 19, Agents.Tests 78, ModelGateway.Tests 15, Connectors.Tests 13, MockEnterprise.Tests 4, Orchestration.Tests 15, Architecture.Tests 14, Contract.Tests 39, Api.Tests 29.
  - Simulator.Tests was 56 passed and 1 failed on that run.
  - Api.IntegrationTests reported no matches for the existing `Category!=PendingLane` filter.
- From `web/`: `npm test` (31 files, 80 tests) and `npx tsc --noEmit` exited 0. Vitest ignores `e2e/**` so the Playwright spec is not collected as a unit test.
- `npx playwright test` in `web/` passed 1 test on system Edge. It covered 1280, 1600, and 1920.

## Behavior Bob should expect

- Tickets shows Operator report and Upload telemetry side by side from 1280 pixels up. There is no new tab or route.
- After a tool is chosen, the parameter list is only that tool's sensor channels. The label is the plain name, the unit, and the sensor spec. Etch ESC temperature is 40-60 C. A channel with no catalog parameter uses the schematic name, such as Run rate.
- A PDF is read as text. A PNG, or a PDF with no text, goes through the model gateway. Mock mode returns the sample reading for that file hash. An unknown file is an error.
- The engineer can correct the fields, then confirm. The sheet is stored as evidence and cited on an investigation for that tool. An in-spec reading does not move the simulation. An out-of-spec reading injects a fault and raises an alert named "Manual measurement outside the printed spec".
- A printed parameter `rfPower` on an etch tool (E-01, or an Ironwood id that contains `-E-`) is applied as `rfSourcePower`. Deposition stays `rfPower`. ADR-007 records why.
- SC-6 runs the oscilloscope printout. The tool shows one oscillation on source RF power.
- Demo reset clears the in-memory measurement log.

## Open questions for Bob

- The browser test uses the dev server and the browser mocks, with Edge already installed on this machine. The API tests cover the real parser, the evidence row, and the fault.
- The unit-test page stringifies form uploads, so that test cannot see the file name. The browser test uploads `data/samples/oscilloscope-e01.pdf` and does.
- `docs/SPEC.md` already described SC-5. This lane added SC-6 and did not change SC-5. The scripted catalog in this worktree is SC-1, SC-2, SC-3, SC-4, and SC-6.
