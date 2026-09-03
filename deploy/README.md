# Deploying to a VPS

Three containers on one host: the app, PostgreSQL, and Caddy (which terminates TLS and
reverse-proxies to the app). A push to `main` runs the test suite, builds the image, pushes it
to GHCR, ships this folder to the VPS, and restarts the stack — failing the deploy if the app
does not come up healthy.

This repo deploys `ghcr.io/4rinababan/gawean-management` to **gawean.web.id** on the VPS at
**103.89.7.185** (SSH user `cashflow`).

## What lives where

| File | Source of truth | Notes |
| --- | --- | --- |
| `docker-compose.prod.yml` | the repo | copied to the VPS on **every** deploy — do not edit on the VPS, the change would be overwritten |
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

```bash
ssh cashflow@103.89.7.185

curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER      # then log out and back in for this to take effect

sudo mkdir -p /opt/taskmanagement
sudo chown $USER /opt/taskmanagement
cd /opt/taskmanagement
```

Create `.env` there (the deploy never touches this file):

```bash
cat > .env <<'EOF'
IMAGE=ghcr.io/4rinababan/gawean-management:latest
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
EOF

chmod 600 .env
```

Generate the database password with `openssl rand -base64 32` rather than inventing one.

Ports 80 and 443 must be reachable from the internet. If `ufw` is active:

```bash
sudo ufw allow 80,443/tcp
```

## 3. GitHub repository secrets

Settings → Secrets and variables → Actions → New repository secret:

| Secret | Value |
| --- | --- |
| `VPS_HOST` | `103.89.7.185` |
| `VPS_USER` | `cashflow` |
| `VPS_SSH_KEY` | the **private** key whose public half is in `~/.ssh/authorized_keys` on the VPS |

`VPS_SSH_KEY` is the entire file including the `-----BEGIN…` and `-----END…` lines. If the
provider gave you a `.pem` download, paste its contents verbatim.

`GITHUB_TOKEN` is provided automatically — it pushes the image and authenticates the VPS-side
`docker pull` during the same job. No PAT is needed for the automated deploy. For a **manual**
`docker compose pull` on the VPS later, either make the GHCR package public (github.com/users/
4rinababan/packages → gawean-management → Package settings → Change visibility) or log in there
once with a PAT that has `read:packages`.

## 4. Deploy

Push to `main`, or run the **CD** workflow manually from the Actions tab. It runs, in order:

1. **test** — the same build and full test suite a pull request runs;
2. **build-image** — multi-stage Docker build, pushed as `:sha-<commit>` and `:latest`;
3. **deploy** — copies `docker-compose.prod.yml` and `Caddyfile` to the VPS, pulls the exact
   commit's image, restarts the stack, then waits for the container's healthcheck. If the app
   is not healthy within ~200s the job fails and prints the last 80 log lines.

The schema is created and migrated on startup (`RunMigrationsOnStartup=true`), so there is no
separate migration step. Data Protection keys live in the `keys` volume, so a redeploy does not
sign anyone out.

First run takes a few minutes: Caddy has to obtain the certificate from Let's Encrypt.

## 5. Verify

```bash
curl -fsS https://gawean.web.id/health     # => Healthy
```

Then open https://gawean.web.id, register the first account, and create a workspace.

## 6. Operations

```bash
cd /opt/taskmanagement
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs -f web
docker compose -f docker-compose.prod.yml logs -f caddy    # certificate problems show up here

# Backup (run from a cron job; keep the dumps off this host)
docker compose -f docker-compose.prod.yml exec -T db \
  pg_dump -U gawean taskmanagement > backup-$(date +%F).sql
```

Attachments live in the `uploads` volume — include it in the backup routine, or switch
`IFileStorage` to an object store.

## Troubleshooting

**Certificate not issued.** Check DNS actually resolves to this host and that ports 80/443 are
open; `docker compose logs caddy` states the reason. Let's Encrypt rate-limits repeated
failures for the same name, so fix DNS before retrying.

**Deploy fails at the health wait.** The app started but is unhealthy — almost always the
database connection or a bad value in `.env`. The job prints the web logs; `docker compose logs
web` on the VPS shows the same.

**`docker: permission denied`.** The SSH user is not in the `docker` group yet, or the session
predates `usermod`. Log out and back in.

**Email not sending.** Port 465 is blocked on some networks. Switch `.env` to
`SMTP_PORT=587` with `SMTP_SECURITY=StartTls` and restart. Email failures are logged and never
block the operation that triggered them, so invitations still work via their **Copy link** button.
