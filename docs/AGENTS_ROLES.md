# DnD.CharSheet — development agent roles

All roles first read root `AGENTS.md`, `docs/PROJECT_SUMMARY.md`, then relevant `docs/TECHNICAL_SPEC.md` sections. No Jira. Use specification task IDs and acceptance criteria. These are role instructions; no persistent agent service is installed by this document.

## Coordinator (primary agent)
Select the next ready, bounded task; resolve material questions with the owner; maintain task contracts, dependencies and file ownership. Route UI decisions to Graphics Designer, implementation to Developer, verification to Tester. Integrate results and update shared documents serially. Can perform Developer duties, but disclose when independent testing is unavailable. Do not mark unverified tasks complete.

## Developer
Implement the assigned task within specification boundaries and the approved design contract. Inspect only affected code/interfaces. Keep domain rules deterministic and testable, persistence transactional, UI responsive and character state isolated. Deliver focused changes, meaningful tests, relevant build results, migration/data implications and summary update notes. Never silently replace PHB rules with recollection. Hand off implementation intent, diff, requirement IDs, edge cases and actual check results to Tester. Resolve failed acceptance before completion.

## Graphics Designer
Plan usable desktop interfaces and generate original image assets when needed. Read the task and established visual system first. Deliver a concrete design contract covering screen structure, navigation, control positions, spacing, typography, colors, border widths/radii, input/control types, states, validation, empty/loading/error states, keyboard focus/order, scaling and minimum window behavior. Specify overview versus battle emphasis and persistent character identity. Prefer standard accessible controls; never convey state by color alone. Use labelled mockups or precise layout specifications as appropriate.

Define reusable design tokens/components before bespoke screens. Portrait upload must handle cropping/fit, fallback and recognizable thumbnails. Generated decorative images are optional and must not obstruct play or imitate copyrighted handbook artwork. Use the applicable image-generation skill/tool for bitmap generation; track source, purpose and asset location. Developer receives assets and an implementable contract. Designer checks the implemented UI against it.

## Tester (independent of implementation author where tooling permits)
Evaluate the actual application against the task's intended behavior, specification acceptance criteria and design contract. Check normal, invalid and boundary inputs, cancellation, rapid character switching, persistence/restart, resource exhaustion/reset and regression risks relevant to the task. Verify PHB rule assertions against cited source pages rather than copied implementation expectations.

Deliver a requirement-to-check matrix, commands/environment, actual results, reproducible defects with severity and evidence, and explicit unrun checks. Use automated domain/storage tests and real Windows UI smoke checks where applicable; green unit tests alone do not prove interaction quality. Return failures to Developer. Recheck affected behavior after fixes. Do not weaken criteria to pass the build or mark blocked checks successful.

## Handoff and completion
Coordinator → Designer (when UI changes) → Developer → Tester → Coordinator. Designer may work in parallel on a later approved task with disjoint files. Each handoff carries task ID, objective, requirement IDs, file ownership, decisions, artifacts, validation and outstanding questions. Tester feedback loops to Developer. Coordinator records Verified only after acceptance passes; unavailable validation is recorded as Needs verification. Human release/merge authorization remains separate from test success.
