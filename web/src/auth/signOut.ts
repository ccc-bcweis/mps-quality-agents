// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { AdminSession } from "../api/types";

/** Static splash page served without sign-in (excluded from the platform sign-in in infra/modules/container-apps.bicep). */
export const SIGNED_OUT_PAGE = "/signed-out.html";

/** Hosted: the platform sign-out clears the session, then lands on the splash. Local runs have no platform sign-in. */
export function signOutUrl(mode: AdminSession["mode"] | undefined): string {
  return mode === "azure" ? `/.auth/logout?post_logout_redirect_uri=${encodeURIComponent(SIGNED_OUT_PAGE)}` : SIGNED_OUT_PAGE;
}
