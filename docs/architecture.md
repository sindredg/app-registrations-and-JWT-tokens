# Architecture and how it actually works

This is my mental model of what I built and how the same pattern works in the real world. Writing it down so future me can rebuild the picture fast.

## The three identities and how they talk

I built three app identities around one protected API.

```
                        Microsoft Entra ID (my tenant)
                        issues and signs all tokens
                                   |
        +--------------------------+--------------------------+
        |                          |                          |
   Employees Client          Employees Daemon           Employees API
   (interactive web app)     (background job)           (the resource)
        |                          |                          |
   signs in a user           uses its own secret        validates the token,
   gets a DELEGATED token    gets an APP-ONLY token      reads scp and roles,
        |                          |                     allows or denies
        +-----------> calls /surveys, /employees/all <---+
```

The flow in words:

1. A client needs to call the API, so first it needs a token from Entra.
2. The Client signs a user in and asks for a delegated token. The Daemon just presents its own secret and asks for an app only token. Either way Entra is the only thing that issues and signs tokens.
3. Entra looks at what the app is allowed to do (consented permissions) and, for a user, what that user is allowed to do (group and role assignments), and bakes the answer into the token as claims.
4. The client sends the token to the API on every request.
5. The API validates the signature, checks the audience is itself, then reads the `scp` and `roles` claims and decides allow or deny. The API never calls Entra on the hot path, it just trusts the signed token.

The one line that ties it all together: the API makes its decision purely from claims in a signed token. Everything I configured in the portal only mattered because it changed what ended up in those claims.

## Delegated versus application, the core split

| | Delegated (Employees Client) | Application / app only (Employees Daemon) |
|---|---|---|
| Is there a user? | Yes, someone signed in | No, runs headless |
| Acts as | The signed in user | Itself |
| Permission ceiling | Never more than the user | Full privilege of the granted permission |
| Claim in the token | `scp` (space separated scopes) | `roles` (app roles) |
| Who can consent | User or admin, depending on the scope | Admin only, always |
| Real world example | HR person using the portal | Nightly payroll sync job |

This is why the two tokens I captured look so different. The user token had `scp` plus a user identity plus a `roles` claim from the group. The daemon token had only `roles`, no `scp`, and no user at all.

## The separation I keep needing to remember: app registration vs enterprise application

This tripped me up, so here it is clearly.

- **App registration** is the blueprint. It is the global definition of the app: its client ID, its redirect URIs, its credentials, the scopes it exposes, the app roles it defines, and the API permissions it requests. There is one registration, and it lives in the tenant that owns the app. In the portal it is under **App registrations**.

- **Enterprise application (service principal)** is the running instance of that app inside a tenant. It is what actually exists and acts in my directory. This is where I assign users and groups to app roles, where sign in logs show up, and where I can grant tenant wide consent or set "assignment required". In the portal it is under **Enterprise applications**.

One registration produces one service principal per tenant that uses the app. A multi tenant app has one registration in the home tenant and a separate service principal in every customer tenant.

Why it matters in practice, and where I actually saw it:

- I **defined** the `Survey Writer` app role on the Employees API **registration** (App roles blade).
- I **assigned** the `HR-Survey-Writers` group to that role on the Employees API **enterprise application** (Users and groups blade).
- The token then carried `roles: Survey.Create` for members of that group.

So the rule of thumb: define capabilities on the registration, grant and assign them on the enterprise application, watch the result show up in the token.

## How this maps to a real product

The lab is Contoso's HR Portal, but the shape is any modern app with a backend API:

- The **API registration** is your backend. It publishes scopes for what user facing clients may do and app roles for what daemons may do, and its code enforces them.
- The **web/SPA/mobile registration** is your frontend. It signs users in and calls the API on their behalf with delegated tokens. In production it uses the auth code flow with PKCE, not the implicit flow I used to grab a token quickly in the browser.
- The **daemon/service registration** is your background workload. It uses client credentials with a certificate (not a secret in prod) to call the API as itself for unattended jobs.
- **Groups drive access.** Instead of assigning the role to each user, you assign it to a group and let HR or IT manage membership. Access changes with group membership and needs zero code or config change. That is the scalable pattern.
- **Conditional Access and MFA** wrap the user sign ins. They do not apply to the daemon because there is no user, which is exactly why workload identities need their own protection: certificates over secrets, short credential lifetimes, secret rotation, and workload identity Conditional Access.

## Security notes I want to keep

- An access token is for exactly one resource. That is the `aud` claim, and the API rejects anything whose `aud` is not itself.
- A client secret is a password string. If it leaks, the daemon is fully compromised. A certificate is much harder to steal, so production uses certificates. I used a secret because it is a lab, and I need to rotate it.
- Tenant wide admin consent means no user ever sees a consent prompt, but it also means the app has broad access, so the permissions it requests should be least privilege and reviewed.
- App only permissions can never be granted without an admin. There is no user in the loop at runtime to approve anything, so the admin decision up front is the whole gate.
