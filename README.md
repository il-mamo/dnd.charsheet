# DnD.CharSheet

Private-use, offline Windows 11 x64 character manager for D&D PHB 2024.

The desktop app provides a local draft character library, character switching, managed portraits, draft recovery and versioned backup/restore. Rules and Adventure management remain planned; see docs/PROJECT_SUMMARY.md for verified state.

Use .NET SDK **10.0.402** on Windows. The SDK is pinned in global.json; an IDE is optional. Initial restore requires network access. Run from the repository root:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build.ps1 -Task Verify
~~~

Verify checks architecture, restores with committed lock files, builds Release, runs tests and creates artifacts/DnD.CharSheet-win-x64.zip. The ZIP is self-contained win-x64, multi-file and untrimmed. It contains no private catalog. Tests cover identity boundaries, SQLite persistence, portraits, draft recovery and backup/restore.

Individual tasks are Restore, Build, Test and Package. Build requires Restore; Test requires Build. Package performs its own locked runtime restore and publish. Windows CI executes Verify and retains the ZIP as a workflow artifact; it does not publish a release.

For an intentional dependency update, run dotnet restore DnD.CharSheet.slnx --force-evaluate, review all seven packages.lock.json files, and rerun Verify. RuntimeIdentifiers includes win-x64 throughout the graph so ordinary and packaging restores share consistent lock files.

Start with [architecture and toolchain](docs/ARCHITECTURE.md), [current state](docs/PROJECT_SUMMARY.md), and [task 1.1](docs/tasks/1.1.md). Product requirements are in [the technical specification](docs/TECHNICAL_SPEC.md).

Do not add the handbook, extracted catalog, live databases or personal character assets to Git. Private data belongs under %LOCALAPPDATA%/DnD.CharSheet, separate from application binaries. Manual updates replace the binary directory while preserving this data; no updater or installer exists.

## Backups

Choose **Backups** in the title bar, then **Create backup** to save a ZIP outside the live data folder. To restore, choose a ZIP, review its date and contents, and select **Restore backup**. This replaces all current characters, portraits and draft recovery. Close the dialog to reload; select a character again afterward.

A safety ZIP is saved under %LOCALAPPDATA%/DnD.CharSheet/backups before replacement. Version 1 supports the current schema only; incompatible archives are rejected. Current data must be readable for the safety backup; corruption blocks replacement and requires manual recovery. Keep copies outside the application data folder. Character import/export and Adventure compatibility are deferred. Only one app session may open this data at a time.
