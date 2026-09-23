# GovErp

Government ERP invoice validation prototype. Implementation follows the
[four-stage plan](docs/superpowers/plans/README.md).

## Current implementation

Stage 1 provides the domain foundation: chart of accounts, budgets and reservations,
PO claims, balanced journal entries, invoices, approval cycles and repository ports.
Validation, Application and Infrastructure projects are placeholders for subsequent
stages. The Blazor host currently displays only the project landing page.

## Build and test

Requires the .NET 10 SDK (see `global.json`). NuGet packages are restored into the
ignored `.packages` directory. No database or LLM credentials are needed at this stage.

```powershell
dotnet restore GovErp.sln
dotnet build GovErp.sln --no-restore
dotnet test GovErp.sln --no-restore
dotnet run --project src/GovErp.AppHost
```

Unit tests cover the domain invariants; architecture tests check project references
and domain purity. SQL Server concurrency, Docker deployment, authentication and
the interactive invoice demo are delivered by stages 3–4, not verified by these tests.
