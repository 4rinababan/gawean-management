# Taskflow — a JIRA-style task manager

Multi-tenant issue tracking for teams: workspaces, projects, issues, a drag-and-drop
Kanban board, sprints with burndown, comments, attachments, and real-time notifications.

Built with **Blazor Web App (.NET 10, Interactive Server)**, **Tailwind CSS**,
**PostgreSQL + EF Core**, and **ASP.NET Core Identity** (email/password plus Google &
GitHub sign-in).

## Solution layout

| Project | Responsibility |
| --- | --- |
| `TaskManagement.Domain` | Entities, enums, and pure domain logic (lexo-rank ordering, sprint state machine, burndown, the role→permission matrix). No external dependencies. |
| `TaskManagement.Application` | Use-case services, DTOs, FluentValidation validators, and the abstractions (`IAppDbContext`, `ITenantContext`, `IEmailSender`, `IFileStorage`, …) the outer layers implement. |
| `TaskManagement.Infrastructure` | EF Core `AppDbContext` (Identity + domain, one database) with per-tenant global query filters, migrations, SMTP email (MailKit), local-disk file storage. |
| `TaskManagement.Web` | Blazor components, reusable Tailwind UI library (`Components/Ui`), pages, the SignalR notification hub, and the web implementations of the application abstractions. |
| `tests/*` | xUnit + FluentAssertions (domain & application), Testcontainers-PostgreSQL + SQLite (infrastructure), bUnit (components). |

Multi-tenancy is a shared database keyed by `OrganizationId`; `AppDbContext` applies a
global query filter from the ambient `ITenantContext`, which `TenantResolver` populates
from the `/{slug}/…` route after checking membership.

## Local development

Prerequisites: .NET 10 SDK, Node 20, Docker (for PostgreSQL and the Testcontainers tests).

```bash
# 1. Database
docker compose -f deploy/docker-compose.yml up -d

# 2. Tailwind (watch mode, separate terminal)
cd src/TaskManagement.Web && npm install && npm run css:watch

# 3. App (migrations apply automatically in Development)
dotnet watch --project src/TaskManagement.Web
```

The default connection string points at the compose database. Set OAuth / SMTP secrets
with user-secrets on `TaskManagement.Web` if you want to exercise those paths.

## Tests

```bash
dotnet test                     # all layers
dotnet test tests/TaskManagement.Domain.Tests
```

The PostgreSQL integration tests start a throw-away container via Testcontainers and
skip themselves when no container runtime is available.

## Deployment

CI/CD is GitHub Actions → GHCR → SSH to a VPS running `docker compose`, with Caddy for
automatic HTTPS. See [`deploy/README.md`](deploy/README.md).
