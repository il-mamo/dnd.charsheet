# DnD.CharSheet — current project summary

Updated: 2026-10-20. State: task 1.1 verified by independent clean-copy build/test/package checks.

## Purpose and confirmed scope
Private-use Windows desktop character manager, strictly PHB 2024. Full classes/subclasses, species, backgrounds, feats, spells and equipment through levels 1–20, including multiclassing. Multiple characters, portraits, guided/existing entry, sheet/finder, battle assistance, inventory and resources. All additional P01–P07 features accepted: autosave/recovery, rests/conditions, advancement, casting resources, backup/restore, accessibility, currency/consumables and session history. DM adjudicates hit/success. No Jira.

## Confirmed stack and defaults
C#/.NET + WPF/MVVM + SQLite; offline, local single-user operation. Windows 11 x64 default target. SDK 10.0.402 pinned; CommunityToolkit.Mvvm 8.4.2, Microsoft.Data.Sqlite 10.0.13 and MSTest 4.0.2 centrally pinned. Owner selected self-contained win-x64 ZIP with manual updates. Presentation/application/domain/infrastructure separation; live data under LocalApplicationData/DnD.CharSheet. External dice; labelled final-total input plus natural dice when required. PDF/private catalog excluded from GitHub. Other defaults are in specification section 2; they may be revised by owner instruction.

## Repository and map

Owner workflow decision (2026-10-20): Developer creates a dedicated branch before each change; Graphics Designer and Tester both receive and work on that same branch. Record branch/base/review snapshot in handoffs; serialize overlapping edits/Git operations and recheck affected reviews after fixes. No separate role branches or implementation on main. This workflow update is on `workflow/shared-change-branch`; task 1.1's verified implementation status is unchanged.
Repository: https://github.com/il-mamo/dnd.charsheet
Local checkout: C:\Users\markm\source\repos\dnd.charsheet
- AGENTS.md: workflow, targeted reading, clarification and summary maintenance.
- docs/AGENTS_ROLES.md: Coordinator, Developer, Graphics Designer, independent Tester.
- docs/TECHNICAL_SPEC.md: v1 requirements, phased roadmap, source map, bounded task contracts and release gates.
- docs/DESIGN_SPEC.md: owner-directed dark gothic theme, tokens, layout/accessibility conventions, planned component catalog and design handoff workflow.
- docs/templates/DESIGN_HANDOFF_TEMPLATE.md: template for task-specific design contracts.
- docs/ARCHITECTURE.md and docs/tasks/0.3.md: project map, dependency/data boundaries, packaging and task 1.1 acceptance contract.
- DnD.CharSheet.slnx: Domain, Application, Infrastructure, Desktop plus three test projects under src/ and tests/. No domain models or storage schema yet.
- scripts/Build.ps1: locked restore, Release build, tests and self-contained ZIP; scripts/Test-Architecture.ps1 enforces project dependency direction. .github/workflows/windows.yml runs Verify on Windows.
- global.json, Directory.Build.props, Directory.Packages.props and seven package lock files pin SDK/build/dependencies; .gitignore excludes private/generated state. README.md provides executable onboarding.

## Verified current state and source
Initial repository had empty placeholders. Task 1.1 now supplies the compilation foundation and a synthetic in-memory SQLite native-loading test. DAImon informed the workflow without its Jira requirements. No persistent agents/runtime orchestration installed.
Supplied PDF accessible at: C:\Users\markm\Documents\Boys Play DnD\D&D [5e] [2024] - 01 - Players Handbook [Alt cover] [compressed].pdf
388 pages; first-printing 2024 identification inspected. Targeted creation/advancement/multiclassing pages text-reviewed. Creation overview, ability-generation and multiclassing pages visually reviewed. Initial source map in spec section 9. OCR errors found, including numeric tables: visual verification required before implementation. No full handbook audit/catalog ingestion or rule implementation performed.

## Design state
Owner-directed design revision 1.1: dark gothic fantasy with charcoal/blue-slate surfaces, pale ivory text, silver/iron framing, brass and crimson accents; raven/spire/mist imagery and atmospheric backgrounds inspired by two supplied references. Graphics Designer owns asset creation and manifest; Developer integrates supplied assets. User portraits are preserved. Text and dense data use opaque dark reading surfaces with verified contrast. Shared rules live in DESIGN_SPEC.md; per-screen contracts in docs/tasks/<id>.design.md. No screens, WPF tokens/styles, mockups or reusable controls have been implemented. Component catalog entries are planned, not verified. Designer supplies Ready contracts; Developer records revision/mappings; Designer compares visuals and Tester verifies behavior.

## Phase status and next work
0.1 decisions: Verified. 0.2 initial creation source map: completed; complete catalog inventory remains. 0.3 architecture/toolchain/packaging: Verified. 1.1 solution/build/Windows CI: Verified by independent clean-copy acceptance; remote CI subsequently failed with MSB1008; argument-forwarding repair passed local PowerShell 7 Verify and independent regression review; remote rerun pending. Next: 1.2 domain IDs and migrations. Phase 1 remains incomplete: app launch, schema and repository behavior await 1.2–1.4. Phases 2–9 not started. Schedule small content slices as dependent features need them.

## Validation and limitations
Task 1.1: Windows SDK 10.0.402; scripts/Build.ps1 Verify passed locked restore, Release build (zero warnings/errors), 1/1 synthetic native SQLite test, self-contained win-x64 publish and ZIP with WPF/runtime/native SQLite assets. Domain/Application test projects intentionally have no behavior tests. Independent Tester confirmed forbidden Domain-to-Infrastructure reference is rejected; clean-copy Verify exited 0 with seven-project build, 1/1 SQLite test and ZIP inventory verified; see docs/tasks/1.1.test.md. git diff --check passed. Initial sandbox NuGet TLS failure resolved with approved unsandboxed restore; repository-local DOTNET_CLI_HOME remains configured.

User-reported GitHub-hosted CI failed in packaging because an unquoted MSBuild property was split by PowerShell. The repair quotes all MSBuild properties and adds an actual-call-site argument regression check; local PowerShell 7 full Verify exited 0 (locked restore, Release build without warnings, SQLite 1/1, publish and ZIP). Independent positive/negative checks passed in PowerShell 5.1/7; see docs/tasks/1.1.ci-fix.test.md. Remote rerun after repair is pending; fresh-machine/no-installed-runtime packaging smoke, UI launch/accessibility and database migration/repository checks remain unrun/deferred to consuming tasks. Desktop entry point exits without a window pending task 1.4 design. No private content bundled, commits, pushes or releases made. Existing uncommitted planning changes preserved. No user-facing rules or character behavior implemented.
