# Scripted replay recordings

Optional. `Mps:Model:ScriptedReplay` must be true before these files are served.

One file per prompt: `{scenarioId}/{promptHash}.json`. The hash is the gateway prompt hash (model id, messages, and tool names). The folder name is the scenario id from the injector catalog.

```json
{
  "modelId": "recorded",
  "text": "same words",
  "inputTokens": 4,
  "outputTokens": 4,
  "functionCalls": []
}
```

A missing directory is an empty catalog. A hash that is not on disk falls through to the live route. Do not invent a recording for a prompt that has not been captured. Live wording may still vary slightly when this folder is empty.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.
