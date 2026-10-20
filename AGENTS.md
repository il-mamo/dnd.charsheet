# DnD.CharSheet — agent instructions

## Purpose and authority
Build a Windows character-management application for Dungeons & Dragons using the Player’s Handbook 2024 exclusively. Repository: https://github.com/il-mamo/dnd.charsheet. No Jira integration.

Follow the user's current instructions first. This file governs development workflow; `docs/TECHNICAL_SPEC.md` governs intended product behavior; `docs/PROJECT_SUMMARY.md` describes verified current state. `docs/AGENTS_ROLES.md` defines development roles. `docs/DESIGN_SPEC.md` governs visual/interaction conventions within the technical scope. These roles are development responsibilities, not runtime AI features.

## Start every task
1. Read this file and `docs/PROJECT_SUMMARY.md`.
2. Read the assigned role in `docs/AGENTS_ROLES.md` and only relevant specification sections.
3. Identify the phase/task ID, dependencies, acceptance criteria and affected files.
4. Inspect the working tree before edits; preserve unrelated user changes.
5. Read only the code/tests necessary for the task. Expand inspection only when evidence warrants it.

Do not read the whole codebase, whole handbook, generated files, dependency trees or historical logs by default. Use targeted `rg` searches and bounded reads. Reuse unchanged context. Summaries guide exploration but do not replace verification.

## Clarification before implementation
Ask focused, grouped questions before consequential implementation when missing information changes scope, architecture, rules interpretation, persistence, compatibility, destructive behavior or acceptance criteria. Explain why it matters and offer a recommendation. Wait on dependent work; continue unrelated authorized work. Existing answers remain authoritative; do not repeatedly request approval. Resolve minor local choices using established patterns and record material assumptions.

Do not implement a proposed or blocked requirement as approved. If code conflicts with the specification, report the discrepancy; do not rewrite requirements to justify the code. Explicit user decisions authorize corresponding specification updates; otherwise request approval for material requirement changes.

## Bounded implementation workflow
- Select one ready task from the specification, normally one demonstrable behavior. Split work that crosses several independent areas or cannot be verified in one focused handoff.
- Record a brief task contract: ID, objective, scope/non-goals, requirements, affected files, dependencies, acceptance checks, owner.
- Before implementing a change, the Developer creates a dedicated task branch after inspecting existing branches/worktrees and the working tree. Record the branch name and base revision in the task contract; do not implement on main. Resume fixes for that change on its existing branch.
- For visible UI work obtain the Graphics Designer's concrete design contract before implementation. Read relevant `docs/DESIGN_SPEC.md` sections and use `docs/templates/DESIGN_HANDOFF_TEMPLATE.md` for `docs/tasks/<task-id>.design.md`. Reuse approved components when no new design is needed; simple reuse needs only a component/state reference in the task.
- Implement the smallest maintainable change. Keep rules/domain logic separate from UI and storage. Avoid unrelated refactors, speculative abstractions and unapproved dependencies.
- Run targeted build/tests and applicable interaction checks. Rules calculations and data migrations need meaningful boundary tests. Never claim unexecuted checks passed.
- Hand the Developer's same task branch to both Graphics Designer and independent Tester with base/review revisions, diff, intended behavior, requirement IDs and evidence. Both roles work on that branch; Designer records applicability when no visual work is involved. Fix findings on the same branch and rerun affected checks.
- Update the project summary and phase/task status with verified facts. A phase is complete only when its acceptance gate passes.

## Rules and content integrity
The supplied PHB 2024 is the rules authority. Do not substitute 2014 rules, third-party references, web snippets or model memory for verified book content. Read only relevant pages and record printed page plus PDF page where different. Mark uncertain rules pending verification; do not invent missing data. Keep source PDFs/private content out of Git until distribution/content handling is decided. DM custom inventory is explicitly labelled custom, separate from the verified catalog.

## Collaboration and Git
Use Developer, Graphics Designer and Tester responsibilities in `docs/AGENTS_ROLES.md`. Delegate bounded tasks when agent tooling and authorization permit; do not claim persistent agents exist. Each change has one Developer-created branch shared through Designer and Tester handoffs; do not create separate role branches. Serialize branch switches, commits and overlapping edits in a shared checkout. Parallel work requires disjoint file ownership and a stable review snapshot; invalidate affected reviews after further changes. Inspect existing branches and worktrees before creating new ones; do not check out the same branch into multiple worktrees. Preserve unrelated changes and do not move them between branches without authorization. Do not discard changes, rewrite history, push to main, merge, publish or release without user authorization. No Jira dependency or issue-prefix requirement. Task IDs come from the specification.

## Summary maintenance and reporting
Keep `docs/PROJECT_SUMMARY.md` concise (target under 200 lines), replacing stale state rather than appending transcripts. Include current architecture/map, decisions, implemented behavior, active/next task IDs, checks actually run, limitations and blockers. Do not copy the full specification or large logs. When handing off report task ID, changed files, acceptance results, unrun checks, unresolved questions and next responsible role. Keep intended behavior, actual implementation and summary distinct and consistent.
