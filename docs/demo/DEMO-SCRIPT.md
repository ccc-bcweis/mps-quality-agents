# Live demo script: Meridian Precision Systems quality agents

Prepared by StatusNeo. About 25 minutes plus questions. Every company, plant, tool, supplier and record is fictional.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

How to read this script:

- **Click**: the exact control as labeled in the app.
- **Say**: your narrative.
- **They see**: what should appear on screen. If it doesn't appear, go to "If something breaks" at the end.

Control names were checked against the code on `main` on 2026-10-17.

---

## 0. Pre-demo checklist (15 minutes before)

| # | Click | Check |
|---|---|---|
| 1 | Open the hosted app → **Log in** on the landing page | Navy and gold presenter bar on top, charcoal console below |
| 2 | **Expand harness** (top right of the bar) | Controls row opens |
| 3 | **Hard reset** → **Confirm hard reset** | Wait until the progress note finishes. Production line: every tool **Nominal**. Alerts, Tickets, Investigation and CAPA lists are empty. Speed is back at 1x. Tool charts show 7 days of healthy history |
| 4 | **Speed** = **1x**, **Sample every** = **5 s** | 1x is real time: the plant clock matches the wall clock |
| 5 | Plant data: **Live** (not **Replay**) | Live follows the running plant. Replay shows a recorded scenario |
| 6 | AI engine chip (read-only): **Azure OpenAI**, **OpenAI** or **Offline (saved answers)** | Set at start by `MODEL_MODE`, not in the console. A live model costs a few cents a run; the gateway stops at $2 a day, or after repeated failures, and falls back to saved answers. The chip then says so |
| 7 | **Metrics** (presenter bar) | Spend today well under the $2 cap. Cost per success reads **n/a** before any run |
| 8 | Console header **Plant** = **All sites** | 32 tools across three plants |
| 9 | **Join links** → **Create and copy link** (if guests need access) → **Close** | Paste the link in the meeting chat. **Copy** recopies an existing link |
| 10 | Put `thermocouple-logger-e01.png` (from `data/samples/`) on the desktop | For the printout moment |
| 11 | Scenario picker: leave on **SC-4 · Slow chamber seasoning drift** | First walkthrough |

Do not open **What the AI should find** on the scenario card while sharing. It holds the answer.

---

## 1. Two layers on screen (1 min)

**Click:** nothing.

**Say:** "Two things on screen. The navy and gold bar at the top is my presenter control panel. I use it to trigger events, change speed and reset. Everything below it is Meridian's console, the screens your engineers would use every day: Sites, Production line, Tool, Alerts, Tickets, Investigation, CAPA, and Rules & Process. The decision on each investigation is made on the Investigation page itself."

**They see:** the presenter bar and the console tabs.

---

## 2. Opening: who Meridian is (2 min, deck slides 1-3)

**Say:** "Meridian runs three plants: Northwater Fab, Ironwood Fab and Lumen Pilot Works. When a tool drifts today, engineers spend hours pulling data from five systems (MES, QMS, PLM, ERP and maintenance) before anyone can name a cause. Our goal is minutes, with every claim pointing at a record, and a human approving every action."

---

## 3. Sites and the Production line (3 min)

1. **Click:** **Sites**.
   - **They see:** the plant buttons **All sites**, **Northwater Fab**, **Ironwood Fab**, **Lumen Pilot Works**, a throughput card for each plant, and the floor plan with named bays ("Cleanroom bay 1", "Pilot line bay 3").
   - **Say:** "This is the fab floor: bays, tools, and how wafers move between them."
2. **Click:** **Ironwood Fab**.
   - **They see:** the header **Plant** switcher changes to Ironwood. Production line, Alerts and Tickets now show only Ironwood.
   - **Click:** **Plant** → **All sites** to switch back.
3. **Click:** **Production line**.
   - **They see:** "Production line overview" and the **Open alerts (0)** button. Tiles are grouped by bay, and each tile shows a status of **Nominal**, **Watch** or **Alarm** (shown in capitals on the tile) with left-aligned sensor readings.
   - **Say:** "One status per tool: the worst alert still active on that tool. The tile, the Sites map, the tool page and the alerts list all read that one value, so they can never disagree."
4. **Click:** any tile, for example **E-01**.
   - **They see:** the tool page with the chamber schematic and charts covering the last 7 days.
   - **Say:** "Two kinds of signal live here. Process readings, like gas flow or temperature, have control limits and SPC rules. Counters, like RF-hours or wafers run, only go up. We chart them but never alarm on them, because a rising count is normal."

---

## 4. Scenario SC-4: slow seasoning drift (process drift, 4 min)

**Story:** after a chamber clean, E-01's chamber walls slowly collect film, and the etch rate falls although nobody changed the recipe.

1. **Click:** scenario picker → **SC-4 · Slow chamber seasoning drift** → **Run scenario**.
   - **Say:** "I've started the event. From here, nothing I do touches the outcome. The plant streams, and the rules decide."
2. **Click:** **Speed** → **10x**, then **Tool** → E-01 (or the E-01 tile). The plant clock stays real time; 10x only makes the slow drift develop faster.
   - **They see:** within about 15 seconds the E-01 tile turns **Watch** (a run of readings below the centre line). After about a minute more rules join and the tile goes to **Alarm**. By about 2 minutes the etch rate on chamber A crosses the lower control limit. At 1x the first alert takes about 4 minutes and the ramp about 20.
   - **Click:** **Speed** → **1x** once the alert is up.
3. **Click:** **Alerts**.
   - **They see:** one row per active alert: status, tool, time, the rule in plain words, the ticket button (for example **TKT-1001**) and **Open**. No evidence on this list.
   - **Say:** "The rules engine raised this from the live stream. It's deterministic code, not AI. A ticket opened automatically."
4. **Click:** **Open** on the E-01 row.
   - **They see:** the alert detail, with the rule, the parameter, and the evidence for this one alert.
5. **Click:** **Investigation** → **Open** on E-01.
   - **They see:** **Agent steps** (each step with a one-line summary and its duration), the **Evidence pack** ("N signals from M records"; expand a row with **Show the cause for …**), and **Hypotheses**.
   - The ranked cause should be **Process drift**: chamber seasoning after the clean (work order WO-CLN-E01-220). The investigation says what it ruled out: no engineering change, no overdue calibration, no bad lot.
   - **Say:** "The agent didn't jump to the most dramatic answer. It checked for a change, a calibration and a lot, found none, and recommends a chamber clean, not a CAPA."

**Click:** **Stop scenario** before the next walkthrough. The fault stops; history stays.

---

## 5. Scenario SC-3: one bad gas lot, two tools (supply chain, 4 min)

**Story:** E-01 and E-02 share one bottle of CF4 process gas. Lot PL-2044 arrived out of specification.

1. **Click:** **Soft reset** → **Confirm soft reset**. Open work closes and history stays. (Use **Hard reset** if you want a clean slate.)
2. **Click:** scenario picker → **SC-3 · Bad precursor gas lot** → **Run scenario**.
3. **Click:** **Production line**. Wait about 1 minute.
   - **They see:** **E-01** and **E-02** both go to **Alarm**. The button reads **Open alerts (2)**.
4. **Click:** **Open alerts (2)**.
   - **They see:** two rows, E-01 (for example **TKT-1001**) and E-02 (**TKT-1002**). The count matches the Production line.
   - **Say:** "Two tools, two alerts, two tickets. Each one is tracked separately, even though they share a cause."
5. **Click:** **Investigation** → **Open** on either tool.
   - **They see:** the ranked cause **Supply chain material**: lot PL-2044 went into service on both tools before the flow shift, and supplier notice SN-PL-2044 says it is out of spec. Other records in the evidence (work orders, a second lot CF4-014) are listed and ruled out.
   - **Say:** "The two-tool pattern is the clue. A maintenance event explains one tool; a shared lot explains both."

Optional cross-plant variant: **SC-5 · Precursor lot on two plants**. Deposition tools at Northwater and Ironwood step down together, and the cause is lot MX4419.

---

## 6. Full lifecycle: approval through CAPA close (5 min)

Continue from SC-3 (or SC-1 · Edge ring revision C on E-02 if you prefer an engineering-change story).

1. **Click:** **Investigation** → **Open** on the investigation, then scroll to the **Decision** section.
   - **They see:** **Decision · Round 1**, **Recommended action** (one containment and one preventive step), and the buttons **Approve CAPA**, **Request more evidence** and **Reject**. The approver is the signed-in user.
   - **Say:** "The agent can recommend. It can't act. Only a person opens a CAPA."
2. Type a short note in **Note or justification**, then **Click:** **Approve CAPA**. In the **Confirm approval** dialog, type the confirmation word it asks for, then **Approve and create CAPA**.
   - **They see:** the decision record, read-only, such as "Approved by Bryan Weis at 9:42 AM" with the note. The buttons are gone: the decision for this round is final. No tokens or hashes on screen.
3. **Click:** **CAPA** → the new corrective action.
   - **They see:** the stages **Open → Containment → Root cause confirmed → Corrective action → Verification → Closed**, and the **Advance stage** button.
4. **Click:** **Advance stage** once, to **Containment**.
   - **They see:** "Moved to Containment." Once the fault is contained, the tool's alert clears and the tile goes back to **Nominal**. An alert also clears on its own after 8 normal readings.
   - **Say:** "Containment is the moment the plant is safe again, and the status follows automatically."
5. **Click:** **Advance stage** through **Root cause confirmed**, **Corrective action**, **Verification**, **Closed**.
   - **Say:** "Stages move one step at a time. You can't skip from Open to Closed, and you can't move backward. The system refuses both."
6. **Click:** **Audit log** (presenter bar).
   - **They see:** every step: alert, ticket, agent steps, approval and each CAPA stage, with who did it and when.

**Escalation and Add evidence (talk track, or run SC-2 · ESC thermocouple calibration drift):** if evidence conflicts or is missing, the round stops at **Waiting for more evidence**. **Approve** is not offered. The engineer has two choices:

1. **Add evidence:** type what you know in **Evidence to add** (for example "Checked on the floor: the thermocouple on chamber A was replaced on Tuesday and not recalibrated"), and optionally attach a bench printout. **Click:** **Add evidence and run round 2**.
   - **They see:** the page moves to **Round 2**. **Rounds on this ticket** shows Round 1 locked, "Closed: more evidence added", with the note, who added it and when. The evidence pack has a row marked **User-provided: Bryan Weis**. The agents run again through the same evidence gate and checks, and Round 2 either recommends or waits for more evidence again.
   - **Say:** "The engineer's knowledge goes in as evidence with a name and a time on it, never as an instruction. Every round is kept. A ticket gets at most five rounds."
2. **Override with justification:** a written reason of at least 20 characters, kept in the audit log.

**Reject** on any round closes the ticket with no action. The audit log shows each added note ("User-provided evidence added by … to round 1; the agents ran again as round 2").

---

## 7. A bench printout becomes a reading (3 min)

**Story:** a technician measured ESC temperature on E-01 with a bench thermocouple logger and printed it.

1. **Click:** **Tickets** → **Upload telemetry** → **Printout** → choose `thermocouple-logger-e01.png`. (The presenter bar also has **Sample printout** for the same flow.)
2. **They see:** the fields the app read from the printout: Tool E-01, Chamber A, Parameter ESC temperature, Value 68 C, Spec minimum 40, Spec maximum 60, Instrument, Operator initials, and **Observed at** with the printed time.
3. **Click:** **Now** next to **Observed at**.
   - **Say:** "The printed time is days old. **Now** sets the reading to the plant clock. The printed time stays on the record for audit."
4. **Click:** **Confirm reading**.
5. **They see:**
   - The reading goes in as one ordinary sample, tagged **Manual**, in the same series as the sensor.
   - 68 C is above the limit, so the normal rule fires: E-01 goes to **Alarm**, a ticket opens, and the investigation starts on its own.
   - The tile shows 68 C with a Manual badge, and the chart shows a **Manual** marker.
   - **Say:** "No special path for paper. The printout is treated like any other reading, and the same rules decide."
6. **Contrast:** upload again with an in-spec value (for example 50 C, set in the Value field before **Confirm reading**). It's stored and charted, but it opens no ticket.
7. **After containment** (section 6, step 4) or 8 normal readings, the alert clears. The tile returns to the live sensor, and the marker reads "resolved". The printout stays in the evidence and the audit log.

Also under **Tickets → Operator report**: pick a **Tool**. The **Parameter** list shows only that tool's real sensors, with units and spec ranges. Then **Open ticket**.

(SC-6 · Oscilloscope RF oscillation on E-01 is the same idea with an oscilloscope printout: `oscilloscope-e01.pdf`.)

---

## 8. Value and close (3 min)

1. **Click:** **Return on investment** (presenter bar).
   - **Say:** "Illustrative figures, with the assumptions stated."
2. **Click:** **Metrics**.
   - **They see:** completion rate and grounding score, category accuracy (from 27 graded test cases that run on every build), cost per successful investigation (n/a until one succeeds), spend today against the $2 cap.
3. **Click:** **Rules & Process**.
   - **They see:** the thresholds and settings, each business rule's conditions and actions, and the **Process map**: readings → rules check → alert → ticket → investigation → decision → CAPA → containment clears the alarm → close, with its numbers filled from the settings.
   - **Say:** "Every number the system acts on is on this page, in plain words. Nothing is buried in code."
4. Deck: architecture, security, 90-day plan, close.

---

## Likely questions

| Question | Answer |
|---|---|
| Can the AI change the plant or open a CAPA by itself? | No. The agents only have read-only tools. A CAPA is created only after a named person approves, and the approval is single-use and recorded. |
| What if the evidence conflicts? | The round waits for more evidence. Approve is not offered. The engineer adds what they know (the agents run another round with it, up to five) or overrides with a written reason, which goes in the audit log. |
| How do you stop hallucinated causes? | Every claim must cite a record in the evidence pack. A grounding check rejects uncited claims, and graded test cases run on every build. |
| Who decides when a tool is in alarm, the AI? | No. Deterministic SPC rules (Western Electric) on the live stream. The AI explains after the rules fire. |
| Why does a counter never alarm? | A counter, like RF-hours, only rises. That's normal, so it's charted without control limits. |
| What does a printout do? | It becomes one ordinary reading in the same series. It alarms only if it breaks a limit. |
| Two tools alarm from one cause. Is that one alert? | Two: one per tool, each with its own ticket. The investigation links them through the shared cause. |
| What does it cost? | Cents per investigation, with a hard $2-a-day cap in this environment. Over the cap it switches to saved answers, and the plant data is unchanged. |
| Which model? | Azure OpenAI in Meridian's own subscription, through a gateway with cost limits, managed identity and no keys in code. |
| What changes for production? | Each component swaps for its production service: Service Bus for the queue, Azure SQL for storage, Azure AI Search for retrieval, Front Door with WAF and private endpoints. The agents and workflow stay the same. |
| How do we connect our systems? | Read-only connectors to MES, QMS, PLM, ERP and maintenance. One fab first, in 90 days, with eval and security gates before production. |
| Is the data real? | No. Fictional plants and records, generated from a fixed seed, so every run is reproducible. |

---

## If something breaks

| Symptom | Recovery |
|---|---|
| Nothing moves after **Run scenario** | Check **Speed** is 1x or higher and Plant data is **Live**. Wait 30 seconds. Then **Stop scenario**, **Soft reset**, and run again |
| Need it faster | **Speed** → **2x**, **5x** or **10x**. The plant clock stays real time; only the fault develops faster. Set it back to **1x** before the next step |
| Wrong scenario running | **Stop scenario**, pick the right one, **Run scenario** |
| Leftover alerts or tickets from a rehearsal | **Hard reset** → **Confirm hard reset**; wait for it to finish (about 30 seconds) |
| Investigation slow, or the live model errors | The gateway falls back to saved answers by itself after 2 failures (20 s timeout each) and the chip says so; re-run the scenario. Check the speed is 10x (at 1x the agents wait about 5 minutes for readings). Plant data is unchanged |
| Spend meter near $2 | Same: switch to Replay |
| A tab looks empty | Header **Plant** → **All sites**; that list may have no records for the selected plant |
| Printout read a field wrong | Edit the field before **Confirm reading** |
| A guest can't get in | **Join links** → **Create and copy link** → send the new link |
| Page looks stale | Browser refresh. State lives on the server, so nothing is lost |
| Everything is wrong | **Hard reset**, then restart from section 3. Talk through the deck while it resets |
