# Entra sign-in for the hosted demo

Easy Auth fronts the single container. The sign-in app requires user assignment, so only invited, assigned users get in.

After the first deploy, an owner of the sign-in app runs:

```bash
deploy/entra-setup.sh <signin-app-id> <container-app-fqdn> [admin-upn]
```

That script is idempotent. It sets:

- The Easy Auth callback `https://<fqdn>/.auth/login/aad/callback`.
- The identifier URI `api://<signin-app-id>` and a `user_impersonation` scope.
- A pre-authorization for the Azure CLI on that scope, so an assigned user can run `deploy/smoke-live.sh` with `az` tokens. Bicep lists `api://<signin-app-id>` as the second allowed audience.

The Azure CLI token is still bound to the signed-in user and to app assignment. Unassigned users cannot get a token.

The script also creates the `admin` app role and assigns it to `admin-upn`. Easy Auth passes app roles in the principal header, and `POST /api/admin/reset` requires `admin`. A user must sign out and back in to pick up a new role.

## Sign out

The platform allows anonymous requests (`unauthenticatedClientAction: AllowAnonymous`) and the app enforces sign-in: `/api` and `/hubs` answer 401 without an Easy Auth principal (`AccessGate`), and a console page without one redirects to the branded `/landing.html` (`ConsoleEntryGate`; console HTML is sent `no-store`). Its Log in button goes to `/.auth/login/aad?prompt=select_account&post_login_redirect_uri=/`, so Entra always shows the account picker. A signed-in account without access lands on `/landing.html?noAccess=1` ("Signed in as X, which has no access" and "Use a different account"); join links still open. The signed-in name in the console header opens a menu with Sign out, which calls `/.auth/logout?post_logout_redirect_uri=/landing.html?signedOut=1`. No new app registration, secret or redirect URI is needed: the login callback is the existing `/.auth/login/aad/callback`.
