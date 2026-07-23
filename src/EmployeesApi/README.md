# EmployeesApi

A minimal .NET 8 API that protects two endpoints with Microsoft Entra ID and makes its allow or deny decision purely from the claims in the incoming token. This is the part of the lab that turns the portal configuration into real enforcement.

## What it does

Every request must carry a valid bearer token issued by the tenant for this API's audience. Once the token is validated, each endpoint reads specific claims and returns 200 or 403.

- `POST /surveys` is the delegated (user) path. It requires both the delegated scope `Employees.Read.All` in the `scp` claim and the app role `Survey.Create` in the `roles` claim.
  - Sindre Writer has the scope and the role (via the HR-Survey-Writers group) so gets 200.
  - Sindre Plain has the scope but no role, so gets 403.
- `GET /employees/all` is the application (app only) path. It requires the app role `app-Employees.Read.All` in the `roles` claim.
  - The Employees Daemon token has that role, so gets 200.
  - A user token without that role gets 403.

Same audience, different claims, different outcome. That is the whole point.

## How the token check works

- `AddMicrosoftIdentityWebApi` wires up bearer token validation from the `AzureAd` section of `appsettings.json`. It checks the signature, the issuer, and that the audience matches this API.
- Authorization is then a plain claim check. `scp` is a space separated string of scopes, `roles` is one value per app role.

## Two gotchas that are already fixed in the code

1. `HttpContext.VerifyUserHasAnyAcceptedScope(...)` was removed in newer Microsoft.Identity.Web, which caused a build error. The code reads the `scp` claim directly instead.
2. `IsInRole` and `RequireRole` returned 403 even for a token that clearly had the role, because the Entra `roles` claim gets remapped to a long schema URI. The fix is `JsonWebTokenHandler.DefaultMapInboundClaims = false` plus a small helper (`RolesOf`) that reads the `roles` claim directly. Both are at the top of `Program.cs`.

## Configure

`appsettings.json` holds the tenant and API identifiers. These are not secrets (they ride in every token), but they do identify the tenant:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<tenant id>",
    "ClientId": "<Employees API app id>",
    "Audience": "api://<Employees API app id>"
  }
}
```

The daemon secret is never stored here. It is only used at token request time in the client credentials call, not by this API.

## Run it

```bash
dotnet run
```

Listens on http://localhost:5080. Then, from another shell, send a token and call an endpoint:

```bash
# get a token first (see docs/notes.md), then:
curl -i http://localhost:5080/employees/all -H "Authorization: Bearer $TOKEN"   # app only role -> 200
curl -i -X POST http://localhost:5080/surveys -H "Authorization: Bearer $TOKEN" # no Survey.Create -> 403
```

Tokens expire after about an hour, so get a fresh one if the API returns 401.

## Files

- `Program.cs` the whole API, top level statements, two endpoints.
- `appsettings.json` the AzureAd config the token validator reads.
- `EmployeesApi.csproj` targets net8.0, references Microsoft.Identity.Web.
