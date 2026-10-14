# Design questions (GB-4)

The console builds against the frozen contracts. None of these block the demo. Contract files were not changed.

## 1. SPC series are not on the API

Context: Tool Detail draws Western Electric charts. `contracts/openapi.yaml` has no series or telemetry route. SignalR `/hubs/fleet` is named and has no payload.

Options: Add a read-only series route later. Keep generating the demo series in the browser.

Recommendation: Keep the client series for the pitch. A later contract can replace `web/src/spc/series.ts` without changing the chart.

Blocking: no.

## 2. ROI has no endpoint

Context: PLAN §6.5 says the model lives in `data/roi/` as CSV plus a generator. There is no ROI route in the OpenAPI file. This lane may only write `web/`.

Options: Have the data lane publish `data/roi/` and point the screen at it. Leave the integer model in `web/src/roi/model.ts`.

Recommendation: Leave the client model until `data/roi/` exists. Shares of the COPQ remainder (50/20/30 scrap, warranty, downtime) and the downtime-hours lever (8% / 15%) are assumptions. The plan names the buckets and does not publish those percents. The screen labels every figure illustrative and synthetic. Expected savings are $7,599,195. Conservative savings are $4,368,075.

Blocking: no.

## 3. Investigations cannot be listed

Context: There is `GET /api/investigations/{id}` and no collection route. The shell links S1 by id. A tool page links E-02 and D-01 by id. `GET` also accepts the incident id.

Options: Add `GET /api/investigations`. Keep id links.

Recommendation: Keep id links for the demo.

Blocking: no.

## 4. Demo fleet disagrees with the single contract fixture

Context: The frozen handlers return one tool, E-02, Bay B, severity high, rule `westernElectric1`, ticket TKT-1001. The wireframes show eight tools, Bay A for etch, E-02 as WATCH / WE-2 inside 40–60 °C, and ticket numbers such as TKT-1044.

Options: Change the contract fixture to the wireframe fleet. Keep the contract fixture and overlay a demo fleet in `web/src/mocks/handlers.ts`.

Recommendation: Keep the overlay. S1 evidence text, hypothesis text, and ids match the contract fixture. Decision, CAPA, health, and metrics handlers are the frozen ones. The overlay is demo-only and is not a contract change.

Blocking: no.

## 5. CAPA read and advance are stubs

Context: `GET /api/capas` returns `[]`. `GET /api/capas/{id}` returns 404. `POST .../advance` returns 409 with title "Illegal CAPA transition." Approve returns capa id `66666666-6666-4666-8666-666666666666` and token `77777777-7777-4777-8777-777777777777`.

Options: Implement CAPA persistence in the API. Keep the decision result in session storage and show the 409 title.

Recommendation: Keep the session CAPA and the visible 409. The console does not call mock-enterprise, so there is no QMS 500 button.

Blocking: no.

## 6. A fault names one tool

Context: `FaultInjection` has one `toolId`. Scenario S4 needs both etch tools. `POST /api/faults` in the frozen handlers returns 202 and stores nothing.

Options: Allow a tool list on the fault. Post twice from the preset. Widen the handler so the new alert is on `GET /api/alerts`.

Recommendation: The preset posts twice. The web handler adds the alert, incident, and ticket. If a stateless handler answers, the injector polls for 10 seconds and then remembers a local alert so the Alerts page still updates.

Blocking: no.

## 7. There is no mode-switch route

Context: Metrics return `mode: replay`, spend 0, cap $2. Nothing in the REST contract switches live and replay.

Options: Add a mode route. Keep a local preview in `sessionStorage` key `mps.previewMode`.

Recommendation: Keep the local preview. Replay display spend is $0. The banner says when the preview differs from the gateway mode. A live inject with no recording flips the banner to MOCK REASONER.

Blocking: no.

## 8. The trace is derived

Context: The investigation trace (Rules, AI, Human) is built from evidence and hypotheses. It is not a contract resource.

Options: Add a trace schema. Keep the derived trace.

Recommendation: Keep the derived trace until an investigation payload includes one.

Blocking: no.
