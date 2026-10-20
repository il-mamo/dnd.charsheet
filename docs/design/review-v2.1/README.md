# Adventure-aware mockup review 2.1

Status: all three revised review boards delivered; owner visual approval pending.

Graphics Designer review milestone for R01/R07/R11/P01/P05/P06 and tasks 2.6-2.8, with existing screen dependencies. Scope: replace review imagery for Adventure startup, management, membership and player-only action resolution. Non-goals: application code, production artwork and final developer metadata. Existing gothic palette and 1920x1080 at 100% target retained.

Review snapshot: main at 484a350d8faa811835ab294f472b41d1f55dbc55 plus the existing uncommitted planning diff and this bounded mockup/documentation update. Existing changes preserved. No Designer branch created. Later implementation contracts require a Developer-created task branch.

Delivered files:

- [board-a.png](board-a.png): Adventure selection, Adventure characters, character overview, guided creation.
- [board-b.png](board-b.png): Inventory, spells, battle, player action resolution.
- [board-c.png](board-c.png): Adventure management, creation with first character, unassigned assignment, deletion/transfer previews.

Mode: built-in image_gen, ui-mockup; prior local boards used as visual references. Original AI-generated decorative artwork, no handbook images. Generated values and prose are illustrative, not rules evidence. Review boards are opaque composites and are not production assets. Written DESIGN_SPEC.md sections 11.4 and 11.7 and TECHNICAL_SPEC.md section 12 govern behavior.

Acceptance: visible Adventure context; explicit startup selection; creation includes first member; deletion retains unassigned characters; assignment required before play; source/destination shown for transfer; action resolution has no role selector. Inspect generated images and record deviations before delivery. Native interaction and contrast verification remain unrun; these are concept images only.


## Visual inspection and limits
All three generated images were inspected inline. Required concepts are present: explicit Open Adventure, scoped roster, Adventure identity, player-only roll/result drawer, first-member creation, unassigned cards with Assign Adventure instead of Play, source/destination transfer and deletion retaining characters. Exact prompts: [prompts.md](prompts.md). Built-in image_gen used; no CLI fallback.

Known concept deviations excluded from implementation:
- Board A welcome shows an Adventure name in the top strip before Open Adventure. This is preview only; remove that identity before explicit selection. Generated helper sentences can be omitted per the concise-copy contract.
- Example numbers, slots, resource labels, conditions and descriptions are unverified. Do not encode them as rules. Inventory provenance must use PHB 2024 / DM custom, separately from acquisition method.
- Board B damage heading says reported by DM; the player enters their external damage roll when applicable. Only hit/success is adjudicated by the DM. Read-only expressions must be labelled text, despite field-like rendering.
- Board C transfer preview beside deletion is a review inset showing two separate designs, never concurrent/nested modals. Header illustrates in-session management; startup management has no active Adventure/character. Back/search follow the written plan despite image omissions.
- Portrait/name consistency, typography and exact geometry remain illustrative. No user portraits are changed.

Written specifications govern these discrepancies. No application code or migrations changed. Final developer metadata and production assets await owner approval. Native UI and contrast checks are unrun. Documentation whitespace and file-presence checks passed.
