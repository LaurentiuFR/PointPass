# Online pilot deployment

See the [dated deployment verification record](VERIFICATION.md) for checks actually completed, measured idle resources and remaining validation. It is an intermediate checkpoint, not a claim that Windows gameplay has passed.

These scripts target the audited **Ubuntu 26.04 x86_64** machine at **62.238.115.207**. They use its existing four vCPUs, 8 GB RAM and 80 GB SSD. They do not provision, resize, buy services or configure cloud billing. One .NET application, one private PostgreSQL cluster and one Unity worker are the entire deployment. Only one two-player match is admitted. This is a pilot capacity limit, not a measured scaling claim.

## Approval and recovery boundary

Run the scripts only after reviewing the diff and rechecking the machine. Do not paste secret environment files into chat or logs. Never copy the SSH private key to the server.

The owner has explicitly requested **leaving SSH access unchanged for now**. These scripts do not create an administration account or alter root/password/SSH configuration. SSH hardening remains pending a later owner decision. Before that future step, establish and independently verify a dedicated recovery administrator. **A restricted service-control sudo helper does not provide general system recovery.** Creating unrestricted passwordless sudo requires separate owner approval.

Keep the original SSH session open during firewall or SSH changes. The firewall rollout arms an automatic five-minute rollback. Open a **new** authenticated connection and verify it before cancelling the timer:

```sh
sudo systemctl stop pointpass-firewall-rollback.timer
```

Any later approved SSH hardening needs its own reviewed recovery and rollback procedure. Keep it separate from this deployment.

## Build and stage

1. Use Unity **6000.3.27f2** with Linux Dedicated Server support. Build the server through the project's dedicated-server build entry point; output must include `PointPassServer.x86_64` and its data/runtime directories. A normal Linux graphical client is not the dedicated-server artifact.
2. Build the backend on the development machine:

   ```powershell
   dotnet publish Backend/PointPass.Api/PointPass.Api.csproj -c Release -r linux-x64 --self-contained false -o Logs/OnlinePublish/api
   ```

3. Stage `Deploy`, backend publish output, Linux server output and `Backend/PointPass.Api/Migrations` onto the existing host using verified SSH/SCP. Do not stage `.git`, client credential files, private keys, development logs, or unrelated game assets.
4. Keep scripts as LF UTF-8. From the checked-out repository, validate them with `bash -n Deploy/*.sh` before transfer. This is syntax verification, not a substitute for running the deployment on Linux.

## Initial rollout

Run as root or through a previously approved administrator. Paths below are examples of staged directories, not download commands.

```sh
sudo bash /var/tmp/pointpass-deploy/Deploy/install-base.sh
sudo bash /var/tmp/pointpass-deploy/Deploy/configure-database.sh
sudo bash /var/tmp/pointpass-deploy/Deploy/configure-secrets.sh
sudo bash /var/tmp/pointpass-deploy/Deploy/configure-firewall.sh
# Verify NEW SSH connection, then cancel the firewall rollback timer.
sudo systemctl stop pointpass-firewall-rollback.timer
sudo bash /var/tmp/pointpass-deploy/Deploy/configure-certificate.sh
sudo bash /var/tmp/pointpass-deploy/Deploy/deploy-release.sh \
  /var/tmp/pointpass-deploy/api \
  /var/tmp/pointpass-deploy/match \
  /var/tmp/pointpass-deploy/Migrations
```

`install-base.sh` installs distro .NET 10/PostgreSQL 18, Nginx and small utilities. Certbot **5.8.0** is pinned in a separate Python virtual environment because Ubuntu's packaged Certbot 4.0 cannot do IP/webroot issuance. The supported virtual-environment installation is maintained on a best-effort basis by Certbot; review/update this pin deliberately rather than allowing a surprise upgrade during renewals. The script refuses to replace a modified default Nginx site. Database setup refuses unrelated databases before modifying the fresh pilot cluster.

The backend listens only on `127.0.0.1:5000`. PostgreSQL uses a Unix socket and has **no TCP listener**. Its runtime role is authenticated by the separate `pointpassapi` OS account, has a maximum of eight connections and cannot create schemas, roles or databases. `pointpassowner` is a non-login migration owner. Migrations run under that owner, outside the application; applied versions are skipped, unexpected schema drift fails. No database password is embedded in clients or Git.

The server-only shared worker key is generated on the server. `/etc/pointpass/api.env` and `match.env` are root-only mode 0600; systemd passes them only to their respective service. Application binaries are root-owned and read-only to the service accounts. The API and match worker run as **different non-root users**. Their writable data is restricted to separate state directories.

Before opening gameplay publicly, verify the Linux worker starts correctly, has no cameras/UI/audio/rendering, stays idle without gameplay simulation, and accepts encrypted compatible clients using valid redeemed tickets. Verify rejected invalid/reused tickets and capacity. Then:

```sh
sudo bash /var/tmp/pointpass-deploy/Deploy/expose-game.sh --linux-local-test-passed
```

Required public ports are TCP **22** (SSH), **80** (HTTP-01 challenge only), **443** (HTTPS API) and UDP **7777** (authenticated DTLS game traffic). HTTP otherwise returns 404. Nginx never forwards `/internal...` routes publicly, including case variations. No router forwarding is needed by joining players. Do not disable certificate validation, OS firewalls or clients' network protection.

## Trusted IP certificate and renewal

The scripts first exercise HTTP-01 against Let's Encrypt staging in a **separate** lineage. Production issuance uses its short-lived IP profile and an RSA certificate for compatibility with the transport. It verifies the chain and IP SAN using system trust roots before deployment; staging certificates are never installed in the game or HTTPS site.

On November 3, 2026 the [official CA status page](https://letsencrypt.status.io/) reported staging offline during scheduled maintenance through November 4 at 21:00 UTC. Its staging directory reset/timed out from this host while the production directory returned 200. In this documented situation only, `configure-certificate.sh --production-only-maintenance` permits one production attempt **after external HTTP-01 webroot reachability is independently verified**. This skips the unavailable staging rehearsal, without bypassing HTTPS/certificate validation. Staging/renewal dry-run verification remains pending until maintenance ends; do not rapidly retry production issuance.

The free certificate is valid for about six days. No contact email was supplied; this pilot registers without one and relies on its scheduled renewal/expiry checks. Renewal checks run every six hours, with jitter, through `pointpass-cert-renew.timer`. A daily expiry check fails and logs an error if less than 48 hours remain. A failed systemd service is visible locally; no external alert service or email delivery is configured.

The deployment hook validates the new chain, copies only the required certificate/key into the match worker's private TLS directory and writes `tls-reload.request`. The worker must load that marker at the next **idle** transport restart; an active match is not interrupted for certificate renewal. Nginx reloads immediately. Check that the worker has acknowledged the marker and uses the new certificate during operational validation. The six-day lifetime makes renewal health essential.

```sh
sudo systemctl list-timers 'pointpass-*'
sudo systemctl status pointpass-cert-renew.service pointpass-cert-check.service
sudo journalctl -u pointpass-cert-renew.service --since '2 days ago'
sudo /opt/pointpass/bin/certificate-expiry-check.sh
sudo /opt/pointpass-certbot/bin/certbot renew --dry-run --cert-name pointpass-ip
curl --fail https://62.238.115.207/health
```

Do not use `curl -k`, accept-all certificate handlers, or a staging root as a workaround. HTTPS success alone does not prove the Unity DTLS path: test both actual Windows clients.

## Service operations

```sh
sudo systemctl status pointpass-api pointpass-match postgresql@18-main nginx
sudo journalctl -u pointpass-match --since '10 minutes ago'
sudo journalctl -u pointpass-api --since '10 minutes ago'
sudo ss -lntup
sudo ufw status verbose
```

A restricted administration account can use only its explicitly approved fixed service-control helper. It must not be given arbitrary shell, file, systemctl-unit, or environment arguments through that helper. General rollout/OS administration still requires an approved recovery administrator.

`deploy-release.sh` stops matches for maintenance, backs up existing data, applies new migrations, atomically switches the release symlink, verifies API health and starts the worker. A failed rollout restores previous binaries when available; it never silently reverses database migrations. Keep compatible rollback binaries until the release is verified. It refuses new deployments with less than 4 GiB free, more than 8 GiB of existing releases, or more than 2 GiB of staged binaries. Review and remove old inactive release directories only after inspecting the resolved paths and confirming `/opt/pointpass/current` does not reference them. No automated worker spawning or prewarming exists.

To create a single-use, 24-hour invitation file without printing its code:

```sh
sudo /opt/pointpass/bin/create-invite.sh tester-one
```

The helper reads the protected server environment and invokes the backend's `--create-invite --invite-file ABSOLUTE_PATH` administration command under `pointpassapi`. It prints only the private file path. Read/deliver the code privately to the participant using an approved secure channel; never paste it into shared logs or commit it. Guest credentials are client-local and must not be copied into the deployed build. Remove expired invitation **files** when no longer needed; doing so does not delete profiles or match history.

## Backups and restore

`pointpass-backup.timer` runs daily. Custom-format compressed dumps are root-readable only under `/var/backups/pointpass`. Each successful dump is limited to **1 GiB** and at most **seven** successful dumps are retained; frequent manual/pre-deploy backups can replace older daily points. A backup is skipped if less than 3 GiB remains free. These limits reserve at most 7 GiB for successful dumps, plus at most 1 GiB for the in-progress dump. An unexpectedly large database fails visibly rather than filling the disk.

```sh
sudo /opt/pointpass/bin/backup.sh
sudo /opt/pointpass/bin/restore-check.sh /var/backups/pointpass/pointpass-TIMESTAMP.dump
```

The restore check creates a disposable database, restores the dump, counts persisted players, and removes only that disposable database. It never overwrites the pilot database. For an actual recovery, stop the worker/API, preserve the failed database for investigation, restore into a new database under `pointpassowner`, verify player/result counts, and explicitly change the API database name after owner review. Do not pipe a dump blindly onto the live database.

These backups are on the **same SSD**. They protect against some application mistakes and bad deployments, not server loss or account compromise. No paid snapshot or external storage has been enabled. Off-host encrypted backups require a separately approved destination and cost assessment.

## Resource measurements and retention

Run `collect-metrics.sh 60` once while waiting with zero clients and again during a real two-player match. Compare service CPU deltas, resident memory, network counters and worker simulation metrics; interface totals include SSH/API traffic and do not by themselves establish bandwidth per player. Record Unity frame/physics timings and per-connection bytes from the actual match. Report the tested capacity as one concurrent 1v1 match only after that test passes.

The application units cap the API at 512 MiB/one CPU and the worker at 2 GiB/two CPUs; PostgreSQL is capped at 768 MiB/one CPU. These are containment limits, not measured usage or scaling promises. Persistent journal storage is bounded to 256 MiB/seven days with app log rate limits; Nginx logs rotate daily at 10 MiB with seven files and exclude query strings, bodies, tokens and authorization headers. The database's persistent player/history data is separate from disposable logs and must be monitored for growth. Review inactive release sizes after deployments.

## Costs and current validation

No new recurring service is required by this design. The **existing Hetzner server and any existing provider charges remain payable**. Let's Encrypt certificates and the deployed open-source runtime/database have no per-user service subscription. Provider traffic beyond the account's included allowance, backups/snapshots if later enabled, storage upgrades and additional machines may cost money. Confirm the account's actual plan/traffic allowance in its dashboard; it is not encoded or inferred here. No paid add-on is enabled by these scripts.

The presence of scripts does not mean deployment or tests have passed. Record actual service status, certificate validation, security regressions, idle/active measurements, two Windows clients and restart persistence in the final delivery report. Android/iOS physical-device testing remains unavailable and must not be claimed.

Official references checked for this implementation:

- [Let's Encrypt: IP certificates, Certbot 5.4+ and webroot workflow](https://letsencrypt.org/2026/03/11/shorter-certs-certbot)
- [Let's Encrypt: generally available six-day IP certificates](https://letsencrypt.org/2026/01/15/6day-and-ip-general-availability)
- [Certbot virtual-environment installation](https://certbot.eff.org/instructions?os=pip&tab=wildcard&ws=other)
- [Certbot supported installation methods](https://eff-certbot.readthedocs.io/en/stable/install.html)
- [Microsoft: .NET on Ubuntu](https://learn.microsoft.com/dotnet/core/install/linux-ubuntu-install)
- [PostgreSQL 18 authentication](https://www.postgresql.org/docs/18/auth-methods.html)
- [systemd service sandbox options](https://freedesktop.org/software/systemd/man/latest/systemd.exec.html)
