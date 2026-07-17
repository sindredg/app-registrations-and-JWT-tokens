# Working notes

Secrets and passwords are redacted on purpose, do not put them back in.

## The pieces that were set up

- Tenant: `567352e7-06fd-4c84-bdc5-f3558b660cb4`
- Employees API: client id `1769ebb1-8942-4e34-a500-4c50a9aad16e`, app id uri `api://1769ebb1-8942-4e34-a500-4c50a9aad16e`
- Employees Client: client id `6da59956-4fd4-4c56-8c92-85f996560d11`
- Employees Daemon: client id `b26a94f0-f0d3-446f-bc4a-68fe6f28ada2`, secret REDACTED, rotate it
- Users: Sindre Writer (group HR-Survey-Writers, role Survey Writer), Sindre Plain (group HR-Readers, no role)

## Commands used

Delegated token in the browser, sign in as the user:

```
https://login.microsoftonline.com/567352e7-06fd-4c84-bdc5-f3558b660cb4/oauth2/v2.0/authorize?client_id=6da59956-4fd4-4c56-8c92-85f996560d11&response_type=token&redirect_uri=https://jwt.ms&scope=api://1769ebb1-8942-4e34-a500-4c50a9aad16e/Employees.Read.All&state=12345&nonce=abc123
```

Daemon token, app only, in WSL or git bash (not powershell):

```bash
TOKEN=$(curl -s -X POST
