# Architecture and toolchain — task 0.3

Baseline date: 2026-10-20. Scope: technical specification D2/D3, sections 2, 5, 7 and 10. Owner selected self-contained ZIP on 2026-10-20. This is a foundation contract, not evidence of an implemented application.

## Toolchain

Windows 11 x64; installed SDK 10.0.402 and Windows Desktop runtime 10.0.13 verified with `dotnet --info`. Pin the SDK exactly in `global.json`. Use SDK-default stable C# language version, nullable references, implicit usings, deterministic builds and warnings as errors. No workloads or IDE are required by the intended CLI workflow; WPF and MSTest templates were listed successfully using a repository-local CLI home; task 1.1 now verifies Release compilation with zero warnings/errors; independent clean-copy evidence is recorded in docs/tasks/1.1.test.md.

.NET 10 is LTS, supported through November 2028 according to the [official support policy](https://dotnet.microsoft.com/en-us/platform/support/policy). Updating SDK/runtime pins is an explicit maintenance change with restore/build/test/publish verification; self-contained deployments must be republished to receive runtime fixes.

Central package baseline (introduced only in projects that use each package):

| Package | Version | Owner/purpose |
| --- | --- | --- |
| [CommunityToolkit.Mvvm](https://www.nuget.org/packages/CommunityToolkit.Mvvm/8.4.2) | 8.4.2 | Desktop observable view models and commands |
| [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.13) | 10.0.13 | Infrastructure ADO.NET SQLite provider, including native bundle |
| [MSTest](https://www.nuget.org/packages/MSTest/4.0.2) | 4.0.2 | Test framework/adapter baseline |

Use `Directory.Packages.props` for direct versions. First solution restore generates per-project `packages.lock.json` files; commit them and use locked restore in CI thereafter. Task 1.1 restored the graph and verified synthetic native SQLite initialization/query; application migration/repository compatibility awaits tasks 1.2/1.3. No ORM, host framework, service container or mapping library is needed initially; compose dependencies explicitly at desktop startup.

## Solution map and dependency direction

Solution: `DnD.CharSheet.slnx`.

| Project | Target | Allowed project references | Responsibility |
| --- | --- | --- | --- |
| `src/DnD.CharSheet.Domain` | net10.0 | None | Pure character concepts, verified calculations and invariants |
| `src/DnD.CharSheet.Application` | net10.0 | Domain | Use cases, validation orchestration and persistence/catalog ports |
| `src/DnD.CharSheet.Infrastructure` | net10.0 | Application, Domain | SQLite repositories/migrations, managed files, catalog loading and backup adapters |
| `src/DnD.CharSheet.Desktop` | net10.0-windows | Application, Infrastructure | WPF views/view models, dispatcher and composition root |
| `tests/DnD.CharSheet.Domain.Tests` | net10.0 | Domain | Verified rules and boundary examples |
| `tests/DnD.CharSheet.Application.Tests` | net10.0 | Application, Domain | Use-case isolation, cancellation and failure behavior |
| `tests/DnD.CharSheet.Infrastructure.Tests` | net10.0 | Infrastructure, Application, Domain | Temporary on-disk SQLite, native loading, migrations and rollback |

Desktop references Infrastructure only for composition; view models depend on application use cases. Domain knows no WPF, SQL, files or catalog loader. Application owns ports; Infrastructure implements them. Avoid placeholder business models and unused interfaces in task 1.1. Add each contract with the first real behavior that needs it. An architecture check in task 1.1 must reject reversed project references.

Mutations carry explicit character identity and expected revision to guard stale writes. A use case validates, calculates through Domain, then atomically persists character state and history. UI selection is never persistence identity. Infrastructure serializes writes; synchronous SQLite work runs off the UI thread with cancellation boundaries and results marshalled to WPF. Do not rely on provider async methods alone to prevent UI blocking. Preview/cancel never writes. Define concrete schema, connection lifetime and concurrency tests in tasks 1.2/1.3.

## Persistence and private content

Root: `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)/DnD.CharSheet`.

| Relative location | Purpose |
| --- | --- |
| `data/characters.db` | Mutable character state, history and migration version |
| `catalog/<version>/` | Locally provisioned private verified catalog and manifest; read-only during play |
| `portraits/` | Managed portrait copies identified by stable asset IDs |
| `backups/` | Consistent snapshots plus referenced assets/manifest |

Catalog IDs and content version/source citations are distinct from mutable character IDs. No catalog content is created or bundled by this task. Define catalog schema/import validation in a later bounded content task; missing/incompatible catalog must produce an actionable state without losing existing characters. The original PDF remains at its supplied external location and is not a runtime dependency.

Use parameterized SQL, foreign keys enabled on every connection, explicit transactions and ordered versioned migrations. Back up before migration; refuse newer unsupported schemas intact. Implement SQLite-safe snapshots rather than copying an open database. No database or catalog lives beside installed binaries. Git ignores are guardrails; inspect staged file lists for private data before any commit.

## Packaging — owner selected self-contained ZIP

Use an owner-approved self-contained `win-x64` ZIP of the publish directory, multi-file, untrimmed. This avoids requiring a runtime install and preserves WPF/native SQLite files. Proposed settings: `SelfContained=true`, `PublishSingleFile=false`, `PublishTrimmed=false`. See [Microsoft deployment guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/).

Manual update: close the app, preserve a consistent backup, extract the new release into a fresh binary directory and launch it; live data stays under LocalApplicationData. Removing the binary directory preserves data. Data deletion is a separate explicit action. No updater or installer is implemented. An installer is outside this initial packaging scope. Release validation later includes native SQLite loading on a machine without a separate .NET runtime and install/update/removal recovery.

## Task 1.1 execution contract

Create the projects above, SDK-based WPF project with `UseWPF=true`, test runner configuration compatible with the pinned MSTest package, solution and Windows CI. Introduce no visible shell until task 1.4 has its design handoff; validate desktop compilation without treating it as launch acceptance.

Add checked-in build commands/scripts for restore, Release build, tests and packaging. CI uses Windows, installs SDK 10.0.402, performs locked restore, builds and runs tests. Generate initial lock files in a normal restore before enabling locked mode. Publish the approved self-contained ZIP. CI must use synthetic fixtures and no PHB/private catalog.

Acceptance: clean-checkout commands execute on Windows; project-reference checks enforce the table; build/tests pass; published SQLite native assets are present and smoke-tested when a consuming behavior exists. App launch and fresh/existing DB behavior remain the wider phase 1 gate, not task 0.3 claims.
