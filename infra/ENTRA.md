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

The signed-in name in the console header opens a menu with Sign out. It calls the platform `/.auth/logout?post_logout_redirect_uri=/signed-out.html`. The splash page and its logo are excluded from the platform sign-in (`globalValidation.excludedPaths`), and its Sign in button goes to `/.auth/login/aad?post_login_redirect_uri=/`. No new app registration, secret or redirect URI is needed: both paths are served by the existing Easy Auth configuration.
