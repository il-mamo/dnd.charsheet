# DnD.CharSheet

Private-use, offline Windows 11 x64 character manager for D&D PHB 2024.

Task 1.1 provides the solution, dependency checks, build scripts and Windows CI. The desktop entry point compiles and exits; a visible application shell is planned for task 1.4. No character rules or storage schema are implemented.

Use .NET SDK **10.0.402** on Windows. The SDK is pinned in global.json; an IDE is optional. Initial restore requires network access. Run from the repository root:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build.ps1 -Task Verify
~~~

Verify checks architecture, restores with committed lock files, builds Release, runs tests and creates artifacts/DnD.CharSheet-win-x64.zip. The ZIP is self-contained win-x64, multi-file and untrimmed. It contains no private catalog. The in-memory SQLite smoke test checks native provider loading; Domain and Application test projects have no behavioral tests yet.

Individual tasks are Restore, Build, Test and Package. Build requires Restore; Test requires Build. Package performs its own locked runtime restore and publish. Windows CI executes Verify and retains the ZIP as a workflow artifact; it does not publish a release.

For an intentional dependency update, run dotnet restore DnD.CharSheet.slnx --force-evaluate, review all seven packages.lock.json files, and rerun Verify. RuntimeIdentifiers includes win-x64 throughout the graph so ordinary and packaging restores share consistent lock files.

Start with [architecture and toolchain](docs/ARCHITECTURE.md), [current state](docs/PROJECT_SUMMARY.md), and [task 1.1](docs/tasks/1.1.md). Product requirements are in [the technical specification](docs/TECHNICAL_SPEC.md).

Do not add the handbook, extracted catalog, live databases or personal character assets to Git. Private data belongs under %LOCALAPPDATA%/DnD.CharSheet, separate from application binaries. Manual updates replace the binary directory while preserving this data; no updater or installer exists.
