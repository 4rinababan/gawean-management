# Deploying to a VPS

The production stack is three containers on one host: the app, PostgreSQL, and Caddy
(which terminates TLS and reverse-proxies to the app). CI builds the image and pushes it
to GHCR; a push to `main` then SSHes into the VPS and runs `docker compose pull && up`.

## 1. One-time VPS setup

```bash
# On the VPS (Ubuntu 22.04+)
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER   # log out/in afterwards

sudo mkdir -p /opt/taskmanagement
sudo chown $USER /opt/taskmanagement
cd /opt/taskmanagement

# Copy these three files from the repo's deploy/ folder:
#   docker-compose.prod.yml, Caddyfile, .env.example
cp .env.example .env
nano .env    # set DOMAIN, TLS_EMAIL, POSTGRES_PASSWORD, SMTP_*, OAuth secrets
```

Point a DNS **A record** for `DOMAIN` at the VPS's public IP. Open ports 80 and 443.

## 2. GitHub repository secrets

Settings → Secrets and variables → Actions:

| Secret          | Purpose                                                        |
| --------------- | ------------------------------------------------------------- |
| `VPS_HOST`      | VPS IP or hostname                                             |
| `VPS_USER`      | SSH user (member of the `docker` group)                        |
| `VPS_SSH_KEY`   | Private key whose public half is in the user's `authorized_keys` |

`GITHUB_TOKEN` is provided automatically and is used to push the GHCR image and to
`docker login` on the VPS during that same deploy job. For manual `docker compose pull`
on the VPS later, either make the GHCR package **public**
(github.com/users/4rinababan/packages → gawean-management → Package settings → Change
visibility) or add a VPS-side PAT with `read:packages`.

This repo deploys `ghcr.io/4rinababan/gawean-management` to `ryugasolusindoproject.com`
on the VPS at `103.89.7.185` (SSH user `cashflow`).

## 3. First deploy

Push to `main` (or run the **CD** workflow manually). The workflow:

1. builds the multi-stage Docker image and pushes `ghcr.io/<owner>/taskmanagement:sha-<commit>` + `:latest`;
2. SSHes to the VPS and runs `docker compose -f docker-compose.prod.yml pull && up -d`.

On startup the app runs `Database.Migrate()` (`RunMigrationsOnStartup=true` in the compose
file), so the schema is created/updated automatically. Data Protection keys live in the
`keys` volume, so redeploys don't sign users out.

## 4. Operations

```bash
cd /opt/taskmanagement
docker compose -f docker-compose.prod.yml logs -f web
docker compose -f docker-compose.prod.yml ps
curl -fsS https://$DOMAIN/health          # => Healthy

# Backups
docker compose -f docker-compose.prod.yml exec db pg_dump -U $POSTGRES_USER taskmanagement > backup-$(date +%F).sql
```

Attachments are stored in the `uploads` volume; include it in your backup routine
(or switch `IFileStorage` to an object store).
