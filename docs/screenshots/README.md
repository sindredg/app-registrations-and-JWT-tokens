# Screenshots

Referenced from `../walkthrough.md`. Drop the images in here using the repo names below. Mapping from my original screenshot dump filenames:

| Repo name | From original | Shows |
|---|---|---|
| 01-expose-api-scopes.png | Screenshot 2026-07-17 044324.png | The two scopes on the Employees API |
| 02-app-role-app-only.png | Screenshot 2026-07-17 045638.png | App only role app-Employees.Read.All |
| 03-register-client.png | Screenshot 2026-07-17 051115.png | Registering the Employees Client |
| 04-daemon-secret.png | Screenshot 2026-07-17 052459.png | Daemon client secret (value masked) |
| 05-client-delegated-permissions.png | Screenshot 2026-07-17 055745.png | Adding delegated permissions to the client |
| 06-consent-delegated-granted.png | Screenshot 2026-07-17 060127.png | Delegated permissions granted for tenant |
| 07-consent-apponly-granted.png | Screenshot 2026-07-17 060731.png | App only permission granted for tenant |
| 08-user-writer-properties.png | Screenshot 2026-07-17 063443.png | Sindre Writer (Survey Author, HR) |
| 09-user-plain-properties.png | Screenshot 2026-07-17 063746.png | Sindre Plain (Analyst, HR) |
| 10-group-dynamic-rule-hr.png | Screenshot 2026-07-17 064215.png | Dynamic membership rule department equals HR |
| 11-group-survey-writers-members.png | Screenshot 2026-07-17 064927.png | HR-Survey-Writers group with Sindre Writer |
| 12-enterprise-app-group-role-assignment.png | Screenshot 2026-07-17 070239.png | Group assigned to Survey Writer role on the enterprise app |
| 13-error-unsupported-response-type.png | Screenshot 2026-07-17 072902.png | The unsupported_response_type error |
| 14-fix-enable-access-tokens.png | Screenshot 2026-07-17 073303.png | Enabling Access tokens on the client |
| 15-writer-token-scp.png | Screenshot 2026-07-17 074646.png | Writer token, scp claim |
| 16-writer-token-roles.png | Screenshot 2026-07-17 074741.png | Writer token, roles Survey.Create plus scp |
| 17-writer-claims-explained.png | Screenshot 2026-07-17 075620.png | Claims tab, roles and scp with descriptions |
| 18-aud-claim.png | Screenshot 2026-07-17 075702.png | aud claim set to the API |
| 19-plain-token-scp-no-roles.png | Screenshot 2026-07-17 080418.png | Plain token, scp but no roles |
| 20-daemon-token-roles.png | Screenshot 2026-07-17 082737.png | Daemon token, roles app-Employees.Read.All |
| 21-daemon-appid-appidacr.png | Screenshot 2026-07-17 083017.png | Daemon token, appid and appidacr 1 |
| 22-signin-logs-users-mfa.png | Screenshot 2026-07-17 085703.png | Sign in logs, users with CA success and MFA |
| 23-api-first-run-401-placeholder.png | Screenshot 2026-07-17 092502.png | API returns 401 on placeholder text |
| 25-api-validating-token.png | Screenshot 2026-07-17 093546.png | API validating a real token (signature, audience) |
| 26-api-200-and-403.png | Screenshot 2026-07-17 094756.png | 200 on employees/all, 403 on surveys |

## Left out on purpose

Secret exposed in plain text (do not commit, or crop the secret line first):
- Screenshot 2026-07-17 082043.png
- Screenshot 2026-07-17 093139.png
- Screenshot 2026-07-17 093524.png

Blank or duplicate frames:
- Screenshot 2026-07-17 073237.png
- Screenshot 2026-07-17 092803.png
- Screenshot 2026-07-17 093849.png
- Screenshot 2026-07-17 083412.png (full token dump, redundant)
- Screenshot 2026-07-17 064842.png (HR group members, optional)
