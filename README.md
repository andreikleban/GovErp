# GovErp

Municipal ERP invoice validation prototype (Blazor Server, SQL Server, multi-tenancy).
Architecture and decisions: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Requirements

- [.NET 10 SDK](global.json)
- Local development: [Docker](https://www.docker.com/) (Aspire runs SQL Server in a container)
- Docker Compose stack: Docker Engine + Compose

NuGet packages restore into the `.packages` directory (listed in `.gitignore`).

## Demo sign-in

Password for all demo users (UI login, Aspire AppHost, Compose env in `docker/.env.example`):

**`1!Qwertyui`**

On `/login`, pick a user (for example **Fire Chief · Springfield** — `fire.chief`) and enter that password.

## Local development (Aspire)

Recommended path: AppHost starts SQL Server with a persistent `goverp-sql` volume and the `GovErp.Web` site.

```powershell
dotnet restore GovErp.sln
dotnet run --project src/GovErp.AppHost
```

Open the **web** app URL from the Aspire dashboard output. First run applies migrations and seed data.

Do not run Aspire and `docker compose` at the same time on one machine if both publish **1433** on localhost — the ports will conflict.

### Explanations (optional LLM)

Default is `Explanation__Provider=Template`. To use an external provider, set environment variables or user secrets on **AppHost** (values are forwarded to Web): see [AppHost.cs](src/GovErp.AppHost/AppHost.cs). For Compose, use [`docker/.env.example`](docker/.env.example).

## Demo via Docker Compose

Docker assets live under [`docker/`](docker/). The **web** service pulls **`klebanab/goverp-web:latest`** from Docker Hub (override with `GOVERP_WEB_IMAGE` in `docker/.env`). SQL data lives in the `sqldata` volume.

```powershell
Copy-Item docker/.env.example docker/.env
docker compose -f docker/docker-compose.yml up
```

To build and tag a local image instead (for example before pushing to Hub), use [`docker/build-image.ps1`](docker/build-image.ps1) and set `GOVERP_WEB_IMAGE=goverp-web:local` in `docker/.env`.

App URL: **http://127.0.0.1:8080** (bound to localhost only).

Variables are documented in [`docker/.env.example`](docker/.env.example). Defaults match the demo password above.

### Demo tenant reset (Compose)

Allowed only in the Demo environment and for allowlisted database names:

```powershell
docker compose -f docker/docker-compose.yml exec web dotnet GovErp.Web.dll demo-reset --tenant springfield --confirm springfield
```

`docker compose -f docker/docker-compose.yml down -v` removes the SQL volume — full data wipe, not the normal demo reset command.

## Build and test

```powershell
dotnet restore GovErp.sln
dotnet build GovErp.sln --no-restore
dotnet test GovErp.sln --no-restore
```

Unit and architecture tests do not start the full Docker stack; SQL integration scenarios use Testcontainers in test projects.

## Implementation plans

Stage-by-stage plans: [docs/superpowers/plans/README.md](docs/superpowers/plans/README.md).
