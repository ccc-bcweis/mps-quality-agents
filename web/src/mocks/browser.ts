// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { setupWorker } from "msw/browser";
import { handlers } from "./handlers";

export const worker = setupWorker(...handlers);
