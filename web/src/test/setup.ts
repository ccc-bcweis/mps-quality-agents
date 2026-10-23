// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { cleanup } from "@testing-library/react";
import "@testing-library/jest-dom/vitest";
import { afterAll, afterEach, beforeAll, beforeEach } from "vitest";
import { setHubConnected } from "../live/fleetHub";
import { setCatalog } from "../labels";
import { resetMockState } from "../mocks/state";
import { storeRunning } from "../components/harnessSession";
import catalogFixture from "./catalog.fixture.json";
import { server } from "./server";

class ResizeObserverStub {
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
}

globalThis.ResizeObserver = ResizeObserverStub;

// Vitest 4 runs jsdom tests on Node's fetch. Undici does not accept a jsdom File, so a
// FormData upload is written as an empty part named "blob" and the filename never arrives
// (vitest-dev/vitest#9135, #9260). jsdom's File has no arrayBuffer(); FileReader does.
// Encode the body after MSW patches fetch so the multipart part keeps the name and bytes.
function readBlob(blob: Blob): Promise<Uint8Array> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      if (!(reader.result instanceof ArrayBuffer)) {
        reject(new Error("expected an array buffer"));
        return;
      }
      resolve(new Uint8Array(reader.result));
    };
    reader.onerror = () => reject(reader.error ?? new Error("The file could not be read."));
    reader.readAsArrayBuffer(blob);
  });
}

function headerName(value: string): string {
  return value.replace(/[\r\n"]/g, "");
}

async function encodeFormData(form: FormData): Promise<{ body: Uint8Array; contentType: string }> {
  const boundary = `----mps${crypto.randomUUID()}`;
  const encoder = new TextEncoder();
  const chunks: Uint8Array[] = [];
  const push = (text: string) => chunks.push(encoder.encode(text));
  for (const [key, value] of form.entries()) {
    push(`--${boundary}\r\n`);
    if (typeof value === "string") {
      push(`Content-Disposition: form-data; name="${headerName(key)}"\r\n\r\n${value}\r\n`);
      continue;
    }
    const type = value.type || "application/octet-stream";
    push(
      `Content-Disposition: form-data; name="${headerName(key)}"; filename="${headerName(value.name)}"\r\nContent-Type: ${headerName(type)}\r\n\r\n`,
    );
    chunks.push(await readBlob(value));
    push("\r\n");
  }
  push(`--${boundary}--\r\n`);
  const body = new Uint8Array(chunks.reduce((sum, chunk) => sum + chunk.byteLength, 0));
  let offset = 0;
  for (const chunk of chunks) {
    body.set(chunk, offset);
    offset += chunk.byteLength;
  }
  return { body, contentType: `multipart/form-data; boundary=${boundary}` };
}

function installFormDataFetch(): void {
  const patched = globalThis.fetch;
  globalThis.fetch = async (input: RequestInfo | URL, init?: RequestInit) => {
    if (!(init?.body instanceof FormData)) return patched(input, init);
    const encoded = await encodeFormData(init.body);
    const headers = new Headers(init.headers);
    headers.set("content-type", encoded.contentType);
    // BodyInit accepts ArrayBuffer. A Uint8Array view is not assignable under the DOM lib.
    const body = new ArrayBuffer(encoded.body.byteLength);
    new Uint8Array(body).set(encoded.body);
    return patched(input, { ...init, body, headers });
  };
}

beforeAll(() => {
  server.listen({ onUnhandledRequest: "error" });
  installFormDataFetch();
});
beforeEach(() => {
  // The server catalog (labels, limits, rule sentences), as served by /api/metadata/catalog. ConsoleCatalogTests keeps it equal.
  setCatalog(catalogFixture);
  if (typeof sessionStorage !== "undefined") sessionStorage.clear();
  if (typeof localStorage !== "undefined") {
    localStorage.removeItem("mps.selectedSite");
    localStorage.removeItem("mps.harness");
  }
  resetMockState();
  storeRunning("");
});
afterEach(() => {
  cleanup();
  setHubConnected(false);
  server.resetHandlers();
});
afterAll(() => server.close());
