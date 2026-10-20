# DnD.CharSheet — current project summary

Updated: 2026-10-20. State: planning; no application code implemented or tested.

## Purpose
Windows desktop character manager using PHB 2024; multiple characters, portraits, guided/existing character entry, character sheet, finder, battle assistance, resources and inventory. DM adjudicates hit/success. No Jira.

## Repository and document map
Repository: https://github.com/il-mamo/dnd.charsheet
Local checkout: C:\Users\markm\source\repos\dnd.charsheet
- AGENTS.md: development workflow and context efficiency.
- docs/AGENTS_ROLES.md: Coordinator, Developer, Graphics Designer, Tester.
- docs/AGENT_ROLES.md: compatibility pointer for original placeholder name.
- docs/TECHNICAL_SPEC.md: draft requirements, decision log and phased task queue.

## Verified current state
Initial inspection found empty documentation placeholders and no application sources. DAImon's agent instructions informed the workflow; its Jira integration and project-specific requirements are excluded. No persistent agents or runtime orchestration have been installed. No handbook PDF was found in this checkout; uploaded project sources have not been accessible through the filesystem inspection.

## Confirmed decisions (2026-10-20)
Full PHB 2024 classes, subclasses, species, backgrounds, feats, spells and equipment through levels 1–20. C#/.NET + WPF/MVVM + SQLite; local single-user offline operation. Private use only; keep handbook and extracted private catalog out of GitHub by default. Additional v1 scope P01–P07 remains proposed; multiclassing/optional rules need confirmation.

## Active decisions and next steps
Phase 0 only. Await an accessible handbook PDF path; full coverage, stack and private-use intent are confirmed. Confirm multiclassing and proposed essentials before final baseline. Next: resolve D1–D4 and create bounded 0.2/0.3 contracts. Do not start implementation while the baseline is unresolved.

## Validation and limitations
Documentation planning only; no application build/tests exist. Git inspection works with a command-scoped safe.directory setting; no global configuration changed. Git tracks docs as an empty file while the local docs path is a directory; reconcile this before committing. Documentation exists on disk but is not yet committed. No PHB rules have been verified or catalog content encoded. Summary must be revised after owner decisions and each verified implementation slice.

