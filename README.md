# Entra ID App Registration Lab

A hands on lab implementing the full application authorization chain in Microsoft Entra ID, inspired by the Microsoft Learn module *Implement app registration* (SC-300). It registers a protected API, two client applications, and drives authorization end to end: exposing scopes, granting consent, defining app roles, assigning access through groups, and enforcing the resulting token claims in a working .NET 8 API.

Built and tested in a tenant with an Entra ID P2 license. The Entra configuration was performed manually. The accompanying .NET API was written with assistance from Claude.

## Overview

Three application identities are registered around one protected resource:

| Application | Role | Access model |
|---|---|---|
| Employees API | The protected resource. Exposes scopes and app roles and enforces them in code. | Validates tokens |
| Employees Client | Interactive web app. Calls the API on behalf of a signed in user. | Delegated (`scp`) |
| Employees Daemon | Background job with no user. Calls the API as itself. | Application (`roles`) |

The scenario models an in house HR Portal: the API is the backend, the Client is the web app the HR team signs into, and the Daemon is an unattended payroll sync job.

## Documentation

- **[docs/walkthrough.md](docs/walkthrough.md)** — the lab step by step, with screenshots, what happened at each stage, issues encountered, and the fixes.
- **[docs/architecture.md](docs/architecture.md)** — the design, the token flow, how the pattern maps to a real product, and the distinction between an app registration and an enterprise application.
- **[docs/lab-guide.md](docs/lab-guide.md)** — a reusable, fully explained procedure for reproducing the lab.
- **[src/EmployeesApi/README.md](src/EmployeesApi/README.md)** — the API code, its enforcement logic, and how to run it.

## Repository structure

```
.
├── README.md
├── docs/
│   ├── walkthrough.md      the lab, step by step, with screenshots
│   ├── architecture.md     design, token flow, real world mapping
│   ├── lab-guide.md         reusable procedure to reproduce the lab
│   ├── notes.md             working notes and commands
│   └── screenshots/         portal and terminal screenshots
└── src/
    └── EmployeesApi/        minimal .NET 8 API that enforces token claims
        ├── README.md
        ├── Program.cs
        ├── appsettings.json
        └── EmployeesApi.csproj
```

## Core concept

Authorization decisions are made entirely from the claims in a signed access token. Portal configuration (scopes, consent, app roles, group assignments) matters only because it determines what appears in the token. The API validates the token, then reads `scp` and `roles` to allow or deny each request. The same token returns 200 on one endpoint and 403 on another, based solely on its `roles` claim.

- Delegated tokens carry `scp` and a user identity, capped at that user's privilege.
- Application (app only) tokens carry `roles` and no user, and always require admin consent.
- Capabilities are defined on the app registration; users and groups are assigned on the enterprise application.

## Running the API

```bash
cd src/EmployeesApi
dotnet run
```

The API listens on `http://localhost:5080`. See [src/EmployeesApi/README.md](src/EmployeesApi/README.md) for acquiring tokens and testing the endpoints.

## Security

- No secrets are committed. Tenant and client identifiers in `appsettings.json` are not secrets, but they do identify the tenant.
- The client secret used during the lab must be rotated, and any test credentials reset, before this configuration is reused.
- Production workloads should use certificate credentials over client secrets, apply least privilege scopes, and protect workload identities with their own Conditional Access policies.
