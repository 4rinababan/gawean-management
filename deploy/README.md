# Deploying to a VPS

Three containers on one host: the app, PostgreSQL, and Caddy (which terminates TLS and
reverse-proxies to the app). The GitHub Actions runner is installed directly on the VPS as a
**self-hosted runner**, so a push to `main` runs the test suite, then builds the Docker image
and restarts the stack on that same host — no image registry, no SSH step, no deploy secrets.

This repo deploys **gawean.web.id** on the VPS at **103.89.7.185**.

## What lives where

| File | Source of truth | Notes |
| --- | --- | --- |
| `docker-compose.prod.yml` | the repo | checked out fresh by the runner on **every** deploy — do not edit on the VPS, the change would be overwritten |
| `Caddyfile` | the repo | same |
| `.env` | **the VPS only** | holds the secrets; never committed, never overwritten by a deploy |

## 1. DNS

Point an **A record** at the VPS before anything else — Caddy cannot obtain a certificate until
the name resolves, and Let's Encrypt rate-limits repeated failures.

```
gawean.web.id.      A    103.89.7.185
www.gawean.web.id.  A    103.89.7.185     (optional)
```

Check it has propagated: `nslookup gawean.web.id` should answer `103.89.7.185`.

## 2. One-time VPS setup

Docker, and a GitHub Actions self-hosted runner registered to this repo, both need to already be
on the host (Settings → Actions → Runners → New self-hosted runner walks through installing and
starting the runner service). The runner's user must be in the `docker` group:

```bash
sudo usermod -aG docker $USER      # then restart the runner service for this to take effect
```

Create the deploy directory and `.env` there (the deploy never touches this file):

```bash
sudo mkdir -p /opt/taskmanagement
sudo chown $USER /opt/taskmanagement
cd /opt/taskmanagement

cat > .env <<'EOF'
IMAGE=gawean-management:latest
DOMAIN=gawean.web.id
TLS_EMAIL=admin@gawean.web.id

POSTGRES_USER=gawean
POSTGRES_PASSWORD=REPLACE-WITH-A-LONG-RANDOM-STRING
POSTGRES_DB=taskmanagement

SMTP_HOST=mail.ryugasolusindo.com
SMTP_PORT=465
SMTP_SECURITY=SslOnConnect
SMTP_USER=noreply@ryugasolusindo.com
SMTP_PASSWORD=REPLACE-ME
SMTP_FROM=noreply@ryugasolusindo.com
SMTP_FROM_NAME=GaWeAn

AI_ENDPOINT=https://api.biznetgio.ai/v1
AI_API_KEY=
AI_MODEL=openai/gpt-oss-20b

# Optional: S3-compatible bucket for attachments (AWS S3, R2, B2, MinIO...). Leave blank to keep
# storing attachments on this VPS's disk.
S3_ENDPOINT=
S3_REGION=
S3_BUCKET=
S3_ACCESS_KEY=
S3_SECRET_KEY=
EOF

chmod 600 .env
```

Generate the database password with `openssl rand -base64 32` rather than inventing one.

Ports 80 and 443 must be reachable from the internet. If `ufw` is active:

```bash
sudo ufw allow 80,443/tcp
```

## 3. Deploy

Push to `main`, or run the **Build, Test & Deploy** workflow manually from the Actions tab. It
runs, in order:

1. **test** — the same build and full test suite a pull request runs (on a GitHub-hosted
   runner);
2. **build-deploy** — on the self-hosted runner: builds the image (`docker build`), runs
   `docker compose -f deploy/docker-compose.prod.yml --env-file /opt/taskmanagement/.env up -d`,
   then waits for the container's healthcheck. If the app is not healthy within ~200s the job
   fails and prints the last 80 log lines. `docker image prune -f` clears the superseded image
   afterwards.

The schema is created and migrated on startup (`RunMigrationsOnStartup=true`), so there is no
separate migration step. Data Protection keys live in the `keys` volume, so a redeploy does not
sign anyone out.

First run takes a few minutes: Caddy has to obtain the certificate from Let's Encrypt.

## 4. Verify

```bash
curl -fsS https://gawean.web.id/health     # => Healthy
```

Then open https://gawean.web.id, register the first account, and create a workspace.

## 5. Operations

```bash
cd /opt/taskmanagement
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs -f web
docker compose -f docker-compose.prod.yml logs -f caddy    # certificate problems show up here

# The db-backup service dumps the database daily on its own (7 daily / 4 weekly / 6 monthly kept) —
# no cron needed. Dumps land in the db_backups volume, still on this host:
docker volume inspect deploy_db_backups --format '{{ .Mountpoint }}'
docker compose -f docker-compose.prod.yml logs db-backup   # confirm it's actually running

# One-off manual dump (e.g. right before a risky change), independent of the schedule above:
docker compose -f docker-compose.prod.yml exec -T db \
  pg_dump -U gawean taskmanagement > backup-$(date +%F).sql
```

The `db-backup` service only protects against a bad migration or an accidental delete — the dumps
still live on this one disk. Copy them off-host periodically (e.g. a cron `rsync`/`rclone` job, or
point `S3_*` in `.env` at a bucket and switch attachments to it) for real disaster recovery.

Attachments live in the `uploads` volume — include it in the backup routine, or switch
`IFileStorage` to an object store.

## Troubleshooting

**Certificate not issued.** Check DNS actually resolves to this host and that ports 80/443 are
open; `docker compose logs caddy` states the reason. Let's Encrypt rate-limits repeated
failures for the same name, so fix DNS before retrying.

**Deploy fails at the health wait.** The app started but is unhealthy — almost always the
database connection or a bad value in `.env`. The job prints the web logs; `docker compose logs
web` on the VPS shows the same.

**`docker: permission denied`.** The runner's user is not in the `docker` group yet, or its
session predates `usermod`. Restart the self-hosted runner service.

**Runner offline / job stuck queued.** Check the runner's status under Settings → Actions →
Runners, and that its service is running on the VPS (`sudo systemctl status actions.runner.*`).

**Email not sending.** Port 465 is blocked on some networks. Switch `.env` to
`SMTP_PORT=587` with `SMTP_SECURITY=StartTls` and restart. Email failures are logged and never
block the operation that triggered them, so invitations still work via their **Copy link** button.
