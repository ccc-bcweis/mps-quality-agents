// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_USE_MSW?: string;
  readonly VITE_API_BASE?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
