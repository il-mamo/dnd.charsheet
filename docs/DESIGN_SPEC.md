# DnD.CharSheet — design specification

Version: 1.1, 2026-10-20. Status: owner-directed dark gothic design baseline for implementation; no screens or components are implemented yet. The owner may revise the visual direction. Routine decisions within this baseline do not need repeated approval.

## 1. Purpose, authority and reading strategy
This document governs visual language, interaction consistency and the Designer-to-Developer handoff. Root `AGENTS.md` governs workflow; `TECHNICAL_SPEC.md` governs product scope and PHB behavior. Design cannot add rules, change calculations or expand functionality. Surface conflicts to the Coordinator before dependent implementation.

Read sections 2–5 for new UI foundations; afterward read only the relevant component and task contract. Keep reusable decisions here, per-screen details in `docs/tasks/<task-id>.design.md`, and implementation status in `PROJECT_SUMMARY.md`. Do not duplicate complete screen specifications in multiple documents. A mockup illustrates intent; the written contract supplies interaction behavior, states and acceptance checks.

## 2. Experience and visual direction
Use a dark gothic fantasy character journal: near-black charcoal and blue-slate surfaces, pale ivory text, weathered silver framing, antique brass details and restrained crimson accents. The owner supplied two visual references on 2026-10-20: a dark stone-and-metal character sheet, and a raven/castle sheet presentation with ironwork and candlelight. Use their atmosphere and palette as inspiration; their embedded text, branding, layout and game values are not requirements or rules sources. The light paper sheets in the second reference do not change the requested dark application theme. The character portrait supplies personality; clear hierarchy and readable numbers keep play dependable.

- Character identity is always apparent. Keep portrait, name and level/class summary visible in the shell; show whose data a dialog will change.
- Support rapid reference: prominent signed modifiers and resource counts, short labels, predictable sections and readily accessible finder/switcher.
- Overview feels like an organized journal; battle mode is a denser workspace with the same controls and vocabulary. Do not create a second visual system.
- Use original gothic imagery and graphic details: weathered iron, carved dark stone, restrained filigree, raven silhouettes, distant spires, mist and small amber/candlelight accents. Place atmospheric backgrounds behind the shell/library and decorative framing around panels; give text, inputs and dense stats opaque dark surfaces. Thin metal-like separators and restrained corner ornaments may decorate panels without obscuring boundaries, focus or hit targets. No detailed texture beneath reading areas, tiny text or animated backgrounds. Do not reproduce reference branding/artwork or import their printed-sheet layout as application navigation.
- Prefer progressive disclosure for descriptions and provenance; never hide essential costs, unavailable reasons or action consequences in hover-only tooltips.
- Use plain English, D&D 2024 terminology and consistent units. Describe actual saved state separately from pending input.

## 3. Initial design tokens
Dimensions are WPF device-independent pixels (DIPs), not physical screenshot pixels. Centralize tokens in application resource dictionaries when UI work begins. Use semantic keys, not repeated literals. A token change must list affected components and verification needs.

| Token | Initial value / use |
|---|---|
| Color.Canvas | #101419 — application background |
| Color.Surface | #1B222A — controls, cards and dialogs |
| Color.SurfaceMuted | #252E38 — secondary grouping |
| Color.Text | #F0EADF — main text |
| Color.TextSecondary | #B8C1CC — supplementary text |
| Color.Border | #7D8997 — meaningful control boundaries |
| Color.Divider | #3A4552 — decorative separation only |
| Color.Accent | #922F49 — primary action/selected emphasis |
| Color.AccentHover | #A83A55 — primary fill on hover; light text |
| Color.AccentPressed | #7C263D — primary fill while pressed; light text |
| Color.Selection | #34485C — selected rows/items with main text and a separate selection indicator |
| Color.DangerFill | #8F2938 — destructive button background; OnAccent text |
| Color.OnAccent | #FFFFFF — text on accent and destructive fills |
| Color.Focus | #8BC9EF — keyboard focus outline |
| Color.Success | #8DC8A4 — positive status with text/icon |
| Color.Warning | #E5BE74 — warning with text/icon |
| Color.Danger | #FF9C9E — error/destructive status with text/icon |
| Color.Brass | #B89A60 — decorative accent, not essential small text |
| Space | 4, 8, 12, 16, 24, 32; default field gap 12, panel padding 16 |
| Radius | 4 controls; 8 panels; circular portrait thumbnails only |
| BorderThickness | 1 normal control/panel; 2 focus outline, not layout-shifting |
| Type.Body | Segoe UI, 14 DIP; field labels 14, helper text minimum 12 |
| Type.Heading | Georgia Bold, 24 page / 18 section; restrained gothic editorial feel; Segoe UI Semibold fallback |
| Type.Stat | Segoe UI Semibold, 28 prominent value; never use decorative numerals |
| Size.Input | minimum height 36; input width follows content/task contract |
| Size.Icon | 16 inline; 20 toolbar; icon-only button minimum 36×36 with accessible name |
| Motion | optional short transitions up to 150 ms; respect reduced-animation settings |

Use pale Text/TextSecondary on dark Canvas/Surface/SurfaceMuted. Primary buttons use Accent/AccentHover/AccentPressed with OnAccent; destructive buttons use DangerFill with OnAccent. Danger is light error text/icon color, not a button fill. Success/Warning/Danger are text/icon accents on dark surfaces, paired with labels. Secondary buttons use SurfaceMuted, Text and Border. Keep primary actions distinguishable with a meaningful border/shape; crimson fill alone may not separate enough from a dark panel. Hover and selection must remain visible without relying only on hue. Disabled explanations remain readable; do not dim the whole component using a low-opacity overlay.

Normal text must meet a 4.5:1 contrast target; large text and meaningful control/focus boundaries a 3:1 target against adjacent colors. Check actual rendered combinations, including hover, selection, validation and disabled explanations. Never assume every token pair is suitable. Background art must be covered by an opaque reading surface or scrim with measured worst-case contrast. Use system colors in Windows high-contrast mode. Dark is the required v1 baseline; a separate light theme is not required.

## 4. Shell, layout and navigation
- Top identity strip: active portrait/name/level and switch-character control on the left; finder and save-state indicator readily visible. Selection changes update all bound content atomically.
- Stable primary destinations: Character, Inventory, Spells (as applicable), and Battle. Library/settings/backup remain reachable. Character sub-sections use consistent tabs or in-page navigation; the Designer selects one pattern in the shell contract.
- On wide windows use a primary detail region and a secondary summary/resource region. At reduced width stack regions in reading order. Do not hardcode absolute screen coordinates; use Grid sizing, minimum widths, wrapping and deliberate scroll regions.
- Contract each screen at 1280×720 and 1920×1080, and account for 100%, 150% and 200% Windows scaling. WPF DIPs and effective client area must be recorded in evidence. Stacking/scrolling may replace columns; bottom actions and validation must remain reachable. Avoid horizontal scrolling except where a genuine data table requires it.
- Battle places HP, conditions, concentration and resource counts near action selection. Keep actions, bonus actions and reactions explicitly labelled. Unavailable options explain why. An option may be documented without fully automating its world effects.
- Guided creation follows the five PHB primary steps in technical-spec section 9; subdivisions preserve that order. Keep progress, Back, Save/close and Next/Finish predictable. Earlier changes preview dependent invalidations.
- Existing-character entry permits direct section navigation; incomplete state is explicit. Dialogs must not silently normalize or reset transcribed values.

## 5. Interaction and accessibility rules
Prefer WPF standard controls with shared styles and UI Automation support. A custom control requires a documented behavior/accessibility need; appearance alone rarely warrants replacing a standard control.

| Data / interaction | Preferred control and contract |
|---|---|
| Names, free-form short values | TextBox with persistent label; character limits and validation defined by task |
| Descriptions/backstory | Multiline TextBox with wrapping and explicit save behavior |
| Small exclusive choice | RadioButton group with a labelled group heading |
| Long catalog choice | Searchable list/selector with item details and explicit selection |
| Small fixed option list | ComboBox; never rely on color-only choices |
| Numeric totals/quantities | Validated numeric TextBox plus labelled increment/decrement buttons when useful; culture/sign/range specified |
| Boolean setting | CheckBox; distinguish false from unknown when the domain needs both |
| Action command | Button with verb label; no clickable decorative text masquerading as a button |
| Item/spell collections | ListView/DataGrid as appropriate; sortable columns only when meaningful; selection and activation distinct |
| Read-only stats | Labelled text/stat component, not disabled input fields |
| HP/resource changes | Dedicated adjustment command with preview; no destructive update on mere selection |

All interactive controls require meaningful accessible names, visible keyboard focus and a logical tab order. Enter submits only the active intended command; Escape cancels a dialog without spending resources. Do not hijack text-editing shortcuts. Initial application shortcuts: Ctrl+K opens finder, Ctrl+Shift+C opens character switcher, Escape closes the current transient surface; Designer checks collisions before adding more. Restore focus to the invoking control after dismissal. Announce important validation/save errors through accessible UI state.

Show validation beside its field and an overall summary for multi-field submission failures. Preserve entered values on errors. Required fields have textual indication. Prevent duplicate submissions while a mutation is pending. Display saving/saved/failed states accurately; only show saved after successful persistence. Confirmation is for consequential changes (restore, deletion, rest application or dependent-choice invalidation), not every navigation or harmless edit.

Search must handle empty queries, zero results and many results. Keep exact display names and modifiers legible. An empty list must distinguish no data from no search matches and missing local catalog. Loading and errors must not look like empty success. Disabled controls include an understandable reason; do not rely only on tooltips.

## 6. Managed component catalog
Stable component IDs communicate shared intent. The Graphics Designer owns design definitions and creation of graphic assets; Developer owns working controls/styles and asset integration. Reuse before creating variants. A row below is a planned contract, not an implemented component.

| ID | Responsibility | Required states / details |
|---|---|---|
| DS-C01 CharacterIdentity | Portrait, name, class/level and switching affordance | Active, draft, no portrait, long name; clear active-character label |
| DS-C02 StatValue | Ability score/modifier or modifier-only check | Signed modifier, score label when present, provenance/details; no empty score box for skills |
| DS-C03 ResourceCounter | Used/remaining/max and recharge label | Zero, full, exhausted, pending edit, unavailable; different units never conflated |
| DS-C04 HealthPanel | Current/max/temp HP and death-save status | Damage/heal adjustments, zero HP, invalid input; temp HP distinct from healing |
| DS-C05 ActionOption | Action type, effect, cost and availability | Available, unavailable with reason, selected, confirmation pending; explicit DM outcome |
| DS-C06 CatalogPicker | Search/select verified or custom records | Empty/query/results/missing catalog; custom provenance; keyboard navigation |
| DS-C07 SectionPanel | Consistent heading, content and optional commands | Expanded/collapsed if needed; labelled sections, restrained border |
| DS-C08 FieldFeedback | Label, input, help and validation | Pristine, modified, invalid, read-only and disabled; text explains error |
| DS-C09 Portrait | Managed-image preview and upload/update | Fit/crop preview, missing/broken fallback, invalid format/size; no identity loss |
| DS-C10 ConditionBadge | Named condition/status and detail access | Active/inactive as needed; textual name plus optional icon |
| DS-C11 SaveStatus | Persistence feedback | Saving, saved, failed/retry; character identity retained |
| DS-C12 Confirmation | Consequence preview and explicit choice | Named target, affected data/resources, confirm/cancel; initial focus favors safe action |

Before implementing a component, its task contract must define data inputs, commands, validation, keyboard behavior, layout constraints and supported states. Add implementation path, examples and verified status to this catalog after delivery. Shared WPF styles belong in `Presentation/Design/` or the equivalent solution path established by task 0.3; do not create a second token source in individual screens. A later showcase/demo screen may be used for checking components, but is not a required product feature.

## 7. Designer-to-Developer contract
Create `docs/tasks/<task-id>.design.md` for each new screen or significant interaction. For simple reuse, a short task note referencing component IDs and exact states is sufficient. Keep one current contract and increment its revision when behavior/layout changes. Designer marks it Ready for implementation only when material questions are resolved; this is a role handoff, not a new human approval gate.

Use the template in `docs/templates/DESIGN_HANDOFF_TEMPLATE.md`. Required content:
1. Task ID, revision/status, linked technical requirements and non-goals.
2. User objective, entry/exit routes and active-character binding.
3. Text wireframe or annotated mockup with named regions and reading order; sizes in DIPs, Grid proportions, minimums, wrapping/scrolling behavior.
4. Component IDs/variants and token keys; data binding fields, commands/events and formatting. Names describe intent; Developer maps them to concrete view models.
5. Control-by-control behavior, focus/tab order/shortcuts and every relevant empty/error/loading/disabled/validation state.
6. Effects of confirm/cancel, persistence boundary, resource spending, rapid switching and stale input.
7. Asset manifest where needed: source/generation provenance, file, purpose, dimensions, transparency, scaling/crop behavior and accessible treatment.
8. Numbered observable acceptance checks linked to requirement IDs, plus any unresolved questions. A screen is not Ready with unanswered consequential questions.

Developer acknowledges the contract revision, implements it, and records actual component/file mappings plus any justified deviations. Do not silently guess missing consequential behavior. Designer inspects resulting screenshots and interactions against the contract. Tester validates function and accessible keyboard/state behavior independently; screenshots alone cannot prove these. Failed checks return to Developer with the contract/check ID. Coordinator records Verified and summary updates only after relevant checks pass.

## 8. Assets and maintenance
The Graphics Designer owns asset concepts, creation/generation, variants, export, visual QA and the asset manifest, including backgrounds, gothic ornaments, frames, icons and placeholders. Developer requests missing assets from that role and implements their specified placement/scaling; Developer does not independently invent or replace artwork. User-uploaded portraits remain user content and are never automatically restyled. Prefer code-native vector icons for interface symbols; the Designer may author these without raster generation. Use bitmap generation for suitable original gothic illustrations/textures/placeholders through the image-generation skill when assets are actually requested by an implementation task. Assets must be local/offline and have known provenance. Store reusable production artwork in the solution asset folder chosen during architecture setup, with an asset manifest recording ID, creator/source, purpose, format, resolution, transparency, crop/scale rules, placement and contrast treatment. Reference images are inspiration, not production assets. Favor original iron/stone/raven/spire motifs; use darkness and atmosphere without making dense UI illegible. Provide opaque dark fallbacks when an asset cannot load. Do not bake text, stats or interactive controls into images. Character portraits are user content, distinct from repository assets. No remote fonts, trackers or runtime asset downloads. Decorative art has no essential meaning and must not compete with numbers or controls.

Designer maintains this document's tokens, catalog, interaction conventions and cross-screen consistency. Developer updates the implementation mapping; Coordinator serializes shared document changes. Do not append transcripts or duplicate source code/XAML here. For a design change, record a short reason and affected token/component/contract IDs; update existing definitions and recheck affected states. Owner direction overrides initial visual defaults; a material navigation/workflow change conflicting with product requirements is clarified first.

## 9. Design completion checklist
- Screen/task matches its requirement IDs and contract revision; no invented PHB mechanics.
- Active character, action type, resource costs and save state remain clear.
- Required states, long text, no portrait, empty catalog and errors are exercised.
- Keyboard operation, focus restoration, accessible naming, contrast and scaling checked; unrun checks disclosed. Check light text on every dark control state and art background; borders/focus must remain visible.
- Graphics Designer supplies original gothic assets and manifest; Developer integrates them without baking functional text into art.
- Layout uses shared tokens/components; Designer records visual comparison and Developer explains deviations.
- Tester records actual functional/interaction results; catalog and project summary reflect implemented versus planned state.

## 10. Design revision note
1.1 (2026-10-20): owner replaces the initial light journal direction with dark gothic surfaces, pale text, iron/silver/brass details and atmospheric artwork. Affects color/heading tokens, panel framing, backgrounds, all DS-C01–DS-C12 states and future screen contracts. Graphics Designer ownership of all asset creation is explicit. No application artwork or controls are created by this documentation revision.
