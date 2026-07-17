# Walkthrough: what I did, step by step

This is the record of the lab in the order I actually did it, what happened at each step, and what I learned. I did the Entra IAM work myself. For the last part, the small .NET API that reads the tokens, I used Claude to help me write and debug it.

Screenshots live in `screenshots/` and are referenced inline.

## Goal

Experiment with how app registrations and authorization actually work in Microsoft Entra ID by building the whole chain myself, then proving it with real tokens. Not just reading the module, but registering the apps, exposing scopes, defining roles, assigning through groups, and watching the claims show up in tokens. The final step was to run a real API that reads those claims and returns 200 or 403, so I could see authorization happen instead of just reading about it.

## Overview

I built three app identities around one protected API, in my own tenant with an Entra ID P2 license:

- **Employees API**, the resource. Exposes scopes and app roles, and its code enforces them.
- **Employees Client**, an interactive web app. Calls the API as a signed in user (delegated).
- **Employees Daemon**, a background job. Calls the API as itself with no user (application permission).

For the full picture of how these talk to each other and how it maps to the real world, see `architecture.md`. The one idea to hold onto: nothing an app "has" is real until it shows up as a claim in a signed token, and the API decides everything from those claims.

## Step by step

### 1. Registered the Employees API and exposed two scopes

I registered the API, set its Application ID URI to `api://1769ebb1-8942-4e34-a500-4c50a9aad16e`, and exposed two delegated scopes: `Employees.Read.All` (admins and users can consent) and `Employees.Write.All` (admins only).

![Two scopes on the API](screenshots/01-expose-api-scopes.png)

What happened: both scopes showed up in Expose an API, enabled. The full scope string is the App ID URI plus the scope name.

Notes: splitting read and write, and gating write behind admin only consent, is least privilege in action. "Who can consent" is the first authorization gate.

### 2. Added an app only role for the daemon

On the API I created an app role with member type Applications, value `app-Employees.Read.All`. This is the permission the daemon will hold.

![App only role](screenshots/02-app-role-app-only.png)

Notes: app only permissions on a custom API are surfaced as app roles with member type Applications. Delegated is for users, this is for workloads.

### 3. Registered the Employees Client and the Daemon, added credentials

Registered the Employees Client as a single tenant app with redirect `https://jwt.ms` so I could read tokens in the browser.

![Register the client](screenshots/03-register-client.png)

Registered the Employees Daemon and gave it a client secret. The portal only shows the secret value once.

![Daemon secret](screenshots/04-daemon-secret.png)

Notes: a confidential client proves itself with a credential. A secret is easy but a leaked string is full compromise, a certificate is the production choice.

### 4. Wired the clients to the API and granted consent

On the Client I added the two delegated permissions.

![Add delegated permissions](screenshots/05-client-delegated-permissions.png)

Then granted admin consent, and the status flipped to granted for the tenant.

![Delegated consent granted](screenshots/06-consent-delegated-granted.png)

On the Daemon I added the app only permission and granted admin consent (the only way app only permissions can ever be granted).

![App only consent granted](screenshots/07-consent-apponly-granted.png)

Notes: requesting a permission and having it are different things. Consent is the grant. Watching the status go from not granted to granted for the tenant is the whole point.

### 5. Set up the users and groups

Created users "Sindre Writer" (job title: "Survey Author", department: "HR") and user "Sindre Plain" (job title: "Analyst", department "HR"). "Sindre Plain" will be my control case, and should end up with no roles.

Made an HR group with a dynamic membership rule (department equals HR), and a security group HR-Survey-Writers with just Sindre Writer in it.

NR Security Group:
![Dynamic rule department equals HR](screenshots/10-group-dynamic-rule-hr.png)
![Survey Writers group members](screenshots/11-group-survey-writers-members.png)

HR Survey Writers Security Group:
![Survey Writers group members](screenshots/11-group-survey-writers-members.png)

Notes: driving membership by an attribute like department is the P2 dynamic group feature, and it is how access scales without hand editing.

### 6. Assigned the role through the group (on the enterprise app)

On the Employees API enterprise application, Users and groups, I assigned the HR-Survey-Writers group to the Survey Writer role.

![Group assigned to role](screenshots/12-enterprise-app-group-role-assignment.png)

Notes: Define the role on the app registration, but assign it on the enterprise application. The registration is the blueprint, the enterprise app is the running instance where assignments and logs live. This is spelled out in `architecture.md`.

### 7. Got a delegated token and read the claims

First try in the browser failed with error unsupported_response_type.

![Unsupported response type error](screenshots/13-error-unsupported-response-type.png)

The Access tokens checkbox was not ticked on the client. I enabled it under Authentication, implicit grant.

![Enable access tokens](screenshots/14-fix-enable-access-tokens.png)

Then signed in as user "Sindre Writer" (member of the Survey Writers group) and the token came back. It had `scp` with the scopes, and `roles` with Survey.Create (which came in through the group), and `aud` set to the API.

![Writer token scp](screenshots/writer-token-roles-scp.png)

Signed in as user "Sindre Plain" (Member of the HR group only) and the token had the same `scp` but no `roles` claim at all. That is the control case working.

Notes: everything I set up was invisible until here. The user token carries what the app may do (`scp`) and what the user may do (`roles`), and the only difference between Writer and Plain is the group driven role.

### 8. Got an app only token for the daemon

Ran the client credentials request from WSL (no browser, no user). The daemon token had `roles: app-Employees.Read.All`, no `scp`, no user claims, `appid` was the daemon, and `appidacr` was 1 meaning it authenticated with a secret.

![Daemon token roles](screenshots/curl-post-deamon)

Notes: app only tokens carry permissions in `roles`, not `scp`, and there is no user. The secret is the login, so no interactive sign in happens at all.

### 9. Checked the sign in logs

Filtered sign in logs to the Employees app. User sign ins (interactive) showed "Sindre Writer" and "Sindre Plain" hitting Employees Client with resource Employees API, Conditional Access success, MFA satisfied.

![Sign in logs, users with MFA](screenshots/signin-logs-users-mfa.png)

Notes: the tenant enforces MFA through Conditional Access, so both users had to do MFA before a token was issued. The daemon, on the service principal sign ins tab, had Conditional Access not applied, because there is no user for a user policy to target.

### 10. Ran a real API that enforces the claims (the part I used Claude for)

With Claude I wrote a small .NET 8 API with two endpoints, `/employees/all` (needs the app only role) and `/surveys` (needs the user role Survey.Create). The idea was to feed it my real tokens and watch it allow or deny.

First run, I tested with placeholder text instead of a real token and got 401, which is correct, the API rejected garbage.

![First run, 401 on placeholder](screenshots/api-first-run-401-placeholder.png)

Then with a real daemon token I got 403 on `/employees/all` even though the token clearly had the role. This one took some debugging.

![API valid token but 403](screenshots/api-valid-token-403.png)

The API started validating tokens properly (valid signature, audience validated) once the code was fixed.

Final run: `/employees/all` returned 200 with the daemon token, and `/surveys` returned 403 with the same token because it has no Survey.Create.

WSL curl shell:
![200 on employees/all, 403 on surveys](screenshots/api-200-and-403.png)

WSL dotnet run shell:
![API validating the token](screenshots/api-validating-token.png)


Learning: same token, two endpoints, two outcomes, all decided by the `roles` claim. This is the moment the whole lab clicked. The portal config becomes a claim, the claim becomes an allow or deny in code.

## What went wrong and how I fixed it

Keeping these because the mistakes taught me the most.

1. **unsupported_response_type** in the browser. The Access tokens checkbox was off on the client. Fixed under Authentication, implicit grant.
2. **Build error, VerifyUserHasAnyAcceptedScope not found.** That helper moved in newer Microsoft.Identity.Web. Switched to reading the `scp` claim directly.
3. **403 even though the token had the role.** The Entra `roles` claim gets remapped to a long schema URI, so IsInRole and RequireRole never matched. Fixed with `JsonWebTokenHandler.DefaultMapInboundClaims = false` and reading the `roles` claim directly. This is in `src/EmployeesApi/Program.cs`.

## Notes, the short list

- Nothing is real until it is a claim in a signed token. The API decides everything from claims.
- Delegated equals a user is present, capped at that user, permission lands in `scp`. Application equals no user, full privilege, permission lands in `roles`, admin consent always.
- App registration is the blueprint, enterprise application is the running instance. Define on the registration, assign on the enterprise app.
- Assigning a role to a group is how access scales. Membership drives access with no code change.
- An access token is for exactly one resource, that is `aud`.
- Conditional Access and MFA protect user sign ins, not daemons. Workload identities need certificates, rotation, and their own policies.
- Consent is the grant. Requesting a permission is not the same as having it.

## Cleanup checklist

- Rotate the daemon secret (it was in my scratch notes in plain text).
- Reset the Sindre Writer and Sindre Plain passwords.
- Delete the three app registrations if I do not need them (this removes the enterprise apps too).
- Remove the group role assignment.
