# Design handoff — <task-id>: <screen/interaction>

Revision: 1
Status: Draft | Ready for implementation | Implemented | Needs verification | Verified
Designer: <role/session>
Technical requirements: <R/P IDs and spec sections>
Dependencies / owned files: <task IDs; intended file ownership>
Related design: DESIGN_SPEC.md sections <numbers>; components <DS-C IDs>

## Objective and boundaries
<User goal; entry/exit routes; non-goals; active character ID and switching behavior.>

## Layout contract
<Text wireframe or annotated local image link. Specify regions, Grid proportions, padding, min/max sizes in DIPs, reading order, wrap/scroll rules and behavior at supported sizes/scaling.>

## Controls and bindings
| Region/control | Component and tokens | Data / format | Command / behavior | Focus order / accessible name |
|---|---|---|---|---|
| <named control> | <DS-C ID / variant> | <intent-level field; units; sign> | <selection versus confirm> | <order; name> |

## State and validation matrix
| Trigger/state | Visible feedback | Enabled actions | Persistence / recovery |
|---|---|---|---|
| Initial / empty | <details> | <details> | <none or exact boundary> |
| Loading / missing catalog | <distinguish states> | <details> | <retry> |
| Invalid input / failed save | <field + summary> | <details> | <preserve input> |
| Unavailable option | <reason> | <details> | <none> |
| Confirm / cancel / duplicate submit | <preview> | <details> | <exact affected state; idempotency> |
| Character switched / stale input | <named target> | <details> | <prevent cross-write> |

Remove inapplicable rows with a reason; add feature-specific states. Specify ranges, culture, units and whether values are raw dice or totals where relevant. Identify source references for any rules-derived constraints.

## Keyboard and accessibility
<Tab order, initial/restored focus, shortcuts, Enter/Escape behavior, accessible names/status announcements, contrast and high-contrast requirements.>

## Assets
<None, or manifest with verified file paths, provenance, dimensions, crop/scaling, transparency and accessibility. Mockups are not production assets unless explicitly identified.>

## Observable acceptance checks
- DES-<task-id>-01: <Given/When/Then; linked technical requirement.>
- DES-<task-id>-02: <Keyboard/state check.>
- DES-<task-id>-03: <Supported layout/scaling check.>

## Open questions
<None before Ready, or issue + owner + dependent work blocked.>

## Implementation and verification handoff
- Developer: <implemented revision; actual files/component mapping; deviations and reason.>
- Designer: <visual/interaction comparison and evidence; unresolved defects.>
- Tester: <checks executed, environment, results, evidence, unrun checks.>
- Coordinator: <final status; summary/catalog updates; next action.>
