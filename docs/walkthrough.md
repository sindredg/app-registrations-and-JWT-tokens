# Walkthrough: the lab, step by step

This is the record of the lab in the order it was carried out, what happened at each step, and the takeaways. The Entra work was done in the portal by hand. For the last part, the small .NET API that reads the tokens, Claude helped write and debug it.

Screenshots live in `screenshots/` and are referenced inline.

## Goal

See how app registrations and authorization actually work in Microsoft Entra ID by building the whole chain end to end, then proving it with real tokens. Not just reading the module, but registering the apps, exposing scopes, defining roles, assigning through groups, and watching the claims show up in tokens. The final step runs a real API that reads those claims and returns 200 or 403, so authorization can be observed happening rather than just read about.

## Overview

Three app identities were built around one protected API, in a single tenant with an Entra ID P2 license:

- **Employees API**, the resource. Exposes scopes and app roles, and its code enforces them.
- **Employees Client**, an interactive web app. Calls the API as a signed in user (delegated).
- **Employees Daemon**, a background job. Calls the API as itself with no user (application permission).

For the full picture of how these talk to each other and how it maps to the real world, see `architecture.md`. The one idea to hold onto: nothing an app "has" is real until it shows up as a claim in a signed token, and the API decides everything from those claims.

## Step by step

### 1. Registered the Employees API and exposed two scopes

The API registration represents the resource that will be protected. It was given the Application ID URI `api://1769ebb1-8942-4e34-a500-4c50a9aad16e`, which is the unique identifier that prefixes every scope. Two delegated scopes were exposed: `Employees.Read.All` (admins and users can consent) and `Employees.Write.All` (admins only).

![Two scopes on the API](screenshots/expose-api-scopes.png)

What happened: both scopes appear in Expose an API, enabled. Exposing a scope publishes a named permission that a client can later request. The full scope string a client asks for is the App ID URI plus the scope name, for example `api://.../Employees.Read.All`.

Notes: splitting read and write, and gating write behind admin only consent, is least privilege in action. "Who can consent" is the first authorization gate, it decides whether an ordinary user can approve the permission or whether an admin has to.

### 2. Added an app only role for the daemon

A custom API declares application (app only) permissions as app roles with member type Applications. One was created with value `app-Employees.Read.All`. This is the permission the daemon will hold when it calls the API with no user present.

![App only role](screenshots/app-role-app-only.png)

Notes: delegated scopes are for a signed in user, app roles with member type Applications are for workloads. Same API, two separate ways to grant access depending on whether a human is in the loop.

### 3. Registered the Employees Client and the Daemon, added credentials

The client apps are separate registrations from the API. The Employees Client was registered as a single tenant app with redirect `https://jwt.ms`, which lets a token be returned to the browser and decoded there.

![Register the client](screenshots/register-the-client.png)

The Employees Daemon was registered and given a client secret. A confidential client authenticates as itself with a credential, and the portal only shows the secret value once.

![Daemon secret](screenshots/daemon-secret.png)

Notes: a secret is a password string, easy to use but full compromise if it leaks. A certificate is the production choice because a private key is much harder to steal.

### 4. Wired the clients to the API and granted consent

Adding a permission on a client only declares intent. It is not usable until it is consented. Two delegated permissions were added on the Client.

![Add delegated permissions](screenshots/client-delegated-permissions.png)

Admin consent was then granted, and the status flipped to granted for the tenant. Tenant wide consent records the grant once for everyone, so no user sees a consent prompt.

![Delegated consent granted](screenshots/consent-delegated-granted.png)

On the Daemon the app only permission was added and admin consent granted. Admin consent is the only way an app only permission can ever be granted, because there is no user at runtime to approve it.

![App only consent granted](screenshots/consent-apponly-granted.png)

Notes: requesting a permission and having it are different things. Consent is the grant. The status going from not granted to granted for the tenant is the moment the permission becomes real.

### 5. Set up the users and groups

Created users "Sindre Writer" (job title: "Survey Author", department: "HR") and user "Sindre Plain" (job title: "Analyst", department "HR"). "Sindre Plain" is the control case, and should end up with no roles.

Two groups back the setup: an HR group with a dynamic membership rule (department equals HR), which auto includes anyone whose department attribute is HR, and a security group HR-Survey-Writers with just Sindre Writer in it. The app role will be assigned to this second group.

NR Security Group:
![Dynamic rule department equals HR](screenshots/group-dynamic-rule-hr.png)
![Survey Writers group members](screenshots/group-hr-members.png)

HR Survey Writers Security Group:
![Survey Writers group members](screenshots/group-survey-writers-members.png)

Notes: driving membership by an attribute like department is the P2 dynamic group feature, and it is how access scales without hand editing membership.

### 6. Assigned the role through the group (on the enterprise app)

On the Employees API enterprise application, Users and groups, the HR-Survey-Writers group was assigned to the Survey Writer role. Every member of the group inherits the role, and it will surface in each member's token as a `roles` claim.

![Group assigned to role](screenshots/enterprise-app-group-role-assignment.png)

Notes: the role is defined on the app registration, but assigned on the enterprise application. The registration is the blueprint, the enterprise app is the running instance where assignments and logs live. This is spelled out in `architecture.md`.

### 7. Got a delegated token and read the claims

The first browser attempt failed with error unsupported_response_type, because the app was not allowed to return a token to the browser yet.

![Unsupported response type error](screenshots/error-unsupported-response-type.png)

The Access tokens checkbox was not ticked on the client. Enabling it under Authentication, implicit grant, allows the token to come straight back to the browser.

![Enable access tokens](screenshots/fix-enable-access-tokens.png)

Signed in as user "Sindre Writer" (member of the Survey Writers group) and the token came back. It carried `scp` with the consented scopes, `roles` with Survey.Create (inherited through the group), and `aud` set to the API, meaning the token is addressed to the Employees API and nothing else.

![Writer token scp](screenshots/writer-token-roles-scp.png)

Signed in as user "Sindre Plain" (member of the HR group only) and the token had the same `scp` but no `roles` claim at all. That is the control case working: same app, same scopes, but no role because there is no assignment.

Notes: everything set up earlier was invisible until here. A user token carries what the app may do (`scp`) and what the user may do (`roles`), and the only difference between Writer and Plain is the group driven role.

### 8. Got an app only token for the daemon

The client credentials request was run from WSL, with no browser and no user. The daemon token carried `roles: app-Employees.Read.All`, no `scp`, no user claims, `appid` set to the daemon, and `appidacr` of 1 meaning it authenticated with a secret.

![Daemon token roles](screenshots/curl-post-deamon)

Notes: app only tokens carry permissions in `roles`, not `scp`, and there is no user identity in them. The secret is the login, so no interactive sign in happens at all. This is the concrete contrast to the user tokens in step 7.

### 9. Checked the sign in logs

Sign in logs were filtered to the Employees app. User sign ins (interactive) show "Sindre Writer" and "Sindre Plain" hitting Employees Client with resource Employees API, Conditional Access success, MFA satisfied.

![Sign in logs, users with MFA](screenshots/signin-logs-users-mfa.png)

Notes: the tenant enforces MFA through Conditional Access, so both users had to complete MFA before a token was issued. The daemon, on the service principal sign ins tab, shows Conditional Access not applied, because a user targeted policy has no user to act on. This is why workload identities need their own protection rather than MFA.

### 10. Ran a real API that enforces the claims (the part Claude helped with)

A small .NET 8 API was written with two endpoints, `/employees/all` (needs the app only role) and `/surveys` (needs the user role Survey.Create). The API validates each incoming token, then reads its claims and decides allow or deny. The plan was to feed it the real tokens and watch the outcome.

First run, a placeholder string was sent instead of a real token and the API returned 401, which is correct, it rejected input that was not a valid token.

![First run, 401 on placeholder](screenshots/api-first-run-401-placeholder.png)

Then with a real daemon token the endpoint returned 403 even though the token clearly had the role. This took some debugging (see the section below).

![API valid token but 403](screenshots/api-valid-token-403.png)

After the fix, the API validated tokens properly (valid signature, audience validated) and the role check matched.

Final run: `/employees/all` returned 200 with the daemon token, and `/surveys` returned 403 with the same token because it has no Survey.Create.

WSL curl shell:
![200 on employees/all, 403 on surveys](screenshots/api-200-and-403.png)

WSL dotnet run shell:
![API validating the token](screenshots/api-validating-token.png)


Takeaway: same token, two endpoints, two outcomes, all decided by the `roles` claim. This is the moment the whole chain connects. The portal config becomes a claim in the token, and the claim becomes an allow or deny in code.

## What went wrong and how it was fixed

Keeping these because the mistakes were the most instructive part.

1. **unsupported_response_type** in the browser. The Access tokens checkbox was off on the client. Fixed under Authentication, implicit grant.
2. **Build error, VerifyUserHasAnyAcceptedScope not found.** That helper moved in newer Microsoft.Identity.Web. Switched to reading the `scp` claim directly.
3. **403 even though the token had the role.** The Entra `roles` claim gets remapped to a long schema URI, so IsInRole and RequireRole never matched. Fixed with `JsonWebTokenHandler.DefaultMapInboundClaims = false` and reading the `roles` claim directly. This is in `src/EmployeesApi/Program.cs`.

## Notes, short list

- Nothing is real until it is a claim in a signed token. The API decides everything from claims.
- Delegated means a user is present, capped at that user, permission lands in `scp`. Application means no user, full privilege, permission lands in `roles`, admin consent always.
- App registration is the blueprint, enterprise application is the running instance. Define on the registration, assign on the enterprise app.
- Assigning a role to a group is how access scales. Membership drives access with no code change.
- An access token is for exactly one resource, that is `aud`.
- Conditional Access and MFA protect user sign ins, not daemons. Workload identities need certificates, rotation, and their own policies.
- Consent is the grant. Requesting a permission is not the same as having it.
