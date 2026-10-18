# Recorded bus runs

`scripted/` holds optional saved model answers (`Mps:Model:ScriptedReplay`). It is not a plant recording.

Each scenario folder, such as `SC-1`, holds `messages.jsonl`: the bus messages from one run of that scenario. The harness can show that recording instead of the running plant. That plant view does not choose the model. The model setting is `Mps:Model:Mode` (`MODEL_MODE`): offline, openai, or azure. `LLM_MODE` is only an old alias. Record a run with `tools/ReplayRecorder`.
