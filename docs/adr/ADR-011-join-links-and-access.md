# ADR-011: One role, Microsoft sign-in for anyone, and join links

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

**Status:** Accepted, 2026-10-15 (Baseline v2). Amended 2026-10-19. **Decided by:** Bryan Weis, recorded by the architect.

## Context
Bryan wants to invite people outside the Common Consulting tenant to the hosted demo. They should sign in with any Microsoft account (work, school, or personal), and every user should have full rights, as both engineer and approver. Only Bryan (bcweis@commonconsulting.us) manages invitations. Shared secrets are not allowed: no client secrets and no Graph app secrets.

## Options
1. **Multi-tenant plus personal accounts on the existing app registration, gated by an app-side allowlist.** Easy Auth uses the `common` issuer, and the API admits home-tenant users and anyone who redeemed a join link. This needs no secret: Easy Auth already uses the ID-token flow and the API uses its managed identity. It needs no tenant admin consent and no new Graph permissions.
2. **B2B guest invitations through Microsoft Graph.** A join link would call `POST /invitations` and assign the app role. The managed identity would need `User.Invite.All` and `AppRoleAssignment.ReadWrite.All` application permissions, which need tenant admin consent, and every guest would become a directory object in Bryan's tenant.

## Decision
Option 1.
- The app registration's `signInAudience` is `AzureADandPersonalMicrosoftAccount`, and the Easy Auth issuer is `https://login.microsoftonline.com/common/v2.0`. Container Apps has no "allowed tenants" setting, so the API does the gating.
- `AccessGate` middleware (hosted mode only) lets a request reach `/api` and `/hubs` when the caller is in the home tenant (`Mps:Auth:HomeTenantId`, which Entra still gates with "assignment required"), is an admin, or is a member who redeemed a join link. Anyone else gets a 403 that says to ask for a join link, and the console shows a "Not part of this demo yet" page.
- **Join links** are random 192-bit tokens with an expiry (1 minute to 30 days) and a maximum number of uses (1 to 500). The admin creates, copies, lists, revokes, reactivates, and restores them at `/admin/members`. Reactivate only clears a revoke. Restore sets a new expiry and use limit, resets the use count, and keeps the same token. A token that is still usable is rejected. A token the store has never seen becomes a new link. A member who redeems again does not spend a use. A revoked member is asked for a join link and can join again only by redeeming a usable link. Members are keyed by tenant id and object id. A member row keeps the join link, the joined time in UTC, email, display name, tenant id, object id, status (`active` or `revoked`), and the revoked time.
- **One role.** Every admitted user can run every quality command and approve. The approver is always the signed-in user's display name, and the API ignores any approver name the client sends in hosted mode. Harness system commands (resets, data mode, speed, join links) stay admin-only. The admin is the Entra app role `admin` or an email in `Mps:Auth:AdminEmails` (default `bcweis@commonconsulting.us`).

## Consequences
- External work and school users sign in through their own tenant. Organisations that block user consent to third-party apps will stop them at consent. Personal Microsoft accounts are unaffected.
- Outside Azure, links and members live in the local SQLite file, outside the quality schema, so a reset keeps them. In Azure they live in Table storage. The storage account turns shared key access off. The app's user-assigned identity has Storage Table Data Contributor on that account. The app is given the table endpoint only. A container restart or a deploy keeps those rows. Quality data in SQLite still starts again from seed 4242. `Mps:Access:Store` is `Sqlite` or `AzureTable`.
- Tokens are stored as written so the admin can copy a link again and so a pasted token restores that same link. They grant only membership in this workspace, which is an acceptable trade.
