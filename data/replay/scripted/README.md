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

A missing directory is an empty catalog. A hash that is not on disk is not answered from this folder. The gateway then uses the configured model mode. A live call that fails is not retried by the client; the gateway answers from saved answers. Do not invent a recording for a prompt that has not been captured.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.
