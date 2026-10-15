# Entra sign-in for the hosted demo

Easy Auth fronts the single container. The sign-in app requires user assignment, so only invited, assigned users get in.

After the first deploy, an owner of the sign-in app runs:

```bash
deploy/entra-setup.sh <signin-app-id> <container-app-fqdn>
```

That script is idempotent. It sets:

- The Easy Auth callback `https://<fqdn>/.auth/login/aad/callback`.
- The identifier URI `api://<signin-app-id>` and a `user_impersonation` scope.
- A pre-authorization for the Azure CLI on that scope, so an assigned user can run `deploy/smoke-live.sh` with `az` tokens. Bicep lists `api://<signin-app-id>` as the second allowed audience.

The Azure CLI token is still bound to the signed-in user and to app assignment. Unassigned users cannot get a token.
