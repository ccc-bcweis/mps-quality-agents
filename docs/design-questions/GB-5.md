# Design questions

Contract files were not changed. Both questions are non-blocking. The tests follow the schema as the parsers see it today.

## 1. `DecisionResult.capa` nullability is invisible to OpenAPI 3.0 readers

**Context.** `contracts/openapi.yaml` defines `DecisionResult.capa` as a `$ref` to `Capa` with a sibling `nullable: true`. OpenAPI 3.0 ignores keywords beside `$ref`. `Microsoft.OpenApi.Readers` 1.6.31 therefore reports `Nullable == false` and keeps the reference id `Capa`. The property is not in `required` (only `investigation` is), so a response may omit `capa`. A JSON `null` is not expressed in the schema object the reader returns. Other nullable fields in the same document are typed properties, and those parsed with `Nullable == true`.

**Options.**

1. Leave the document. Callers treat a missing `capa` as "no CAPA". Generated clients will not know the property can be null.
2. Wrap the reference so `nullable` sits on the schema object, which 3.0 keeps:

```yaml
capa:
  nullable: true
  allOf:
    - $ref: "#/components/schemas/Capa"
```

3. Move the document to OpenAPI 3.1, where a `$ref` may have siblings and null is `type: [object, "null"]`. That touches every operation.

**Recommendation.** Option 2. It keeps OpenAPI 3.0 and makes null visible to generators. **Blocking? No.** Contract tests assert the reference id and that `capa` is not required. Integration tests allow the property to be absent or JSON null.

## 2. Approve description says a client signature is ignored

**Context.** `POST /api/investigations/{investigationId}/decisions` says "A client-supplied signature is ignored." `HumanDecisionRequest` is `additionalProperties: false` and has no `signature` property. A conforming server returns 400 for that extra field. "Ignored" can also be read as 200 with the field dropped and no effect on the token.

**Options.**

1. Change the sentence to say a client signature is not in the schema and the request is rejected.
2. Leave the prose. The schema remains the rule, and the integration test expects 400.

**Recommendation.** Option 1, wording only, when the next contract revision is open. **Blocking? No.** `ClientSuppliedSignatureIsRejected` expects 400 and no stored CAPA.
