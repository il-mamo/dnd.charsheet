# DnD.CharSheet — current project summary

Updated: 2026-10-20. State: specification baseline completed; no application code implemented or tested.

## Purpose and confirmed scope
Private-use Windows desktop character manager, strictly PHB 2024. Full classes/subclasses, species, backgrounds, feats, spells and equipment through levels 1–20, including multiclassing. Multiple characters, portraits, guided/existing entry, sheet/finder, battle assistance, inventory and resources. All additional P01–P07 features accepted: autosave/recovery, rests/conditions, advancement, casting resources, backup/restore, accessibility, currency/consumables and session history. DM adjudicates hit/success. No Jira.

## Confirmed stack and defaults
C#/.NET + WPF/MVVM + SQLite; offline, local single-user operation. Windows 11 x64 default target. SDK/package versions and packaging format remain task 0.3 choices. Presentation/application/domain/infrastructure separation; live data under LocalApplicationData/DnD.CharSheet. External dice; labelled final-total input plus natural dice when required. PDF/private catalog excluded from GitHub. Other defaults are in specification section 2; they may be revised by owner instruction.

## Repository and map
Repository: https://github.com/il-mamo/dnd.charsheet
Local checkout: C:\Users\markm\source\repos\dnd.charsheet
- AGENTS.md: workflow, targeted reading, clarification and summary maintenance.
- docs/AGENTS_ROLES.md: Coordinator, Developer, Graphics Designer, independent Tester.
- docs/TECHNICAL_SPEC.md: v1 requirements, phased roadmap, source map, bounded task contracts and release gates.

## Verified current state and source
Initial repository had empty placeholders and no application sources. DAImon informed the workflow without its Jira requirements. No persistent agents/runtime orchestration installed.
Supplied PDF accessible at: C:\Users\markm\Documents\Boys Play DnD\D&D [5e] [2024] - 01 - Players Handbook [Alt cover] [compressed].pdf
388 pages; first-printing 2024 identification inspected. Targeted creation/advancement/multiclassing pages text-reviewed. Creation overview, ability-generation and multiclassing pages visually reviewed. Initial source map in spec section 9. OCR errors found, including numeric tables: visual verification required before implementation. No full handbook audit/catalog ingestion or rule implementation performed.

## Phase status and next work
0.1 decisions: Verified. 0.2 initial creation source map: completed; complete catalog inventory remains. 0.3 architecture/toolchain/packaging contract: Ready for the next development request. Phases 1–9 not started. Schedule small content slices as their dependent features need them, never one whole handbook/class/spell implementation task. Application implementation was not part of this documentation request.

## Validation and limitations
Documentation and targeted source verification only; no app build/tests yet. Git inspection works with command-scoped safe.directory; no global config changed. Git currently tracks docs as an empty file while local docs is a directory; reconcile this before committing so documents are tracked. No commits or pushes made. User-facing rules and coverage must not be described as implemented.
