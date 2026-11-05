# PointPass pilot recurring costs

Checked on 5 November 2026. This is a budget explanation, not an account invoice. The existing Helsinki server stays at 4 vCPU, 8 GB RAM and 80 GB SSD; this audit does not purchase, resize or enable services.

## Current base and possible extras

The owner reports **10.19 per month**, plus surplus usage charges. The currency, tax treatment, exact plan and whether IPv4 is already included were not supplied. Use the owner's invoice as the base amount; do not add tax or IPv4 twice.

| Item | Recurring cost / status |
| --- | --- |
| Existing server | Owner-reported 10.19/month. Still payable when idle or powered off; stopping the game does not stop the server charge. |
| Public IPv4 | Hetzner publishes an additional EUR 0.50/month excluding VAT. Confirm whether the reported base already includes it. |
| Traffic | EU CX/CPX plans publish 20 TB included outgoing traffic and EUR 1.00 per additional TB excluding VAT. Confirm this account's allowance/rate/currency before estimating an invoice. |
| Existing 80 GB root disk | Included in the existing server shape. Database, logs, releases and local dumps consume that capacity; filling it can cause failures, rather than automatically buying more space. |
| Current local PostgreSQL dumps | No separate provider-storage subscription is introduced by these scripts. The dumps consume the same existing SSD and do not protect against losing that SSD/server. |
| Provider Backups | Optional and not enabled by this audit. Hetzner publishes 20% of the server plan price for seven backup slots; this differs from the application's local database dumps. |
| Snapshots / extra volumes / off-host backup storage | Optional, separately charged resources. Snapshot billing uses compressed GB-months; volumes use provisioned capacity and time. Actual account inventory/pricing and an approved destination are needed before any total can include them. |
| Certificate and application services | Let's Encrypt, the API/database and the deployed runtime introduce no new per-player hosted-service subscription here. This does not establish the project's separate Unity commercial licensing obligations. |
| Domain | No domain is used or purchased. The pilot uses a trusted IP certificate. |

Server/IPv4 billing, outgoing-only traffic, 100 MB overage billing blocks, backup pricing and account-price changes are explained in the [Hetzner billing FAQ](https://docs.hetzner.com/cloud/billing/faq/). The IPv4 amount is published in [server networking](https://docs.hetzner.com/cloud/servers/overview/). The EU allowance and additional-TB rate appear on the [official cost-optimized plan page](https://www.hetzner.com/cloud/cost-optimized/). Those public EUR figures do not establish this owner's contract, currency or VAT.

## Usage estimate

The monthly total is the confirmed invoice base, plus billable traffic above its confirmed allowance, plus any existing separately billed resources, with the account's applicable tax treatment. No automatic paid server scaling is implemented; only one concurrent 1v1 room is configured. CPU/RAM saturation does not establish that more matches are affordable or playable.

For a EUR account at the public EU overage rate, each additional billable TB is approximately EUR 1.00 before tax, rounded according to the provider's traffic-billing units. Incoming traffic is free. Provider warnings at 75% and 100% usage are notifications, not a spending cap. Confirm invoice/Console usage rather than multiplying a game packet counter into a guaranteed bill. See the [traffic billing rules](https://docs.hetzner.com/cloud/billing/faq/#how-do-you-bill-for-traffic).

Game payload bytes exclude IP/UDP/DTLS and reliable-transport overhead. A full network estimate must also include API, downloads, deployment/SSH and other applications sharing the host. A measured average application egress of `r` bytes/second/player over `H` total player-hours gives `r × H × 3600 / 1e12` application TB; use actual provider or interface measurements to account for overhead. This formula is not a player-capacity claim.

## Storage decisions and remaining account facts

The application retains at most seven successful local dumps of at most 1 GiB each, plus at most one current in-progress dump. Frequent manual/pre-deploy backups can replace older daily restore points. Journals, active logs, database/WAL growth, abandoned partial files and inactive releases still need measurement. A logrotate size threshold is evaluated when logrotate runs; it is not a hard instantaneous file-size cap.

Off-host encrypted backups would address a different failure mode from same-disk dumps, but require an approved destination, retention policy and cost quote. No paid storage was enabled by this audit. Confirm existing provider backup/snapshot/volume subscriptions in the owner's Console; repository scripts alone cannot prove their billing state. Hetzner documents [snapshot/backup billing](https://docs.hetzner.com/cloud/billing/faq/#how-do-you-bill-for-snapshots-and-backups) and [volume billing](https://docs.hetzner.com/cloud/volumes/overview/).

Still needed for an exact recurring total: invoice currency, actual plan/legacy tariff, VAT, IPv4 inclusion, monthly billable egress/allowance and current optional-resource inventory. Do not infer these from the server's CPU/RAM/disk size.
