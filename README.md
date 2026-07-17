# Entra ID App Registration home lab

This is my personal record of a home lab inspired by the Microsoft Learn module "Implement app registration" (SC-300 track).The point of this repo is to remember what was done, learn from it, and have something to look back at later.

## Start here

- **[docs/walkthrough.md](docs/walkthrough.md)** is the main thing. Goal, overview, every step I did with screenshots, what happened, what went wrong, and what I learned.
- **[docs/architecture.md](docs/architecture.md)** is how it all fits together and how the same pattern works in the real world, including the app registration versus enterprise application separation that I keep needing to remember.

## What was built

Three app identities around one protected API:

- **Employees API**, the resource. Exposes scopes and app roles, and its code enforces them.
- **Employees Client**, an interactive web app. Calls the API as a signed in user (delegated).
- **Employees Daemon**, a background job. Calls the API as itself with no user (application permission).

The scenario used to make it concrete is an in house HR Portal: the API is the backend, the Client is the web app HR signs into, the Daemon is a nightly payroll job.

## Repo layout

```
.
├── README.md
├── docs/
│   ├── walkthrough.md     what I did, step by step, with screenshots and learnings
│   ├── architecture.md    design, how it works, real world, registration vs enterprise app
│   ├── lab-guide.md       the reusable how to, every step explained, if I want to redo it
│   ├── notes.md           my raw working notes
│   └── screenshots/       the portal and terminal screenshots
└── src/
    └── EmployeesApi/      the minimal .NET 8 API that reads the claims
        ├── Program.cs
        ├── appsettings.json
        └── EmployeesApi.csproj
```

## Am omtrestomg idea 

Nothing an app "has" is real until it shows up as a claim in a signed token. Everything configured in the portal (scopes, consent, roles, group assignment) only mattered because it changed what ended up in the token, and the API decides allow or deny purely from those claims. The lab clicked when we ran the API and watched the same token get a 200 on one endpoint and a 403 on another, based only on its `roles` claim.
