# AshmesMarketplaces Docker VPS Runbook

This runbook covers the first production Docker MVP for one Ubuntu VPS. It is intentionally simple: Docker Compose runs the web stack, Caddy is the only public entrypoint, and operational actions stay manual.

## 1. Deployment Overview

The production stack runs on a single VPS with Docker Compose:

- `proxy`: Caddy, public ports `80` and `443`.
- `frontend`: static Vue build, internal port `80`.
- `api`: .NET API, internal port `8080`, public only through `/api/*`.
- `intelligence`: Python FastAPI, internal port `8020`.
- `analytics-worker`: .NET background worker for scheduled heavy analytics, internal-only with no public ports.
- `postgres`: PostgreSQL, internal port `5432`, persistent volume.
- `migrator`: manual-only EF migration runner under the `tools` profile.

Public routing:

- `https://<ASHMES_PUBLIC_HOST>/` -> frontend.
- `https://<ASHMES_PUBLIC_HOST>/api/*` -> API without stripping `/api`.

No public route exists for PostgreSQL, Intelligence, or the analytics worker.

## 2. Manual VPS Prerequisites

Before deploying:

- Buy or provision a VPS.
- Install Ubuntu 24.04 LTS or 22.04 LTS.
- Create or know the SSH user you will use.
- Point a DNS A record to the VPS, for example `<your-app-host> -> VPS_IP`.
- Open only inbound ports:
  - `22` for SSH.
  - `80` for HTTP and Caddy certificate challenges.
  - `443` for HTTPS.
- Do not open:
  - `5432` PostgreSQL.
  - `8080` API.
  - `8020` Intelligence.
  - `5173` Vite dev server.

## 3. Install Docker On VPS

Use Docker's official Ubuntu installation flow. A compact version is:

```bash
sudo apt-get update
sudo apt-get install -y ca-certificates curl
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu \
  $(. /etc/os-release && echo "${UBUNTU_CODENAME:-$VERSION_CODENAME}") stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
docker compose version
```

If you do not add your user to the Docker group, run Docker commands with `sudo`.

## 4. Clone Repo / Copy Release

Two MVP deployment options:

- Clone the repository on the VPS.
- Upload a reviewed release archive and extract it on the VPS.

Cloning the private repository is the simplest first path if credentials are available. A release archive is preferable once releases become formal.

Run all commands below from the repository root.

Make helper scripts executable after cloning or unpacking a release:

```bash
chmod +x deploy/docker/scripts/*.sh
```

## 5. Configure Environment

Create the real env file:

```bash
cp deploy/docker/.env.example deploy/docker/.env
```

Edit `deploy/docker/.env` and replace every placeholder.

Required values:

- `COMPOSE_PROJECT_NAME`: Compose project name, default placeholder is fine if one stack runs on the VPS.
- `ASHMES_PUBLIC_HOST`: host only, no scheme, for example your app host.
- `POSTGRES_DB`: database name.
- `POSTGRES_USER`: database user.
- `POSTGRES_PASSWORD`: strong database password.
- `JWT_ISSUER`: full HTTPS origin, for example `https://<ASHMES_PUBLIC_HOST>`.
- `JWT_AUDIENCE`: full HTTPS origin, usually same as issuer for MVP.
- `JWT_SIGNING_KEY`: long random secret. Generate a strong value outside git.
- `JWT_ACCESS_TOKEN_LIFETIME_MINUTES`: default `15`.
- `JWT_REFRESH_TOKEN_LIFETIME_DAYS`: default `30`.
- `INTELLIGENCE_ENABLED`: `true` for the D2/D3 stack.
- `INTELLIGENCE_LOG_LEVEL`: default `INFO`.

Never commit `deploy/docker/.env`. It contains production secrets. The committed `deploy/docker/.env.example` must contain placeholders only.

## 6. Build

Run:

```bash
./deploy/docker/scripts/check-prerequisites.sh
./deploy/docker/scripts/build.sh
```

Raw command equivalent:

```bash
docker compose -f deploy/docker/compose.prod.yml --env-file deploy/docker/.env build
```

## 7. Start Infrastructure

Recommended first deployment sequence:

```bash
docker compose -f deploy/docker/compose.prod.yml --env-file deploy/docker/.env up -d postgres
./deploy/docker/scripts/migrate.sh
./deploy/docker/scripts/up.sh
```

The API container does not apply migrations automatically. `up.sh` also does not run migrations. `up.sh` starts `analytics-worker` together with the API, frontend, intelligence service, and proxy.

After the top forecast migration, the public schedule `top_forecast_public` may already be due. On the first start after migration, `analytics-worker` can immediately begin the heavy forecast calculation. Watch worker and intelligence logs plus `docker stats` during that first run.

For later deployments:

```bash
./deploy/docker/scripts/build.sh
./deploy/docker/scripts/migrate.sh
./deploy/docker/scripts/up.sh
```

Run the migrator only when applying reviewed migrations. The D2 migrator restores `dotnet-ef` at runtime from `.config/dotnet-tools.json`; this is acceptable for the MVP but should be replaced later by a dedicated migrator image or EF bundle.

To stop the stack without deleting volumes:

```bash
./deploy/docker/scripts/down.sh
```

`down.sh` does not remove Docker volumes.

## 8. Logs

All logs:

```bash
./deploy/docker/scripts/logs.sh
```

Service-specific logs:

```bash
./deploy/docker/scripts/logs.sh api
./deploy/docker/scripts/logs.sh proxy
./deploy/docker/scripts/logs.sh frontend
./deploy/docker/scripts/logs.sh intelligence
./deploy/docker/scripts/logs.sh analytics-worker
./deploy/docker/scripts/logs.sh postgres
```

## 9. Smoke Checks

Run:

```bash
./deploy/docker/scripts/smoke.sh
```

The smoke script:

- prints `docker compose ps`;
- checks the public frontend URL;
- checks `GET /api/v1/health`;
- prints internal Docker health status for the production services, including `analytics-worker`.

`analytics-worker` has no HTTP health endpoint. For the worker, Docker running/restart status and logs are the readiness signals.

A fresh database may return empty valid responses for Market Analytics and recommendations until parser staging and recalculation are performed later. The smoke script does not depend on those data-heavy endpoints.

If DNS is not propagated, ports `80/443` are blocked, or Caddy has not obtained a certificate yet, smoke checks may report HTTPS/TLS readiness problems. That is not automatically an application failure.

## 10. Database Bootstrap

Option A: fresh DB

```bash
docker compose -f deploy/docker/compose.prod.yml --env-file deploy/docker/.env up -d postgres
./deploy/docker/scripts/migrate.sh
./deploy/docker/scripts/up.sh
sudo ./deploy/docker/scripts/bootstrap-admin.sh
```

The admin credentials are written to `/etc/ashmes/admin-credentials.env` with `600` permissions.
The application starts with an empty production database except for the initial admin user and workspace.
Parser staging and public analytics calculations are later operational stages. Scheduled analytics are executed by `analytics-worker`.

Option B: restore a local demo DB

1. Create a dump from the source database.
2. Copy the dump to the VPS.
3. Start PostgreSQL.
4. Restore with:

```bash
./deploy/docker/scripts/restore-postgres.sh path/to/backup.dump
```

Do not enable development seed in production unless explicitly approved.

## 11. Parser Worker

The production parser is a private Docker worker under the `worker` profile. It publishes no ports, sends completed batches to the API queue, and writes artifacts/checkpoints/outbox files to `/var/lib/ashmes/parser`. Normal production parser ingestion does not use a direct PostgreSQL connection.

```bash
./deploy/docker/scripts/parser-start.sh
./deploy/docker/scripts/parser-status.sh
./deploy/docker/scripts/parser-logs.sh
./deploy/docker/scripts/parser-stop.sh
```

The parser uses `Parser/presets/production/proxy_mapping.local.json` when that
file exists next to the tracked fallback `proxy_mapping.json`. Keep the local
file out of git because it can contain real proxy credentials.

One-batch production smoke command inside the parser image:

```bash
docker compose -f deploy/docker/compose.prod.yml --env-file deploy/docker/.env --profile worker run --rm parser \
  python Parser/app/cycle_runner.py \
  --config Parser/presets/production/market_refresh_selected_niches_batched.prod.json \
  --mode batched_full_enrichment \
  --smoke-max-batches 1 \
  --smoke-source-subcategory "Коврики для ванной"
```

Smoke acceptance:

- a row appears in `ParserBatchSubmissions`;
- raw artifacts appear and are removed after successful server processing;
- local outbox payload is removed only after server status `completed`;
- current rows and CDC events are written by `analytics-worker`;
- admin parser monitoring shows instance id, niche, proxy key, batch status and errors.

## 12. Manual Backup

Create a backup:

```bash
./deploy/docker/scripts/backup-postgres.sh
```

Backups are written to:

```text
deploy/docker/backups/
```

The backup script uses restrictive permissions where practical and writes timestamped custom-format `.dump` files. It refuses to overwrite an existing file.

Restore a backup:

```bash
./deploy/docker/scripts/restore-postgres.sh deploy/docker/backups/<file>.dump
```

Restore is destructive. The script verifies the dump exists, prints the target database and Compose project, and runs only if you type the exact confirmation word `restore`.

## 12. Updating Deployment

Safe update flow:

1. Pull new code or upload a new release archive.
2. Review changed migrations if any.
3. Run `./deploy/docker/scripts/build.sh`.
4. Run `./deploy/docker/scripts/backup-postgres.sh` before schema changes.
5. Run `./deploy/docker/scripts/migrate.sh` when reviewed migrations changed.
6. Run `./deploy/docker/scripts/up.sh`.
7. Run `./deploy/docker/scripts/smoke.sh`.

Do not run parser jobs or ad hoc database commands from the Docker MVP stack during update. Scheduled analytics must run only through `analytics-worker`.

## 13. Troubleshooting

Caddy cannot get a certificate:

- DNS A record may not point to the VPS yet.
- Ports `80` or `443` may be blocked.
- `ASHMES_PUBLIC_HOST` may include a scheme. It must be host-only.

API cannot connect to DB:

- Check `deploy/docker/.env`.
- Check `postgres` health.
- Check API logs with `./deploy/docker/scripts/logs.sh api`.

Frontend works but API returns 502:

- API container may be down.
- Caddy may not be able to reach `api:8080`.
- Confirm `/api/*` routing still uses `handle`, not `handle_path`.

Intelligence unavailable:

- Check `intelligence` container status and logs.
- Confirm API uses `Intelligence__BaseUrl=http://intelligence:8020`.

Analytics worker high memory or busy:

- Check worker logs with `./deploy/docker/scripts/logs.sh analytics-worker`.
- First deploy after the top forecast migration can immediately run `top_forecast_public`.
- API should remain a lightweight HTTP service; heavy memory should be isolated to `analytics-worker` and `intelligence`.
- If the first forecast job fails, public pages should remain available and the previous successful snapshots stay in the database.

Migrator fails:

- PostgreSQL may not be healthy.
- Connection string values may be wrong.
- Migration history may not match the target database.
- D2 migrator runtime tool restore may fail if the VPS cannot reach NuGet.

Empty recommendations:

- Fresh database has no parser staging data.
- Hot-products recalculation may not have been run.
- Empty recommendation results can be valid.

Health check fails:

- Check `https://<ASHMES_PUBLIC_HOST>/api/v1/health`.
- Check API logs with `./deploy/docker/scripts/logs.sh api`.
- Confirm the API container is healthy in `docker compose ps`.

## 14. Security Checklist

- Only `22`, `80`, and `443` are public.
- `5432`, `8080`, `8020`, and `5173` are not public.
- `deploy/docker/.env` is never committed.
- `deploy/docker/.env.example` contains placeholders only.
- PostgreSQL password is strong.
- JWT signing key is long and random.
- Development seed remains disabled.
- PostgreSQL is internal-only.
- Intelligence is internal-only.
- Analytics worker is internal-only and has no public ports.
- Swagger should remain disabled outside Development and be reviewed again in production hardening.
- Manual backups are enabled before risky operations.
