# DnD.CharSheet — technical specification

Status: BASELINE v1 — owner scope decisions confirmed 2026-10-20. Task 1.1 implementation status is recorded in section 7 and docs/tasks/1.1.md.

## 1. Purpose and boundaries
Windows desktop application for a single player's multiple D&D characters, following PHB 2024. Local SQLite database. C#/.NET with WPF/MVVM; offline, single-user operation. Intended for private use only. Fast identity-safe switching during play. No Jira integration. DM adjudicates target AC, success/hit and world effects; application explains the character's rolls/effects and tracks confirmed character state.

Confirmed original requirements use R IDs; accepted additional requirements retain P IDs for stable traceability. All R and P requirements are in v1 scope. Source verification must precede encoding handbook mechanics.

## 2. Confirmed decisions and implementation defaults
- D1 CONFIRMED: Full PHB 2024 classes, subclasses, species, backgrounds, feats, spells and equipment through levels 1–20, including multiclassing. Incremental delivery is permitted; reduced final coverage is not. PHB features requiring companion/summoned/transformed creature information must include the relevant PHB stat blocks and character-state effects. This is not an encounter manager.
- D2 CONFIRMED: C#/.NET, WPF/MVVM and SQLite; offline, single-user operation. Select and pin a supported SDK/package baseline during architecture task after checking installed tooling and official support information.
- D3 CONFIRMED: Private use only. Supplied PHB PDF is accessible; see section 6. Keep PDF, page renders and extracted private catalog out of GitHub. Public repository code must support loading the local private catalog without bundling book content.
- D4 CONFIRMED: P01–P07 are included in v1, including backup/restore, leveling, rests, conditions, spell resources, accessibility and session history.
- Defaults: external physical dice with manual input; no built-in dice roller required. Enter the final roll total in a clearly labelled field; optionally record natural die values when a rule depends on them, without inferring them from totals. DM success/hit remains explicit. Users confirm critical status when relevant.
- Defaults: level 1 guided creation plus existing-character entry at levels 1–20; XP tracking and DM-directed level advancement; HP advancement supports entered rolls or verified fixed values. Do not force XP thresholds when the DM awards a level.
- Defaults: Windows 11 x64, resizable desktop window, per-user data under LocalApplicationData/DnD.CharSheet. Package and update strategy must be documented in task 0.3; no auto-update service required. Other architectures/Windows versions are not acceptance targets without a scope change.
- Defaults: backup is a versioned ZIP containing a manifest, consistent database snapshot and managed portraits; character export is a versioned JSON document plus assets in a ZIP. Restore previews and validates before replacement; character import creates new IDs by default. No arbitrary code or absolute/path-traversal entries may be imported.
- Optional-rule boundary: support PHB choices needed for the agreed character flows, including multiclassing and all three ability-generation methods. DM-discretionary outcomes are recorded explicitly. Older-book compatibility remains excluded; a reference to it in the PHB does not expand the catalog. Any unresolved discretionary variant is clarified at its bounded task, not silently enabled.

## 3. Confirmed functional requirements
- R01 Character library: create/manage multiple characters; recognizable portrait/name; switch quickly without state leaking between characters. Keep unsaved input safe and bind mutations to character ID even if selection changes.
- R02 Portraits: import/update local image; managed copy, preview, fallback, supported-format validation and reasonable size limits. Missing original file must not break saved portrait.
- R03 Guided creation: ordered screens matching the verified PHB Creating a Character process (section 9); progress, back navigation, prerequisite validation and resumable draft. Distinguish incomplete drafts from playable characters.
- R04 Existing character entry: enter existing level and all relevant choices/stats/resources; verify consistency and flag incomplete information. Never silently reroll, reset or overwrite supplied values.
- R05 Character sheet: neatly section identity, ability scores/modifiers, saves/skills, class features, feats, species traits, inventory, equipment, weapons, spells and other applicable character details. Explain calculated modifiers and their sources.
- R06 Battle mode: prominent HP/status/resources and categorized actions, bonus actions, reactions plus relevant contextual options. Derive options from character choices, equipment, resources and verified rules. Do not imply all features share identical action costs.
- R07 Dice workflow: select action and weapon/spell/feature as needed; show roll expression, accept reported roll and DM success/hit outcome, then show applicable effects/damage expression. Example dice expressions in the brief are illustrative, not weapon rules. Use the roll-entry contract in section 2; label totals and natural dice distinctly. Cancellation or duplicate confirmation must not accidentally spend resources. Do not determine enemy AC or automatically apply enemy damage.
- R08 Finder: readily accessible search across ability scores, skills, saves and applicable checks; partial case-insensitive names such as Perf find Performance and its signed modifier. Show score plus modifier for six abilities; modifier-only entries remain clearly distinct. Reuse central calculations.
- R09 Status: current/max HP, relevant temporary HP, death save successes/failures and limited-use resources; show used/remaining/max and recharge trigger. Persist resources independently for each character. Verify actual resource maxima/recharge rules; brief examples do not establish handbook values.
- R10 Inventory: search verified catalog; add/remove quantities; inspect item details; equip/use applicable items; create DM custom items with descriptive and structured mechanical fields. Label provenance. Deleting catalog references must not corrupt existing character items.

## 4. Accepted additional v1 requirements
- P01 Autosave validated changes transactionally; recover drafts after restart; visible save failure and retry; never silently lose data.
- P02 Explicit short/long rest workflow with preview of affected resources and confirmation; apply each resource's verified recharge rules. Track conditions, exhaustion, concentration and death-state transitions where applicable.
- P03 Level advancement with choice/prerequisite checks and explicit handling of prior-level mistakes. Include multiclassing with prerequisites, distinct class/total levels, correct feature interactions, mixed Hit Dice and spell resource rules.
- P04 Spell preparation/known spell bookkeeping, slots and other casting resources, concentration and resource expenditure where applicable to supported classes.
- P05 Versioned backup/export and restore/import covering characters, portraits and custom items; validate before mutation, back up before migration/restore, preserve existing data on failure. Portable character export for transfer.
- P06 Keyboard navigation, labelled inputs, scalable readable UI, clear signed modifiers, non-color status cues and offline operation with no required account/network.
- P07 Basic currency, ammunition and consumable tracking; spending/use only on explicit confirmation. Session history for recent character mutations to explain changes; History is an audit aid; unrestricted undo is not required. Provide explicit correction controls for current HP/resources and preserve the correction in history.

## 5. Architecture and data contracts
Confirmed stack; architectural boundaries: desktop presentation → application use cases → pure domain/rules → SQLite/files infrastructure. UI must not duplicate rule calculations or issue SQL directly. Use stable IDs, foreign keys, schema migrations, transactions and explicit units. Store mutable character state separately from versioned verified catalog content. Preserve rule-source references and overrides with reasons. No runtime LLM dependency.

Model characters, progression/choices, ability values/proficiencies, features/resources/recharge policies, items/equipment, spellcasting, conditions, portraits and source citations. Schema details belong to bounded design tasks. Calculated values derive from a shared service; represent proficiency/expertise/overrides explicitly. Store portrait assets in managed local application data, not arbitrary source-file references. Installation data location: user-local application data (section 2); keep live databases and private source content out of source control. Backups must include referenced assets. No cloud sync, telemetry, multiplayer, DM encounter manager, virtual tabletop, automated enemy resolution, homebrew class builder or 2014 compatibility in baseline v1.

## 6. Rules verification protocol
Use accessible supplied PHB 2024; record edition, section, printed/PDF page, interpreted mechanic and representative boundary checks. Build a coverage matrix for every supported option. Mark unverified entries unavailable/draft instead of presenting them as correct. Distinguish app UX choices from book rules. Keep the source PDF and extracted private catalog local and out of GitHub by default. Record the accessible source location under D3 before verification. Do not extract the whole book merely to implement one feature.

## 7. Phases and bounded task queue
Statuses: Proposed → Ready → In progress → Needs verification → Verified; Blocked includes reason and next action. Each task needs a brief objective, scope, dependencies, affected files and executable acceptance criteria before Ready. Split any task too large for one focused implementation/test handoff. Baseline scope is confirmed and source access verified. Phase 0 is in progress: 0.1 Verified; 0.2 initial creation/multiclassing source map completed, full coverage inventory remains; 0.3 Verified: architecture contract and pinned toolchain checked independently; owner selected self-contained ZIP. See ARCHITECTURE.md and tasks/0.3.md. Phase 1 has started: task 1.1 is Verified by independent clean-copy build/test/package acceptance; task 1.2 domain IDs/migrations is Verified by independent boundary/storage tests; task 1.3 repository round-trip is Verified by independent storage acceptance; task 1.4 remains pending.

| Phase | Small task sequence | Acceptance gate |
|---|---|---|
| 0 Decisions and rules map | 0.1 record D1–D4; 0.2 creation source map and bounded catalog inventory; 0.3 SDK/package/packaging decisions and solution map | Approved baseline and accessible rules source; no guessed mechanics |
| 1 Foundation | 1.1 solution/build/Windows CI (Verified locally; CI argument repair awaiting remote rerun); 1.2 domain IDs and migrations (Verified); 1.3 repository round-trip (Verified); 1.4 shell/design tokens | App launches; clean build; fresh and existing DB paths tested |
| 2 Library and persistence | 2.1 library/create draft; 2.2 switching; 2.3 portraits; 2.4 save recovery; 2.5 backup/restore | Two distinct characters survive restart and restore, including portraits; switching cannot cross-write |
| 3 Character model and existing entry | 3.1 ability/proficiency calculations; 3.2 identity/progression entry; 3.3 features/resources; 3.4 spellcasting entry | Representative supported existing higher-level characters validate and round-trip without resets |
| 4 Guided creation | 4.1 wizard scaffolding/draft resume; 4.2 verified origin choices; 4.3 class/abilities/equipment choices; 4.4 review/finalization | Book-ordered flow; back/resume preserve choices; invalid prerequisites cannot finalize |
| 5 Sheet and finder | 5.1 sectioned sheet; 5.2 finder; 5.3 modifier breakdowns | Perf finds Performance; score/modifier distinctions correct; identity always apparent |
| 6 Inventory and spells | 6.1 catalog search; 6.2 custom items/quantity/equip; 6.3 casting preparation/slots | Verified/custom provenance visible; supported equip/casting effects consistent across screens |
| 7 Session state | 7.1 HP/temp HP/death saves; 7.2 limited resources; 7.3 conditions/concentration; 7.4 rest preview/apply | Boundary rules verified; rest resets only eligible resources; restart preserves session state |
| 8 Battle workflows | 8.1 action availability; 8.2 weapon hit/miss flow; 8.3 spell/feature flows; 8.4 expenditure/history | DM outcome remains explicit; hit shows correct expression; cancel/repeat safe; no enemy AC logic |
| 9 Progression and release | 9.1 level-up and multiclass slices by supported class; 9.2 content coverage gaps; 9.3 accessibility/Windows smoke; 9.4 packaging/recovery | All approved options covered; migration/restore and end-to-end scenarios pass; known limits disclosed |

Task ordering within phases may follow explicit dependencies: spell/feature entry needs verified catalog slices; final creation choices need those slices too. Never wait until phase 9 to ingest required class rules. Phase 9 closes coverage and adds advancement, rather than supplying earlier tasks' prerequisites. Scope each content slice against confirmed D1 before scheduling; routine in-scope slices do not require repeated owner approval.

## 8. Verification and release criteria
Domain tests cover score modifier rounding, proficiency, supported stacking, resource limits, rest policies and dice expressions from verified examples. Storage integration checks cover migrations, character isolation, restart and failed/invalid restore. UI checks cover guided and existing entry, rapid switching, finder, portraits, battle hit/miss/cancel and resource persistence. Include one martial and one spellcaster path, then a coverage matrix for all approved class/species options. Check invalid inputs and error recovery as well as happy paths. Record commands, environment and unrun checks. No completion claims based only on screenshots or unit tests.

Release requires the approved scope's acceptance matrix, no unresolved data-loss or rules-correctness defects, working install/update/uninstall data policy, backup/recovery instructions and user-authorized release. Task 0.3 selects packaging against the Windows target in section 2. Uninstall preserves user data by default; removal of user data is a separate explicit choice.


## 9. Verified creation flow and initial source map
Source: supplied Player’s Handbook, 2024 first printing, 388 PDF pages. Printed page numbers below differ from 1-based PDF page numbers. Text extraction contains OCR mistakes; numeric tables and ambiguous text require rendered-page inspection before encoding. Creation overview and ability-generation page were visually checked; multiclassing pages 44–45 were also visually checked. Other cited creation pages were text-reviewed; their detailed mechanics still require implementation-time verification.

| Area | Printed page | PDF page | Required application behavior |
|---|---|---|---|
| Creation overview / class | 33 | 35 | Five primary steps: class, origin, ability scores, alignment, remaining details |
| Origin and starting equipment | 36–37 | 38–39 | Background, species, languages, granted proficiencies/feat and class/background equipment choices |
| Ability generation | 38 | 40 | Standard array, manual rolled results and point buy; background adjustments and score-limit checks |
| Alignment | 39 | 41 | Explicit alignment step with descriptive guidance; record DM permission where applicable |
| Remaining sheet details | 40–41 | 42–43 | Features/choices, derived values, HP/Hit Dice, AC, attacks, spellcasting, name and optional descriptive details |
| Level advancement | 41–42 | 43–44 | Per-level choices, HP and derived-value recalculation; verify all numeric tables visually |
| Higher-level start | 43 | 45 | Distinguish creating a new higher-level character from transcribing an existing character; DM determines extra starting equipment |
| Multiclassing | 44–45 | 46–47 | Validate prerequisites; separate class levels from character level, spell preparation from slot availability and Pact Magic from ordinary slots |

Wizard subdivisions are allowed for readability but preserve this primary order. Changing an earlier choice must preview dependent invalidations, preserve unaffected input, and require replacement choices before completion. Existing-character entry may navigate sections freely and must not reinterpret transcribed state as a newly rested character. Name/portrait may be entered early as identity aids without changing the rules sequence.

Add explicit sheet/session coverage for Heroic Inspiration, Hit Dice by die type, proficiency/training, initiative, speed, size, Passive Perception, armor calculations and item attunement where applicable. Include weapon mastery and class-specific selections. Spell/feature screens must show relevant range, target, duration, components, concentration, action cost and scaling. Rules needing target choices or DM decisions prompt for them; unsupported automated effects remain visible instructions, never silently disappear. Full coverage means every approved option is usable and its character-state mechanics are implemented; arbitrary world simulation remains outside scope.

## 10. Content slices, task contracts and completion evidence
The phase table is a roadmap, not permission to assign an entire phase as one agent task. Use these decomposition limits:
- Class content: one base-class capability or one subclass across a bounded level band per task; level bands 1–4, 5–10, 11–16 and 17–20 are scheduling aids, split further when mechanics differ.
- Origin content: one species (including its choices) or a small homogeneous background group per task.
- Feats, equipment and spells: a small named batch with shared mechanics; separate unusual behaviors into dedicated tasks. Never assign all spells or all classes in one task.
- Multiclassing: separate prerequisites/proficiencies, HP/Hit Dice, non-stacking features/AC and combined spellcasting/Pact Magic; test interactions with relevant class slices.
- Catalog inventory records entity ID, source reference, ingestion status, implementation status, verification status and associated task/test IDs. Verified prose alone does not establish mechanical support.

For each task record: ID, dependencies, specification IDs, objective, non-goals, assigned role, owned files, source pages, acceptance scenarios, commands/results, status and next handoff. Put task-local detail in a small `docs/tasks/<id>.md` when implementation begins; keep only current/next IDs in the summary. Designer contracts live in `docs/tasks/<id>.design.md` using `docs/templates/DESIGN_HANDOFF_TEMPLATE.md`; simple component reuse can be documented briefly in the task. Maintain a concise `docs/CONTENT_COVERAGE.md` index when content ingestion starts; do not paste private book text into it.

First implementation-ready contract to prepare (0.3): inspect installed SDKs; verify selected versions against official sources; document package/version pins, solution projects, persistence adapter choice and packaging recommendation. Acceptance: choices fit WPF/SQLite/offline Windows target, private catalog location is defined, no content is bundled in Git, and task 1.1 can run from a clean checkout. No requirement to investigate the entire machine or repository.

Release coverage gate: all twelve classes and their PHB subclasses, all PHB species/backgrounds/feats/spells/equipment and relevant creature blocks are indexed and verified; every level 1–20 is represented in progression validation. Automated table checks may verify all level entries; focused behavioral tests cover distinct mechanics and interactions. Include mixed caster/Pact Magic and mixed Hit Dice scenarios, higher-level existing entry and level advancement with Constitution changes. No exhaustive enumeration of every possible character build is required, but every rule implementation needs representative tests and every catalog entity needs schema/source validation.

## 11. Design and nonfunctional acceptance
`docs/DESIGN_SPEC.md` defines the visual system, managed component catalog, interaction conventions and Designer-to-Developer contract. It operates within this technical specification; visual design cannot change product scope or PHB mechanics. The owner-directed baseline is dark gothic, with pale readable text, atmospheric backgrounds and restrained iron/silver/brass graphic details. Graphics Designer owns asset creation; Developer implements the design and integrates supplied artwork.

Designer defines reusable tokens and component contracts before feature screens. Persistent portrait/name identifies the active character in overview and battle mode. Library switching and the finder are available without leaving a long navigation path; provide keyboard shortcuts in the design contract. Battle layout prioritizes HP, conditions, resources and action categories, while full detail remains reachable. Do not impose a visual theme by copying the handbook artwork.

Validate supported layouts at 1280×720 and 1920×1080 with 100%, 150% and 200% scaling as practical; scrolling is acceptable, inaccessible controls are not. UI Automation names, visible focus, logical tab order and keyboard access are required. Search filters without a network call; catalog/database work must not freeze the UI. Establish repeatable representative data and timing measurements during UI implementation before setting numeric performance budgets.

Persist each confirmed mutation atomically with affected resources/history. Preview and cancellation do not mutate state. Prevent duplicate submission and stale-character writes. Handle missing portraits, unreadable private catalog, disk-write failures and incompatible imports with actionable errors and intact prior state. Use SQLite-safe snapshots rather than copying a live database without its journal state. No credentials, telemetry or runtime network access are required for normal play.
