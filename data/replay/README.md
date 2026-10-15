# Recorded bus runs

`scripted/` holds saved model answers. It is not a plant recording.

Each scenario folder, such as `SC-1`, holds `messages.jsonl`: the bus messages from one run of that scenario. Replay mode in the console reads these files. Live mode ignores them. Record a run with `tools/ReplayRecorder`.
