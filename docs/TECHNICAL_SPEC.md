# DnD.CharSheet — technical specification

Status: DRAFT — full PHB coverage, stack and private-use intent confirmed on 2026-10-20; remaining decisions below. No application implementation authorized by this planning task.

## 1. Purpose and boundaries
Windows desktop application for a single player's multiple D&D characters, following PHB 2024. Local SQLite database. C#/.NET with WPF/MVVM; offline, single-user operation. Intended for private use only. Fast identity-safe switching during play. No Jira integration. DM adjudicates target AC, success/hit and world effects; application explains the character's rolls/effects and tracks confirmed character state.

Confirmed requirements below use R IDs; proposed additions use P IDs until accepted. Source verification must precede encoding handbook mechanics.

## 2. Decisions required before baseline approval
- D1 CONFIRMED: Full PHB 2024 classes, subclasses, species, backgrounds, feats, spells and equipment through levels 1–20. Incremental delivery is permitted; reduced final coverage is not. Multiclassing and optional-rule inclusion still require explicit confirmation.
- D2 CONFIRMED: C#/.NET, WPF/MVVM and SQLite, offline and single-user. Choose supported SDK/package versions during architecture task after checking tooling.
- D3 PARTLY RESOLVED: Application is for private use only. Accessible handbook PDF location remains required. Private use does not authorize committing the PDF or extracted book content to GitHub; keep these local by default.
- D4: Proposed v1 additions in section 4; backup format and installation can use documented defaults after scope confirmation.

## 3. Confirmed functional requirements
- R01 Character library: create/manage multiple characters; recognizable portrait/name; switch quickly without state leaking between characters. Keep unsaved input safe and bind mutations to character ID even if selection changes.
- R02 Portraits: import/update local image; managed copy, preview, fallback, supported-format validation and reasonable size limits. Missing original file must not break saved portrait.
- R03 Guided creation: ordered screens matching the verified PHB Creating a Character process; progress, back navigation, prerequisite validation and resumable draft. Distinguish incomplete drafts from playable characters.
- R04 Existing character entry: enter existing level and all relevant choices/stats/resources; verify consistency and flag incomplete information. Never silently reroll, reset or overwrite supplied values.
- R05 Character sheet: neatly section identity, ability scores/modifiers, saves/skills, class features, feats, species traits, inventory, equipment, weapons, spells and other applicable character details. Explain calculated modifiers and their sources.
- R06 Battle mode: prominent HP/status/resources and categorized actions, bonus actions, reactions plus relevant contextual options. Derive options from character choices, equipment, resources and verified rules. Do not imply all features share identical action costs.
- R07 Dice workflow: select action and weapon/spell/feature as needed; show roll expression, accept reported roll and DM success/hit outcome, then show applicable effects/damage expression. Example dice expressions in the brief are illustrative, not weapon rules. Define whether entry is raw die or total before implementation; label clearly. Cancellation or duplicate confirmation must not accidentally spend resources. Do not determine enemy AC or automatically apply enemy damage.
- R08 Finder: readily accessible search across ability scores, skills, saves and applicable checks; partial case-insensitive names such as Perf find Performance and its signed modifier. Show score plus modifier for six abilities; modifier-only entries remain clearly distinct. Reuse central calculations.
- R09 Status: current/max HP, relevant temporary HP, death save successes/failures and limited-use resources; show used/remaining/max and recharge trigger. Persist resources independently for each character. Verify actual resource maxima/recharge rules; brief examples do not establish handbook values.
- R10 Inventory: search verified catalog; add/remove quantities; inspect item details; equip/use applicable items; create DM custom items with descriptive and structured mechanical fields. Label provenance. Deleting catalog references must not corrupt existing character items.

## 4. Proposed v1 essentials (pending owner acceptance)
- P01 Autosave validated changes transactionally; recover drafts after restart; visible save failure and retry; never silently lose data.
- P02 Explicit short/long rest workflow with preview of affected resources and confirmation; apply each resource's verified recharge rules. Track conditions, exhaustion, concentration and death-state transitions where applicable.
- P03 Level advancement with choice/prerequisite checks and explicit handling of prior-level mistakes. Multiclassing remains D1.
- P04 Spell preparation/known spell bookkeeping, slots and other casting resources, concentration and resource expenditure where applicable to supported classes.
- P05 Versioned backup/export and restore/import covering characters, portraits and custom items; validate before mutation, back up before migration/restore, preserve existing data on failure. Portable character export for transfer.
- P06 Keyboard navigation, labelled inputs, scalable readable UI, clear signed modifiers, non-color status cues and offline operation with no required account/network.
- P07 Basic currency, ammunition and consumable tracking; spending/use only on explicit confirmation. Session history for recent character mutations to explain changes; undo scope to be decided, not implied by history.

## 5. Architecture and data contracts
Confirmed stack; proposed architectural boundaries: desktop presentation → application use cases → pure domain/rules → SQLite/files infrastructure. UI must not duplicate rule calculations or issue SQL directly. Use stable IDs, foreign keys, schema migrations, transactions and explicit units. Store mutable character state separately from versioned verified catalog content. Preserve rule-source references and overrides with reasons. No runtime LLM dependency.

Model characters, progression/choices, ability values/proficiencies, features/resources/recharge policies, items/equipment, spellcasting, conditions, portraits and source citations. Schema details belong to bounded design tasks. Calculated values derive from a shared service; represent proficiency/expertise/overrides explicitly. Store portrait assets in managed local application data, not arbitrary source-file references. Default installation data location proposed: user-local application data; keep live databases and private source content out of source control. Backups must include referenced assets. No cloud sync, telemetry, multiplayer, DM encounter manager, virtual tabletop, automated enemy resolution, homebrew class builder or 2014 compatibility in baseline v1.

## 6. Rules verification protocol
Use accessible supplied PHB 2024; record edition, section, printed/PDF page, interpreted mechanic and representative boundary checks. Build a coverage matrix for every supported option. Mark unverified entries unavailable/draft instead of presenting them as correct. Distinguish app UX choices from book rules. Keep the source PDF and extracted private catalog local and out of GitHub by default. Record the accessible source location under D3 before verification. Do not extract the whole book merely to implement one feature.

## 7. Phases and bounded task queue
Statuses: Proposed → Ready → In progress → Needs verification → Verified; Blocked includes reason and next action. Each task needs a brief objective, scope, dependencies, affected files and executable acceptance criteria before Ready. Split any task too large for one focused implementation/test handoff. All phases currently Proposed. Stack and full content coverage are confirmed; source access, multiclassing/optional rules and D4 remain unresolved. This task remains documentation-only.

| Phase | Small task sequence | Acceptance gate |
|---|---|---|
| 0 Decisions and rules map | 0.1 Resolve D1–D4; 0.2 locate/cite creation rules and content coverage; 0.3 approve stack/data/distribution decisions | Approved baseline and accessible rules source; no guessed mechanics |
| 1 Foundation | 1.1 solution/build/Windows CI; 1.2 domain IDs and migrations; 1.3 repository round-trip; 1.4 shell/design tokens | App launches; clean build; fresh and existing DB paths tested |
| 2 Library and persistence | 2.1 library/create draft; 2.2 switching; 2.3 portraits; 2.4 save recovery; 2.5 backup/restore | Two distinct characters survive restart and restore, including portraits; switching cannot cross-write |
| 3 Character model and existing entry | 3.1 ability/proficiency calculations; 3.2 identity/progression entry; 3.3 features/resources; 3.4 spellcasting entry | Representative supported existing higher-level characters validate and round-trip without resets |
| 4 Guided creation | 4.1 wizard scaffolding/draft resume; 4.2 verified origin choices; 4.3 class/abilities/equipment choices; 4.4 review/finalization | Book-ordered flow; back/resume preserve choices; invalid prerequisites cannot finalize |
| 5 Sheet and finder | 5.1 sectioned sheet; 5.2 finder; 5.3 modifier breakdowns | Perf finds Performance; score/modifier distinctions correct; identity always apparent |
| 6 Inventory and spells | 6.1 catalog search; 6.2 custom items/quantity/equip; 6.3 casting preparation/slots | Verified/custom provenance visible; supported equip/casting effects consistent across screens |
| 7 Session state | 7.1 HP/temp HP/death saves; 7.2 limited resources; 7.3 conditions/concentration; 7.4 rest preview/apply | Boundary rules verified; rest resets only eligible resources; restart preserves session state |
| 8 Battle workflows | 8.1 action availability; 8.2 weapon hit/miss flow; 8.3 spell/feature flows; 8.4 expenditure/history | DM outcome remains explicit; hit shows correct expression; cancel/repeat safe; no enemy AC logic |
| 9 Progression and release | 9.1 level-up slices by supported class; 9.2 content coverage gaps; 9.3 accessibility/Windows smoke; 9.4 packaging/recovery | All approved options covered; migration/restore and end-to-end scenarios pass; known limits disclosed |

Task ordering within phases may follow explicit dependencies: spell/feature entry needs verified catalog slices; final creation choices need those slices too. Never wait until phase 9 to ingest required class rules. Phase 9 closes coverage and adds advancement, rather than supplying earlier tasks' prerequisites. Approve each content slice against D1 before scheduling.

## 8. Verification and release criteria
Domain tests cover score modifier rounding, proficiency, supported stacking, resource limits, rest policies and dice expressions from verified examples. Storage integration checks cover migrations, character isolation, restart and failed/invalid restore. UI checks cover guided and existing entry, rapid switching, finder, portraits, battle hit/miss/cancel and resource persistence. Include one martial and one spellcaster path, then a coverage matrix for all approved class/species options. Check invalid inputs and error recovery as well as happy paths. Record commands, environment and unrun checks. No completion claims based only on screenshots or unit tests.

Release requires the approved scope's acceptance matrix, no unresolved data-loss or rules-correctness defects, working install/update/uninstall data policy, backup/recovery instructions and user-authorized release. Packaging format, Windows minimum version and target architecture are decided in 0.3.

