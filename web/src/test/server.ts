// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { setupServer } from "msw/node";
import { handlers } from "../mocks/handlers";

export const server = setupServer(...handlers);
