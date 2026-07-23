Working notes:

## The pieces that were set up

- Tenant: `567352e7-06fd-4c84-bdc5-f3558b660cb4`
- Employees API: client id `1769ebb1-8942-4e34-a500-4c50a9aad16e`, app id uri `api://1769ebb1-8942-4e34-a500-4c50a9aad16e`
- Employees Client: client id `6da59956-4fd4-4c56-8c92-85f996560d11`
- Employees Daemon: client id `b26a94f0-f0d3-446f-bc4a-68fe6f28ada2`, secret REDACTED, rotate it
- Users: Sindre Writer (group HR-Survey-Writers, role Survey Writer), Sindre Plain (group HR-Readers, no role)

## Commands used

Daemon token, first quick test (prints the raw JSON so you can eyeball the access_token):

```bash
curl -X POST "https://login.microsoftonline.com/<TENANT_ID>/oauth2/v2.0/token" \
  -d "client_id=<DAEMON_CLIENT_ID>" \
  -d "client_secret=<DAEMON_SECRET>" \
  -d "scope=api://<API_APP_ID>/.default" \
  -d "grant_type=client_credentials"
```

Daemon token, app only, in WSL or git bash (not powershell):

```bash
TOKEN=$(curl -s -X POST "https://login.microsoftonline.com/567352e7-06fd-4c84-bdc5-f3558b660cb4/oauth2/v2.0/token" \
  -d "client_id=b26a94f0-f0d3-446f-bc4a-68fe6f28ada2" \
  -d "client_secret=REDACTED_ROTATE_ME" \
  -d "scope=api://1769ebb1-8942-4e34-a500-4c50a9aad16e/.default" \
  -d "grant_type=client_credentials" | grep -o '"access_token":"[^"]*' | cut -d'"' -f4)
echo "${TOKEN:0:20}..."
```

Decode a token:

```bash
python3 -c "import sys,base64,json; p=sys.argv[1].split('.')[1]; p+='='*(-len(p)%4); d=json.loads(base64.urlsafe_b64decode(p)); print('roles:', d.get('roles')); print('scp:', d.get('scp'))" "$TOKEN"
```

Run and test the API:

```bash
cd src/EmployeesApi && dotnet run
# other tab
curl -i http://localhost:5080/employees/all -H "Authorization: Bearer $TOKEN"   # 200
curl -i -X POST http://localhost:5080/surveys -H "Authorization: Bearer $TOKEN" # 403
```

## How the run went, in order

1. Browser gave unsupported_response_type. Access tokens checkbox was off on the client. Fixed under Authentication.
2. The daemon token came back fine. roles was app-Employees.Read.All, scp empty, no user claims.
3. API build error, VerifyUserHasAnyAcceptedScope missing (moved in newer Microsoft.Identity.Web). Read scp directly instead.
4. 403 even though the token had the role. The roles claim gets remapped to a schema URI. Fixed with JsonWebTokenHandler.DefaultMapInboundClaims = false and reading roles directly.
5. Final run worked. 200 on employees/all, 403 on surveys with the same daemon token.
