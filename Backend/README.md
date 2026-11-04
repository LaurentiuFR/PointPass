# PointPass private pilot API

This is the persistent admission/result service, not the real-time football simulation.
It targets .NET 10 and PostgreSQL 18, with Npgsql pinned to 10.0.3. The database pool
has at most eight connections and no prewarmed minimum. Movement never writes to it.

## Build and local checks

```powershell
dotnet restore Backend/PointPass.Api.Checks/PointPass.Api.Checks.csproj --configfile Backend/NuGet.Config
dotnet build Backend/PointPass.Api/PointPass.Api.csproj -c Release --no-restore
dotnet run --project Backend/PointPass.Api.Checks/PointPass.Api.Checks.csproj -c Release --no-restore
./Tools/Validation/Online/BackendSmoke.ps1
dotnet publish Backend/PointPass.Api/PointPass.Api.csproj -c Release --no-restore -o Logs/BackendPublish/Api /p:UseAppHost=false
```

`BackendSmoke.ps1` launches an isolated loopback API with an intentionally unavailable
database, checks HTTP rejection paths, then stops only the process it created. Those
checks do not prove database or gameplay correctness.

## Protected server configuration

Use a protected environment file outside Git, read only by the service manager.
`Deploy` contains deployment and systemd configuration.

| Environment variable | Meaning |
| --- | --- |
| `ASPNETCORE_URLS` | `http://127.0.0.1:5000`, never a public listener |
| `ConnectionStrings__PointPass` | Unix-socket/peer-authenticated limited application role |
| `PointPass__WorkerKey` | At least 43 characters of cryptographically random server-only secret |
| `PointPass__GameVersion` | Exact compatible protocol, initially `PointPass-Online-1` |
| `PointPass__GameHost` | Public game-server IPv4 |
| `PointPass__GamePort` | UDP game port, initially `7777` |
| `PointPass__TlsServerName` | Certificate identity checked by game clients |

The API account owns no schema. Apply `PointPass.Api/Migrations/001_initial.sql` as
the separate deployment owner inside a transaction, record/check `schema_migrations`,
and grant only table CRUD to the application role. Reapplying a recorded migration
must skip it; partially modified or unexpected schemas fail rather than being silently
accepted. No migration or database-creation endpoint exists.

Create a single-use, 24-hour invitation on the server:

```sh
# Run under the API service account with its protected configuration loaded.
dotnet /opt/pointpass/current/api/PointPass.Api.dll --create-invite --invite-file /var/lib/pointpass-api/invitations/new-private-invitation.txt
```

The destination must be a new absolute path in a private directory. The CLI writes
the code with owner-only read/write permissions and does not print it. Transfer it
privately to one tester. Never attach the file to Git or logs. Guest credentials,
access tokens and match tickets likewise must not be logged.

## HTTP contract

All JSON uses camelCase. Unknown or duplicate properties, null required values, deep
JSON and unexpected query strings are rejected. Bodies are capped at 8 KiB. Errors
use `{ "code": "...", "message": "..." }`; a response rejected at the web-server
body-size boundary may have an empty body with status 413. The public proxy supplies
HTTPS and blocks `/internal/`; Kestrel trusts forwarded headers from loopback only.

| Method/path | Request | Response |
| --- | --- | --- |
| `GET /health` | None | `{status,protocol}`; process liveness, not database readiness |
| `POST /v1/guests` | `{invitation,credential,displayName}` | `{playerId,accessToken,expiresUtc,displayName}` |
| `POST /v1/guests/session` | `{credential}` | Same session response |
| `GET /v1/profile` | Bearer access token | Own `{playerId,displayName,createdUtc,played,wins,draws,losses}` |
| `GET /v1/history` | Bearer access token | Own latest 20 completed results |
| `POST /v1/room/join` | Bearer + `{gameVersion}` | `{matchId,playerId,slot,ticket,expiresUtc,host,port,tlsServerName}` |
| `POST /v1/room/leave` | Bearer + `{matchId}` | 204 |

The client creates a cryptographically random 32-byte credential, represented as
43-character unpadded Base64Url, and protects it locally **before** registration.
Registration retries reuse that identity rather than consuming another invitation.
Only hashes of credentials, invitations, access tokens and match tickets are stored.
Access tokens expire in ten minutes; a guest has at most three live sessions.
Local demo wallets/inventory are neither uploaded nor treated as trusted funds.

Tickets expire in 30 seconds, are single-use, and are bound to the player, match,
worker instance and exact game version. There is one open match with two fixed slots
(0 = blue, 1 = red). An authenticated returning player can reclaim their slot for
30 seconds after disconnect. New players cannot replace participants in an active
match. A delayed disconnect includes its original connection ID and cannot eject
the player's new connection.

The worker calls loopback `/internal/...` endpoints with `X-PointPass-Worker-Key`:

| Path (POST unless noted) | Request |
| --- | --- |
| `/worker/register` | `{instanceId,gameVersion}` |
| `/worker/heartbeat` | `{instanceId}` |
| `/tickets/redeem` | `{instanceId,ticket,gameVersion,connectionId}` |
| `/players/disconnect` | `{instanceId,matchId,playerId,connectionId}` |
| `/matches/result` | `{instanceId,matchId,blueScore,redScore}` |
| `/matches/abandon` | `{instanceId,matchId,reason}`; reason is a bounded alphanumeric/underscore tag |
| `GET /security-counters` | None |

Registration/heartbeat replies are `{instanceId,matchId,state,players}` with each player
`{playerId,slot,connected,connectionId}`; disconnected/reserved slots have a null connection ID.
This reconciliation identity is returned only to the trusted worker, never to public
profile or admission endpoints. If a redemption commits but its HTTP response is lost,
the worker can identify and clear the orphan through heartbeat and the existing
connection-bound disconnect operation, without consuming a ticket twice.
Idle has null match ID and an empty player array. Redemption
replies `{matchId,playerId,slot}`. Registering a new trusted worker instance abandons
its predecessor's unfinished match. Admissions require a heartbeat within 30 seconds.
Results require the trusted worker, two authenticated participants and an active
match. Repeating an identical result succeeds without adding rewards or another row;
repeating a different result is a conflict. No client result submission exists.

Global API traffic is limited to 90 requests/minute/IP, with guest registration/login
also limited to ten/minute/IP. Limiters have no queue. Six fixed security-counter
buckets bound memory; the API never automatically bans an account. Expired sessions,
old invitations and spent/expired tickets are pruned every five minutes. Persistent
profiles/results are retained and their disk growth must be measured operationally.

## Real database integration checks

The executable deliberately refuses a production database. Create a **disposable**
`pointpass_validation` database with the same migration and grants. Start a second API
bound only to `127.0.0.1:5001`, using that database. Load its protected environment
without printing it, then run:

```sh
export POINTPASS_TEST_ALLOW_MUTATION=isolated-validation
export POINTPASS_API_TEST_URL=http://127.0.0.1:5001
dotnet /opt/pointpass/checks/PointPass.Api.Checks.dll --integration
```

The checks create disposable invitations/guests/matches and verify authentication,
identity isolation, version rejection, capacity, ticket consumption/expiry, reconnection,
stale disconnects, trusted result submission, idempotency and history. They register a
fake worker **only in the disposable database**, so never point them at the live pilot.
Stop the temporary API and remove the disposable database after validation. Service
restart persistence and encrypted game transport are separate deployment/game tests.
