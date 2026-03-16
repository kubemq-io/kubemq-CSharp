---
name: gap-implementation
description: Implement gap-close specs into actual SDK code. Reads all spec files, executes implementation in phased work units with dependency ordering, runs QA (code-review + code-simplifier) after each phase, produces a retrospective, and outputs a final verification report.
argument-hint: SDK name (go, java, csharp, python, js)
user-invocable: true
allowed-tools: Read, Grep, Glob, Write, Edit, Bash, Agent, AskUserQuestion
---

# Gap Implementation

Implement gap-close specs into SDK code: $ARGUMENTS

## Overview

Reads the completed gap-close specs (13 categories) and implements them in the actual SDK codebase. Work is organized into 3 phases with dependency ordering. After each phase, QA agents (code-review + code-simplifier) validate the changes. A retrospective is generated at the end to feed learnings back into future SDK runs. No automatic git commits are made — all changes are left uncommitted for the user to review.

**KEY PRINCIPLE:** The spec is authoritative. Implement exactly what the spec says. When the spec is ambiguous, check the Golden Standard. When both are ambiguous, ask the user.

---

## SDK Configuration

| SDK | Source Root | Build Command | Build Incremental | Test Command | Lint Command | Test Disable | Build Config File |
|-----|-----------|---------------|-------------------|--------------|-------------|-------------|-------------------|
| java | `kubemq-java/` | `cd kubemq-java && mvn clean compile -q` | `cd kubemq-java && mvn compile -q` | `cd kubemq-java && mvn test -q` | `cd kubemq-java && mvn checkstyle:check` (after WU-4) | `@Disabled("reason")` | `pom.xml` |
| go | `kubemq-go/` | `cd kubemq-go && go build ./...` | `cd kubemq-go && go build ./...` | `cd kubemq-go && go test ./...` | `cd kubemq-go && golangci-lint run` | `t.Skip("reason")` | `go.mod` |
| csharp | `kubemq-csharp/` | `cd kubemq-csharp && dotnet build` | `cd kubemq-csharp && dotnet build --no-restore` | `cd kubemq-csharp && dotnet test` | N/A | `[Ignore("reason")]` | `*.csproj` |
| python | `kubemq-python/` | `cd kubemq-python && uv run python -m py_compile kubemq/*.py` | same | `cd kubemq-python && uv run pytest` | `cd kubemq-python && uv run ruff check` | `@pytest.mark.skip(reason="reason")` | `pyproject.toml` |
| js | `kubemq-js/` | `cd kubemq-js && npm run build` | same | `cd kubemq-js && npm test` | `cd kubemq-js && npm run lint` | `test.skip("reason", ...)` | `package.json` |

**Notes:**
- Python uses `uv` as the package manager (per project convention). Verify `pyproject.toml` exists before using `uv run`. Verify `ruff` is in dev dependencies before using lint command.
- Java lint command becomes available after WU-4 (Code Quality) implements checkstyle. Use N/A before that.
- JS build command assumes a `build` script in `package.json`. Verify with `cat package.json | grep build` before use.
- **Build Incremental** is used when the full build times out (>2 minutes). It skips clean/restore steps.
- **Test Disable** is the language-appropriate annotation/mechanism for disabling failing tests.
- **Build Config File** is the primary build configuration file for that SDK — an automatic serialization trigger for parallel execution.

---

## Step 1: Validate Input

**$ARGUMENTS is REQUIRED.** Must be one of: `go`, `java`, `csharp`, `python`, `js`

**If empty:** STOP, ask: "Which SDK should I implement specs for? Specify one: go, java, csharp, python, js"

Set variables from the SDK Configuration table:
- `SDK_NAME` = $ARGUMENTS (e.g., `java`)
- `SDK_LABEL` = Display name (e.g., `Java`)
- `SPECS_DIR` = `clients/gap-close-specs/{SDK_NAME}/`
- `LANG_CONSTRAINTS_DIR` = `clients/skills/gap-close-spec/` (language constraint files are stored alongside the spec-generation skill)
- `SDK_ROOT` = Source Root from config table
- `BUILD_CMD` = Build Command from config table
- `BUILD_CMD_INCR` = Build Incremental from config table
- `TEST_CMD` = Test Command from config table
- `TEST_DISABLE` = Test Disable annotation from config table
- `BUILD_CONFIG_FILE` = Build Config File from config table

### Validation Checks

1. Verify `{SPECS_DIR}/SPEC-SUMMARY.md` exists. If not: STOP, tell user to run `gap-close-spec {SDK_NAME}` first.
2. Verify `{SPECS_DIR}/COVERAGE-CHECKLIST.md` exists.
3. Check if `{LANG_CONSTRAINTS_DIR}/LANGUAGE-CONSTRAINTS-{SDK_NAME}.md` exists. If not: WARN the user: "No language constraints file found for {SDK_NAME}. Implementation agents will have no language-specific guidance. Consider creating `{LANG_CONSTRAINTS_DIR}/LANGUAGE-CONSTRAINTS-{SDK_NAME}.md` before proceeding." Ask via `AskUserQuestion` whether to continue or stop.
4. Run `{BUILD_CMD}` to verify baseline compilation. If the command times out, try `{BUILD_CMD_INCR}`. If both fail: STOP, tell user to fix build first.
5. Run `{TEST_CMD}` and capture baseline test count and pass/fail status. Record as `BASELINE_TESTS`. If the command fails because no tests exist, record `BASELINE_TESTS = 0 tests (no test suite)` and proceed.

### Resume Check

Check for existing tracking files. If `{SPECS_DIR}/PROGRESS.md` exists:
1. Verify `{SPECS_DIR}/IMPLEMENTATION-PLAN.md` also exists. If PLAN is missing but PROGRESS exists, re-run Steps 2-2.5 to rebuild the plan.
2. If both exist and PROGRESS.md contains work units with status other than PENDING:
   - Present to user: "A previous implementation run was found with {N} completed, {M} blocked, {K} skipped work units. Resume from last completed WU, or start over?"
   - If resuming: read `{SPECS_DIR}/ORCHESTRATOR-STATE.md`. Verify its `Next WU` is not already COMPLETED in PROGRESS.md (if it is, advance to the next PENDING WU). Jump to the appropriate phase step and continue.
   - If starting over: delete PROGRESS.md, IMPLEMENTATION-PLAN.md, ORCHESTRATOR-STATE.md, and any `wu-*-output.md` files. Proceed normally.

---

## Step 2: Load Context & Build Implementation Plan

### Read These Files

1. `{SPECS_DIR}/SPEC-SUMMARY.md` — Overview, dependency graph, implementation sequence
2. `{SPECS_DIR}/COVERAGE-CHECKLIST.md` — All REQ-* items to implement
3. `{LANG_CONSTRAINTS_DIR}/LANGUAGE-CONSTRAINTS-{SDK_NAME}.md` — Language-specific rules (if exists)
4. `{SPECS_DIR}/RETROSPECTIVE-NOTES.md` — Lessons from spec generation (if exists)

**Do NOT read all 13 spec files now.** They will be read by individual implementation agents. Only read the summary and checklist for planning.

### Resolve Open Questions

Read the "Open Questions" section of SPEC-SUMMARY.md. For each unresolved question that affects implementation (e.g., type naming, API design decisions):
- Present the question to the user via `AskUserQuestion`
- Record the user's answer in IMPLEMENTATION-PLAN.md under an "## Open Question Resolutions" section
- Pass relevant answers to implementation agents via additional context in the IMPL-AGENT-PROMPT

### Parse Dependency Graph

From SPEC-SUMMARY.md's "Implementation Sequence" section, **dynamically** extract the 3-phase dependency graph. Map each spec to a Work Unit (WU). Identify specs with internal phases (look for "Phase 1" / "Phase 2" / "Phase 3" subdivisions in the sequence table) and split them into separate work units.

**IMPORTANT:** Do NOT use a hardcoded WU table. The WU assignments must be derived from the actual SPEC-SUMMARY.md content for the target SDK. Different SDKs may have different phase structures, dependency orderings, and sub-phase splits.

### Build Work Unit Table

For each entry in SPEC-SUMMARY.md's "Implementation Sequence" table:
1. Assign a sequential WU number (WU-1, WU-2, ...)
2. Record the spec file, phase, and dependencies
3. For specs with internal phases, create one WU per phase and record which REQ-IDs belong to each sub-WU
4. Identify parallel groups: WUs within the same phase that have no cross-dependencies
5. For any WU where ALL scoped REQ-IDs are COMPLIANT in COVERAGE-CHECKLIST.md, mark it as `COMPLETED (all REQs compliant)` — do not launch an agent for it

**EXAMPLE (Java SDK — derive dynamically, do not use verbatim for other SDKs):**

```
Phase 1 — Foundation (Sequential):
  WU-1:  Spec 01 (Error Handling)     — deps: none
  WU-2:  Spec 02 (Connection)         — deps: WU-1
  WU-3:  Spec 03 (Auth)               — deps: WU-1, WU-2
  WU-4:  Spec 07 (Code Quality)       — deps: WU-1

Phase 2 — Features (Partially Parallel):
  WU-5:  Spec 04-Phase1 (CI infra)    — deps: none           [PARALLEL-A]
  WU-6:  Spec 05-Phase1 (Logger)      — deps: none           [PARALLEL-A]
  WU-7:  Spec 08 (API Completeness)   — deps: none           [PARALLEL-A]
  WU-8:  Spec 09-Phase1 (Validation)  — deps: WU-1
  WU-9:  Spec 05-Phases2-3 (OTel)     — deps: WU-1, WU-2, WU-4

Phase 3 — Quality & Polish (Partially Parallel):
  WU-10: Spec 10 (Concurrency)        — deps: WU-1, WU-2, WU-8, WU-9
  WU-11: Spec 11 (Packaging)          — deps: none           [PARALLEL-B]
  WU-12: Spec 13 (Performance)        — deps: none           [PARALLEL-B]
  WU-13: Spec 12 (Compatibility)      — deps: WU-11
  WU-14: Spec 06 (Documentation)      — deps: none           [PARALLEL-B]
  WU-15: Spec 04-Phases2-3 (Tests)    — deps: all Phase 2
  WU-16: Spec 09-Phase2 (Verb align)  — deps: WU-7 (subscribe return types needed for verb alignment)
```

### Sub-Phase REQ-ID Scoping

For split specs, read the spec file's table of contents or REQ sections to determine which REQ-IDs belong to each phase. Record these in the implementation plan. The orchestrator must read the relevant spec's "Implementation Order" or internal phasing section — do NOT hardcode REQ-ID lists.

For SDKs without a multi-phase spec split, collapse sub-WUs into a single WU covering the full spec.

### File Conflict Detection

Before marking work units as parallel, compare their "Files to Modify" and "Files to Create" sections. Read SPEC-SUMMARY.md's "Files to Modify (Combined)" table.

**Automatic serialization triggers** — if two WUs in the same parallel group both modify ANY of these, serialize those WUs:
- The SDK's `{BUILD_CONFIG_FILE}` (from SDK config table)
- `README.md`
- Main client class files (identify from SPEC-SUMMARY.md's "Files to Modify" table — typically the top-level client file modified by the most specs)

Use these canonical resolution values in the File Conflict Analysis table:
- `PARALLEL` — all WUs can run simultaneously
- `SERIALIZE-ALL` — all WUs must run sequentially
- `SERIALIZE-{WU-A}-BEFORE-{WU-B}` — specific ordering; other WUs may still run in parallel

### Write Tracking Files

Write `{SPECS_DIR}/IMPLEMENTATION-PLAN.md`:
```
# {SDK_LABEL} SDK — Implementation Plan

**Generated:** {date}
**Source:** {SPECS_DIR}/SPEC-SUMMARY.md

## Open Question Resolutions

| # | Question | User's Answer |
|---|----------|--------------|
| 1 | {question from SPEC-SUMMARY.md} | {answer} |

## Work Unit Execution Order

| WU | Spec | Phase | Dependencies | Parallel Group | Status |
|----|------|-------|-------------|----------------|--------|
| WU-1 | 01-error-handling | 1 | — | — | PENDING |
| ... | | | | | |

## Sub-Phase Scope Filters

| WU | Spec | Phase | Scoped REQ-IDs |
|----|------|-------|----------------|
| {only for split-spec WUs} | | | |

## File Conflict Analysis

| Parallel Group | WUs | Shared Files | Resolution |
|---------------|-----|-------------|------------|
| PARALLEL-A | WU-5, WU-6, WU-7 | {list or "none"} | {PARALLEL / SERIALIZE-ALL / SERIALIZE-X-BEFORE-Y} |
| PARALLEL-B | WU-11, WU-12, WU-14 | {list or "none"} | {resolution} |

## Breaking Changes

{Copy the Breaking Changes Summary from SPEC-SUMMARY.md for reference by implementation agents}
```

Write `{SPECS_DIR}/PROGRESS.md` with two sections — a compact **Status Table** (for agents to read) and a detailed **WU Detail Log** (for humans and QA):

```
# {SDK_LABEL} SDK — Implementation Progress

**Started:** {date}
**Baseline Tests:** {BASELINE_TESTS}

## Status Table

This compact table is the primary interface for agents. Read ONLY this section for status checks.

| WU | Spec | Phase | Status | Build | Tests | REQs Done |
|----|------|-------|--------|-------|-------|-----------|
| WU-1 | 01 Error Handling | 1 | PENDING | — | — | 0/9 |
| WU-2 | 02 Connection | 1 | PENDING | — | — | 0/6 |
| ... | | | | | | |

## WU Detail Log

Detailed per-WU output. Updated by the orchestrator from wu-{N}-output.md files.

### WU-1: Spec 01 (Error Handling)
- **Status:** PENDING
- **REQs:**
  - [ ] REQ-ERR-1: {title} | QA: —
  - [ ] REQ-ERR-2: {title} | QA: —
  - ... (all REQs from this spec)
- **Build:** —
- **Tests:** —
- **Notes:** —

### WU-2: Spec 02 (Connection)
... (same format for all WUs)

## Summary
- Total REQs: {n}
- Implemented: 0
- Blocked: 0
- Skipped: 0
```

For REQs that are split across two WUs, use separate entries:
```
  - [ ] REQ-TEST-1 (WU-5 scope: JaCoCo config) | QA: —
  - [ ] REQ-TEST-1 (WU-15 scope: new unit tests) | QA: —
```

Populate REQ lists from COVERAGE-CHECKLIST.md.

---

## Step 2.5: Write Initial Orchestrator State

Write `{SPECS_DIR}/ORCHESTRATOR-STATE.md`:
```
# Orchestrator State Checkpoint

**Last Updated:** {date}
**Current Phase:** 1
**Current Step:** 3 (Execute Phase 1)
**Next WU:** WU-1
**SDK:** {SDK_NAME}

## Completed WUs
(none)

## Blocked WUs
(none)

## Skipped WUs
(none)

## Phase Metrics
| Phase | WUs Launched | Completed | Blocked | Skipped | Build Fix Attempts | QA Issues |
|-------|-------------|-----------|---------|---------|-------------------|-----------|
| 1 | 0 | 0 | 0 | 0 | 0 | 0 |
| 2 | 0 | 0 | 0 | 0 | 0 | 0 |
| 3 | 0 | 0 | 0 | 0 | 0 | 0 |

## Notes
{any context needed by a resumed session}
```

**Contract:** The orchestrator updates this file after every WU completion and phase transition. When resuming:
1. Read `Current Phase` and `Next WU`
2. Read `Completed WUs` list to verify against PROGRESS.md Status Table
3. If `Next WU` is marked COMPLETED in PROGRESS.md, advance to the next PENDING WU
4. Jump to the step corresponding to `Current Phase`

---

## Step 3: Execute Phase (Generic)

This step is a **parameterized template** used for all 3 phases. The orchestrator executes it with `{PHASE_NUM}` = 1, 2, or 3.

### Identify WUs for This Phase

Read IMPLEMENTATION-PLAN.md's "Work Unit Execution Order" table. Extract all WUs where Phase = `{PHASE_NUM}`.

Separate into:
- **Parallel group WUs:** Those with a Parallel Group value (e.g., PARALLEL-A)
- **Sequential WUs:** All others, ordered by WU number

### Execute Parallel Group (if any)

Read IMPLEMENTATION-PLAN.md's "File Conflict Analysis" for this phase's parallel group.

Based on the Resolution value:
- `PARALLEL`: Launch all group WUs simultaneously using multiple Agent tool calls in a single message
- `SERIALIZE-ALL`: Launch WUs one at a time in WU-number order
- `SERIALIZE-{WU-A}-BEFORE-{WU-B}`: Launch WU-A first, wait for completion, then launch WU-B. Other group WUs can run in parallel with WU-A or WU-B as appropriate.

After all parallel group agents complete:
1. Run `{BUILD_CMD}` to verify no conflicts
2. If build fails due to conflicting changes:
   - For `{BUILD_CONFIG_FILE}`: read the file, identify each WU's intended changes from their spec, apply in WU-number order (lower first)
   - For source files: identify specific methods/sections each WU added, verify no duplicates, merge in WU-number order
   - Note in PROGRESS.md: "Post-parallel merge required for {file}"
3. After all parallel agents complete, check `git status` for files created by multiple agents. Compare per-WU output files (`wu-{N}-output.md`) to detect overlapping file creation. If found, merge manually.

### Execute Sequential WUs

For each sequential WU (in WU-number order, respecting dependency chain):

1. **Skip check:** If the WU is already COMPLETED (all-compliant) or SKIPPED in PROGRESS.md, skip it.
2. **Dependency check:** Verify all dependency WUs are COMPLETED or PARTIAL. If any dependency is BLOCKED, mark this WU as `SKIPPED (dependency WU-{N} blocked)`.
3. **Launch agent** using IMPL-AGENT-PROMPT with all variables substituted.
4. **Wait for completion.** The agent writes its results to `{SPECS_DIR}/wu-{N}-output.md`.
5. **Merge output:** Read `wu-{N}-output.md`. Update PROGRESS.md's Status Table (status, build, tests, REQs done count) and WU Detail Log (REQ checkboxes, notes).
5b. **Deferred-work detection:** After reading `wu-{N}-output.md`, scan the Notes section and
    REQ Status entries for keywords: "deferred", "integration pending", "standalone infrastructure",
    "wiring owned by", "not yet wired", "will be integrated later". For each match:
    - If the deferred work references a specific future WU: verify that WU exists in
      IMPLEMENTATION-PLAN.md and is PENDING. Log in ORCHESTRATOR-STATE.md Notes:
      "WU-{N} deferred integration for {component} to WU-{M}"
    - If the deferred work does NOT reference a specific WU: create a remediation entry in
      ORCHESTRATOR-STATE.md Notes: "UNASSIGNED INTEGRATION: WU-{N} created {component} but
      integration is not assigned to any WU. Must be resolved before phase completion."
    - Mark the affected REQ as PARTIAL (not COMPLETED) in PROGRESS.md unless the integration
      is explicitly assigned to a later WU.
6. **Build verification:** Run `{BUILD_CMD}`. If it times out, try `{BUILD_CMD_INCR}`. If both fail and the agent didn't already mark BLOCKED, mark as BLOCKED.
7. **Update ORCHESTRATOR-STATE.md:** Record completed WU, increment Phase Metrics, set next WU.

### Validation Gate (after phase completes)

Before proceeding to QA:
1. Verify each WU in this phase has a status in PROGRESS.md (no PENDING WUs remain for this phase)
2. For COMPLETED WUs, verify the expected new files from SPEC-SUMMARY.md actually exist (spot-check — at least verify the primary files listed in "Files to Create")
3. Record any missing files in ORCHESTRATOR-STATE.md Notes

---

## Step 3.5: Cross-Spec Type Verification (after each phase)

After each phase completes, launch a lightweight type verification agent:

```
Read all files created during Phase {PHASE_NUM}. Use `git status` to find new untracked files
and `git diff --name-only` to find modified files under {SDK_ROOT}.

For each new public class/interface created:
1. Extract the fully-qualified name (package + class name for Java, module path for Go/Python, namespace for C#, export path for JS)
2. List its public methods and constructor signatures

Then read the Executive Summary and "Files to Modify" sections of Phase {NEXT_PHASE_NUM} spec files:
{list Phase N+1 spec files from IMPLEMENTATION-PLAN.md}

Check that every type referenced in Phase {NEXT_PHASE_NUM} specs matches the actual name
and location created by Phase {PHASE_NUM}. Flag any mismatches.

Write results to {SPECS_DIR}/type-verification-phase{PHASE_NUM}.md
```

**Gate:** If mismatches are found:
1. Launch a targeted fixer agent to update the next-phase spec file references to match actual source (the source files from the completed phase are authoritative — do not rename them)
2. If a spec file is updated, note the change in ORCHESTRATOR-STATE.md

Skip this step after Phase 3 (no next phase).

---

## Step 3.6: Integration Wiring Verification (after each phase)

After each phase completes (and after Step 3.5), verify that new components are actually used
in production code. This is a hard gate — do NOT proceed to QA with unwired components.

### Process

1. From PROGRESS.md, collect all REQs completed in this phase that have "Integration Verification
   Criteria" in their spec.

2. For each such REQ, read the spec's Integration Verification Criteria table. For each row:
   - Verify the "Must Be Imported By" file actually imports the new component
   - Use grep/rg to search for the component class name in production source files (exclude
     test directories)
   - Record result: WIRED (import found in production code) or UNWIRED (only in tests or
     _internal/ with no production import)

3. Write results to `{SPECS_DIR}/integration-verification-phase{PHASE_NUM}.md`:

```
# Integration Wiring Verification — Phase {PHASE_NUM}

| Component | Expected Import Location | Status          | Evidence                     |
| --------- | ------------------------ | --------------- | ---------------------------- |
| {name}    | {file from spec}         | WIRED / UNWIRED | {grep result or "not found"} |

## Unwired Components

{list any UNWIRED components with the WU that created them}

## Remediation

{for each UNWIRED component: is integration assigned to a future WU? If not, flag.}
```

4. **Gate decision:**
   - If ALL components are WIRED: proceed to QA
   - If UNWIRED components exist AND their integration is assigned to a future-phase WU:
     proceed with a warning logged in ORCHESTRATOR-STATE.md
   - If UNWIRED components exist with NO assigned integration WU: STOP. Create a remediation
     WU for the current phase. The orchestrator must either:
     (a) Launch a targeted agent to wire the component, or
     (b) Ask the user whether to proceed without wiring

---

## Step 4: QA Pass (Generic)

This step is a **parameterized template** used after each phase. The orchestrator executes it with `{PHASE_NUM}` = 1, 2, or 3.

### 4a: Prepare QA Context

Build QA variables by reading IMPLEMENTATION-PLAN.md and PROGRESS.md:
- `{PHASE_SPEC_FILES}`: List all spec files for WUs in this phase (from IMPLEMENTATION-PLAN.md's WU table)
- `{PHASE_REQ_LIST}`: For each spec, list REQ-IDs and their status from PROGRESS.md's WU Detail Log
- `{PHASE_RISK_AREAS}`: Read from `{LANG_CONSTRAINTS_DIR}/LANGUAGE-CONSTRAINTS-{SDK_NAME}.md`. Extract all constraint rules as known risk areas. If no constraints file exists, use only: "Cross-spec type consistency (names, signatures)" as the single universal risk area.

### 4b: Snapshot Pre-QA State

Before launching any QA agent, create a backup of all phase-modified files:
```bash
mkdir -p /tmp/kubemq-qa-backup-phase{PHASE_NUM}
# Copy only files modified in this phase
git diff --name-only -- {SDK_ROOT} > /tmp/kubemq-qa-backup-phase{PHASE_NUM}/file-list.txt
git status --porcelain -- {SDK_ROOT} | grep '??' | cut -c4- >> /tmp/kubemq-qa-backup-phase{PHASE_NUM}/file-list.txt
# Archive the files
tar czf /tmp/kubemq-qa-backup-phase{PHASE_NUM}/backup.tar.gz -T /tmp/kubemq-qa-backup-phase{PHASE_NUM}/file-list.txt
```

### 4c: Code Review (run FIRST)

Launch a `feature-dev:code-reviewer` agent using QA-REVIEW-PROMPT (see below), substituting all `{PHASE_*}` variables.

**Wait for code-review to complete. Run `{BUILD_CMD}` to verify review changes compile.** If build fails:
1. Restore from backup: `cd {SDK_ROOT} && tar xzf /tmp/kubemq-qa-backup-phase{PHASE_NUM}/backup.tar.gz`
2. Note in PROGRESS.md: "QA code-review changes reverted for Phase {PHASE_NUM}: {build error}"
3. Skip code-simplifier (proceed to 4f)

### 4d: Code Simplifier (run SECOND, only after code-review passes build)

Launch a `code-simplifier` agent using QA-SIMPLIFIER-PROMPT (see below).

**Wait for code-simplifier to complete. Run `{BUILD_CMD}` to verify.**

If build fails after code-simplifier only:
1. Restore from backup: `cd {SDK_ROOT} && tar xzf /tmp/kubemq-qa-backup-phase{PHASE_NUM}/backup.tar.gz`
2. Re-apply code-review changes by relaunching the code-reviewer (its changes passed build)
3. Note in PROGRESS.md: "QA code-simplifier changes reverted for Phase {PHASE_NUM}: {build error}"

### 4e: Final Build + Test Verification

Run `{BUILD_CMD}` and `{TEST_CMD}`. Verify results match or improve upon pre-QA state.

### 4f: Save QA Report

Write QA results to `{SPECS_DIR}/IMPL-REVIEW-PHASE-{PHASE_NUM}.md`:
```
# {SDK_LABEL} SDK — Implementation QA Review (Phase {PHASE_NUM})

**Date:** {date}
**Specs Reviewed:** {PHASE_SPEC_FILES}
**Code Review Issues:** {count found, count fixed}
**Simplification Changes:** {count}
**Build Status:** {PASS/FAIL}
**Test Status:** {N passed, M failed}

## Issues Found and Fixed
{list of issues from code-review agent output}

## Simplification Changes Applied
{list of changes from code-simplifier agent output}

## Issues Deferred
{any issues found but not fixed, with reasons}
```

Update PROGRESS.md:
- Update REQ entries with QA status: `| QA: PASS` or `| QA: ISSUES (see Phase {N} review)`
- Update Status Table with final post-QA status

Update ORCHESTRATOR-STATE.md: increment QA Issues count, set next phase.

---

## Step 5-8: Phase Execution and QA

The skill executes 3 phases, each consisting of an execution step and a QA pass:

| Step | Action | Template Used |
|------|--------|--------------|
| Step 3 (Phase 1) | Execute Phase 1 WUs | Step 3 Generic with PHASE_NUM=1 |
| Step 3.5 | Type verification Phase 1 → Phase 2 | Step 3.5 with PHASE_NUM=1 |
| Step 3.6 | Integration wiring verification Phase 1 | Step 3.6 with PHASE_NUM=1 |
| Step 4 (QA 1) | QA Pass for Phase 1 | Step 4 Generic with PHASE_NUM=1 |
| Step 5 (Phase 2) | Execute Phase 2 WUs | Step 3 Generic with PHASE_NUM=2 |
| Step 5.5 | Type verification Phase 2 → Phase 3 | Step 3.5 with PHASE_NUM=2 |
| Step 5.6 | Integration wiring verification Phase 2 | Step 3.6 with PHASE_NUM=2 |
| Step 6 (QA 2) | QA Pass for Phase 2 | Step 4 Generic with PHASE_NUM=2 |
| Step 7 (Phase 3) | Execute Phase 3 WUs | Step 3 Generic with PHASE_NUM=3 |
| Step 7.6 | Integration wiring verification Phase 3 | Step 3.6 with PHASE_NUM=3 |
| Step 8 (QA 3) | QA Pass for Phase 3 | Step 4 Generic with PHASE_NUM=3 |

**Phase-specific notes (verify from SPEC-SUMMARY.md, do not assume):**
- Concurrency spec often depends on connection AND feature specs that modify client classes
- Compatibility spec typically depends on packaging spec for version constants
- Test spec (later phases) depends on all feature WUs being complete
- Verb alignment typically depends on API completeness for new return types

---

## Step 9: Final Verification & Report

### 9a: Full Build + Test

Run `{BUILD_CMD} && {TEST_CMD}`. If build times out, use `{BUILD_CMD_INCR}`.

Capture final test count. Compare with `BASELINE_TESTS`.

### 9b: Coverage Verification

Read COVERAGE-CHECKLIST.md and PROGRESS.md. For every REQ-* in the checklist:
1. Verify it appears as implemented (checked off) in PROGRESS.md's WU Detail Log
2. Check its QA status — flag any REQs with "ISSUES" that were not resolved
3. Compare PROGRESS.md's Summary counts against COVERAGE-CHECKLIST.md's total REQ count
4. List any gaps (REQs that are still unchecked)

### 9c: File Count Verification

From SPEC-SUMMARY.md, get expected new files and modified files counts. Compare with actual:
- Use `git status` to count new untracked files under `{SDK_ROOT}`
- Use `git diff --name-only` to count modified files under `{SDK_ROOT}`

---

## Step 9.5: Generate Retrospective

Write `{SPECS_DIR}/IMPL-RETROSPECTIVE.md` by analyzing PROGRESS.md, ORCHESTRATOR-STATE.md, and all 3 IMPL-REVIEW-PHASE-*.md files.

```
# {SDK_LABEL} SDK — Implementation Retrospective

**Generated:** {date}
**Purpose:** Enable iterative improvement of the gap-implementation skill across SDK runs.

## 1. Issue Patterns

Analyze all 3 IMPL-REVIEW-PHASE-*.md files. Categorize and count issues:

| Issue Type | Phase 1 | Phase 2 | Phase 3 | Total | Example |
|------------|---------|---------|---------|-------|---------|
| {e.g., "Missing import"} | {n} | {n} | {n} | {n} | {brief example} |

### Root Cause Analysis
{For the top 3 most frequent issue types, explain why they occurred and how to prevent them}

## 1b. Integration Wiring Analysis

Read all `integration-verification-phase*.md` files. Summarize:

| Phase | Components Created | Components Wired | Components Unwired | Remediated |
|-------|-------------------|-----------------|-------------------|------------|
| 1 | {n} | {n} | {n} | {n} |
| 2 | {n} | {n} | {n} | {n} |
| 3 | {n} | {n} | {n} | {n} |

### Unwired Components at Final Assessment
{List any components that remained unwired after all phases completed, with root cause}

### Wiring Gap Patterns
{Were certain spec categories more prone to creating unwired infrastructure? Which ones?}

## 2. Rules to Add to IMPL-AGENT-PROMPT

Based on repeated issues, these rules would prevent the most common failures:

| # | Proposed Rule | Would Have Prevented | Issue Count |
|---|--------------|---------------------|-------------|
| 1 | {rule text} | {issue type} | {n} |

## 3. Language Constraint Updates

New {SDK_LABEL}-specific issues discovered during implementation that should be added
to LANGUAGE-CONSTRAINTS-{SDK_NAME}.md:

| # | Constraint | Category | Source |
|---|-----------|----------|--------|
| 1 | {e.g., "Maven Surefire 3.x requires explicit provider declaration"} | Build | Phase 2 QA |

## 4. Process Metrics

| Metric | Value |
|--------|-------|
| Total WUs launched | {n} |
| WUs completed | {n} |
| WUs blocked | {n} |
| WUs skipped | {n} |
| WUs skipped (all-compliant) | {n} |
| Build fix attempts (total across all WUs) | {n} |
| Build fix success rate | {n}% |
| QA issues found (total) | {n} |
| QA issues fixed | {n} |
| QA issues deferred | {n} |
| New files created | {n} |
| Files modified | {n} |
| REQs implemented | {n} / {total} |

## 5. Agent Performance

| WU | Spec | Status | Build Attempts | Test Failures | QA Issues | Notes |
|----|------|--------|---------------|---------------|-----------|-------|
| WU-1 | 01 | {status} | {n}/3 | {n} | {n} | {brief} |

### Which specs caused the most trouble?
{Analysis of which categories had the most build failures, QA issues, or blocks}

### Error cascade analysis
{If any WU was BLOCKED, trace the cascade: which dependent WUs were SKIPPED as a result?}

## 6. Cross-SDK Transfer Notes

Before running gap-implementation on a different SDK, review these findings:
- {lesson 1}
- {lesson 2}
- {lesson 3}
```

### Update Language Constraints

If Section 3 of the retrospective identified new language-specific constraints:
1. Read `{LANG_CONSTRAINTS_DIR}/LANGUAGE-CONSTRAINTS-{SDK_NAME}.md`
2. Append the new constraints with sequential rule numbers (e.g., J-15, J-16 for Java)
3. Note in IMPL-RETROSPECTIVE.md: "Updated LANGUAGE-CONSTRAINTS-{SDK_NAME}.md with {n} new rules"

If no language constraints file exists yet and new constraints were found, create one following the format of `LANGUAGE-CONSTRAINTS-java.md`.

---

## Step 10: Present Final Report

```
## Gap Implementation Complete

**SDK:** {SDK_LABEL}
**Specs Implemented:** {count} / 13
**Work Units:** {completed} completed, {blocked} blocked, {skipped} skipped

### Test Results
- Baseline: {BASELINE_TESTS} tests
- Final: {FINAL_TESTS} tests ({delta})
- Status: {all pass / N failures}

### Coverage
- Total REQs: {n}
- Implemented: {n}
- Blocked: {n} (see PROGRESS.md for details)
- Skipped: {n}
- QA Issues Found: {n} (see IMPL-REVIEW-PHASE-*.md)

### Files Changed
- New files created: {n} (expected: {n})
- Files modified: {n} (expected: {n})
- Files deleted: {n}

### QA Summary
- Phase 1 review: {summary from IMPL-REVIEW-PHASE-1.md}
- Phase 2 review: {summary from IMPL-REVIEW-PHASE-2.md}
- Phase 3 review: {summary from IMPL-REVIEW-PHASE-3.md}

### Breaking Changes Applied
{list breaking changes from IMPLEMENTATION-PLAN.md that were implemented, with their WU}

### Blocked Items
{list any BLOCKED or SKIPPED work units with reasons}

### Retrospective
See {SPECS_DIR}/IMPL-RETROSPECTIVE.md for detailed analysis and lessons learned.
{n} new language constraints added to LANGUAGE-CONSTRAINTS-{SDK_NAME}.md.

### Next Steps
1. Review all uncommitted changes with `git diff`
2. Address any BLOCKED items manually
3. Run full test suite: `{TEST_CMD}`
4. Review QA reports: `{SPECS_DIR}/IMPL-REVIEW-PHASE-*.md`
5. Review retrospective: `{SPECS_DIR}/IMPL-RETROSPECTIVE.md`
6. Commit when satisfied
```

Update PROGRESS.md with final status for all work units.

---

## IMPL-AGENT-PROMPT

<prompt-template id="impl-agent">
You are a senior {SDK_LABEL} developer implementing a gap-close specification for the KubeMQ {SDK_LABEL} SDK.

## Your Task

Implement specification {SPEC_FILE} (Category {CATEGORY_NUM}: {CATEGORY_NAME}) in the actual SDK codebase. Create new files, modify existing files, and write tests as specified.

{SCOPE_FILTER_SECTION}

{OPEN_QUESTIONS_SECTION}

## Input Files — READ STRATEGICALLY

1. **Implementation Spec:** Read `{SPECS_DIR}/{SPEC_FILE}`. If the spec is longer than 500 lines, read it in sections:
   - FIRST: Read the Executive Summary, Prerequisites, and Implementation Order sections
   - THEN: Implement one REQ-ID at a time — read its section, implement it, verify it compiles, then proceed to the next
   - Do NOT attempt to read the entire spec into memory before starting implementation
2. **Language Constraints:** Read `{LANG_CONSTRAINTS_DIR}/LANGUAGE-CONSTRAINTS-{SDK_NAME}.md` — language-specific rules you MUST follow. If this file does not exist, proceed without it but be extra careful with language-specific idioms.
3. **PROGRESS.md Status Table:** Read ONLY the "## Status Table" section of `{SPECS_DIR}/PROGRESS.md` — to understand what has already been implemented by prior work units. Do NOT read the full WU Detail Log unless you need specifics about a dependency WU.
4. **Prior WU outputs:** Check `git status` and `git diff --name-only` to see what files have been created/modified by prior work units. For any type your spec depends on (exception classes, interfaces, configuration classes), **read the actual source file** and confirm the exact package name, class name, and constructor signature before referencing it. Do not rely on the spec's cross-references alone.

## Implementation Process

### Phase A: Understand Current State

1. Read the spec's Executive Summary and Implementation Order sections first.
2. For each REQ-* in your scope, check its status in COVERAGE-CHECKLIST.md:
   - **MISSING:** Create from scratch as specified
   - **PARTIAL:** Read the spec's "Current State" subsection to understand what already exists. Only implement what is listed as missing or incorrect. Do not overwrite existing working code.
   - **NOT_ASSESSED:** Verify current state by reading existing source files before implementing. If the feature already exists, treat as PARTIAL.
   - **COMPLIANT:** Skip implementation — note "COMPLIANT — no changes needed" in your output file
3. For each "Files to Create" entry: verify the target directory exists. Create directories if needed.
4. For each "Files to Modify" entry: read the current file content. Understand the existing code before changing it.
5. Check what prior work units have already created — especially shared types like exceptions, interfaces, and configuration classes.

### Phase B: Implement

Follow the spec's "Implementation Order" section. For each spec item (one REQ at a time):

1. **Read the REQ section** from the spec.
2. **Create new files** as specified. Use the exact interface/class definitions from the spec. Include all imports, annotations, and complete method bodies. Include inline documentation shown in the spec's class definitions, using the {SDK_LABEL}-appropriate documentation format.
3. **Modify existing files** as specified. Make minimal, targeted changes. Do not refactor unrelated code.
4. **Run `{BUILD_CMD}`** after each REQ to catch errors early. If the build times out, use `{BUILD_CMD_INCR}` instead. Fix errors immediately before moving to next REQ.
5. **Write tests** for each spec item. Follow the test scenarios table in the spec. Place tests in the standard test directory structure.

### Phase C: Verify

After implementing all items:

1. Run `{BUILD_CMD}`. If compilation fails, follow this debugging protocol:
   - Read the exact compiler error message
   - Identify the specific file and line number
   - Read the current content of the failing file
   - If the error is a missing import: verify the imported class exists in the language's standard library, project dependencies, or a file you/prior WUs created
   - If the error is in a file created by a prior WU: read that file to verify the exact API
   - Fix only the identified error — do not rewrite the entire file
   - Re-run build
   - Repeat up to 3 times total (3 build-fix cycles within this single agent run)
   - If the build times out, use `{BUILD_CMD_INCR}` (incremental build without clean)
   - If still failing after 3 attempts, document the exact error and mark as BLOCKED

2. Run `{TEST_CMD}`. If tests fail:
   - Distinguish between NEW test failures (your code) and PRE-EXISTING failures
   - Fix new test failures (up to 3 attempts, same protocol as build failures)
   - If a new test cannot be fixed, disable it using `{TEST_DISABLE}` with a descriptive reason and mark as PARTIAL
   - Never fix or modify pre-existing test failures

3. **Integration wiring verification.** For each REQ in your scope, check if the spec has an
   "Integration Verification Criteria" table. If it does, verify EACH row:
   - The new component IS imported by the listed production file (not just test files)
   - The component IS instantiated or called in the listed method
   - Run a targeted search (e.g., grep/rg for the class name in the production source directory,
     excluding test directories) to confirm
   - If the component is NOT imported by any production code:
     a. If the integration target is in YOUR scope: implement the wiring NOW
     b. If the integration target belongs to a different WU/spec: mark the REQ as **PARTIAL**
        with note: "Component created but wiring owned by WU-{N} / spec {XX}. Integration
        pending."
   - **CRITICAL:** Do NOT mark a REQ as COMPLETED if its Integration Verification Criteria
     has unmet rows. "Deferred to later" is not an acceptable completion status for rows
     that are in your scope.

### Phase D: Write Output File

Write your results to `{SPECS_DIR}/wu-{WU_NUM}-output.md` (the orchestrator will merge this into PROGRESS.md):

```
# WU-{WU_NUM} Output: {SPEC_FILE}

**Status:** {COMPLETED | PARTIAL | BLOCKED}
**Build:** {PASS | FAIL: error summary}
**Build Fix Attempts:** {0-3}
**Tests:** {N passed, M failed, K skipped}

## REQ Status
- [x] REQ-*: {title} — implemented
- [~] REQ-*: {title} — partial: {what was done, what remains}
- [ ] REQ-*: {title} — blocked: {reason}

## Files Created
- {path/to/file.ext}
- ...

## Files Modified
- {path/to/file.ext}: {brief description of changes}
- ...

## Breaking Changes Applied
- {description} (migration guide needed)

## Notes
- {decisions made}
- {deviations from spec, with rationale}
- {issues encountered}
- {language-specific issues discovered (for retrospective)}
```

## CRITICAL RULES

1. **The spec is authoritative.** Implement what the spec says. If you disagree with a design decision, implement it anyway and add a note in your output file.

2. **Preserve existing code.** When modifying files, keep all existing functionality intact. Add to or wrap existing code — don't replace working code unless the spec explicitly says to.

3. **Compilation is mandatory.** Your changes MUST compile. If you cannot make them compile, mark as BLOCKED rather than leaving broken code.

4. **No scope creep.** Only implement the REQ-* items assigned to this work unit. Do not implement items from other specs or phases, even if they seem related.

5. **Use exact type names from the spec.** Class names, package paths, method signatures — use exactly what the spec defines. These have been through a type registry and review process.

6. **Follow language constraints.** Read and follow ALL rules in the language constraints file. These prevent common implementation errors discovered in prior runs.

7. **Test isolation.** New tests must not depend on external services (KubeMQ server) unless they are explicitly integration tests. Unit tests must be fully self-contained with mocks/stubs.

8. **No automatic commits.** Do NOT run any `git commit` commands. All changes stay uncommitted.

9. **Spec-faithful implementation.** Implement exactly what the spec defines, including inline documentation shown in the spec's class definitions. Use the {SDK_LABEL}-appropriate documentation format. Do not add extras beyond the spec, but do not omit spec content either.

10. **Check before creating.** Before creating a new file, check if it already exists (from a prior work unit). If it does, modify it rather than overwriting.

11. **Import verification.** Before writing any import statement, verify the imported type exists — either in the language's standard library, in project dependencies (check the build config file), or in a file you've already created or that was created by a prior WU.

12. **Thread safety.** When implementing concurrent code, use the exact synchronization mechanism specified in the spec's "Thread Safety" section. Do not substitute a different mechanism.

13. **Read before reference.** Before using any type defined by a prior work unit (exception classes, interfaces, configuration classes), read the actual source file and confirm the exact name and signature. Do not rely on memory from reading the spec.

14. **Canonical artifact ownership.** Before creating any file that could be owned by multiple specs (CI config, changelog, build config sections), check if a prior WU has already created it (via `git status`). If it exists, modify it rather than replace it.

15. **Self-contained compilation.** Every source file you create must include all required imports. Before writing an import, verify the type is available in: the standard library, a declared dependency, or a file already created in this or a prior work unit.

16. **Verify all method references.** Every method call in your implementation must reference a method that either exists in the current codebase or that you are creating in this work unit. Do not reference methods from future work units.

17. **Breaking change annotation.** When your spec includes a breaking change, record it in your output file's "Breaking Changes Applied" section.

18. **Integration wiring is mandatory.** If the spec's "Integration Verification Criteria" table
    lists a production file that should import your new component, you MUST verify that import
    exists before marking the REQ as COMPLETED. A component that exists only in _internal/ with
    test coverage but zero production imports is PARTIAL, not COMPLETED.

{SCOPE_FILTER_RULES}
</prompt-template>

### Scope Filter Section (for split specs)

When a work unit covers only part of a spec, insert this section into the IMPL-AGENT-PROMPT at `{SCOPE_FILTER_SECTION}`:

```
## Scope Filter

This work unit covers ONLY these REQ-IDs from the spec:
{list of REQ-IDs from IMPLEMENTATION-PLAN.md}

You MUST read the full spec for context (to understand types and dependencies), but ONLY implement the REQ-IDs listed above. All other REQ-IDs in this spec belong to a different work unit and must NOT be implemented now.
```

And insert this at `{SCOPE_FILTER_RULES}`:

```
18. **Scope filter is strict.** You are only implementing the REQ-IDs listed in the Scope Filter section. If a REQ-ID is not listed, do not implement it even if it appears in the spec. Other work units will handle those items.
```

For work units that cover the entire spec:
- Set `{SCOPE_FILTER_SECTION}` to: "This work unit covers the entire spec. Implement all REQ-IDs listed in the spec."
- Set `{SCOPE_FILTER_RULES}` to empty string.

### Open Questions Section

If the user resolved open questions in Step 2, insert at `{OPEN_QUESTIONS_SECTION}`:

```
## Resolved Open Questions

The user has resolved these questions that affect this spec:
{list of relevant Q&A pairs from IMPLEMENTATION-PLAN.md}

Follow these decisions in your implementation.
```

If no open questions affect this WU, set `{OPEN_QUESTIONS_SECTION}` to empty string.

---

## QA-REVIEW-PROMPT

<prompt-template id="qa-review">
Review all files created or modified during Phase {PHASE_NUM} of the gap implementation for the KubeMQ {SDK_LABEL} SDK.

## Context

These specs were implemented in Phase {PHASE_NUM}:
{PHASE_REQ_LIST}

Read the implementation agents' notes from per-WU output files ({SPECS_DIR}/wu-*-output.md for Phase {PHASE_NUM} WUs) for any deviations or issues encountered.

## Spec Files (for reference)
{PHASE_SPEC_FILES}

## Language Constraints
Read {LANG_CONSTRAINTS_DIR}/LANGUAGE-CONSTRAINTS-{SDK_NAME}.md for language-specific rules and known risk areas.

## Known Risk Areas
{PHASE_RISK_AREAS}
Additionally, always check: cross-spec type consistency (names, packages, signatures across files created by different WUs).

## Focus Areas
1. Bugs, logic errors, and security vulnerabilities
2. Adherence to the spec (verify implementations match spec definitions)
3. Thread safety issues (verify synchronization mechanisms match spec)
4. Proper exception/error handling and resource cleanup
5. API consistency across the new types
6. Import correctness (no undefined imports, no standard library name collisions)
7. Spec integration compliance — for each REQ implemented in this phase, verify that new
   components created by the spec are actually imported and called by production code (not just
   tests). If a spec's "Integration Verification Criteria" table exists, verify each row.
   Flag CRITICAL if a component has zero production imports.

## Process
Use `git diff` and `git status` to identify all changed/new files under {SDK_ROOT}.
Review each file thoroughly. For each issue found, fix it directly.

## Constraints
- Do NOT rename types, change method signatures, or alter the public API — these come from a reviewed spec
- Do NOT remove synchronization, locking, or defensive checks even if they appear unnecessary
- Do NOT remove inline documentation that appears in the spec definitions
- If `feature-dev:code-reviewer` is not available as an agent type, use `general-purpose` with this same prompt
</prompt-template>

---

## QA-SIMPLIFIER-PROMPT

<prompt-template id="qa-simplifier">
Simplify and refine code recently modified during Phase {PHASE_NUM} of the KubeMQ {SDK_LABEL} SDK
gap implementation. Focus on clarity, consistency, and maintainability while preserving
ALL functionality.

## Files to Review
Use `git diff --name-only` and `git status` to find all changed/new files under {SDK_ROOT}/src/
(or the language-appropriate source directory).

## Constraints — DO NOT CHANGE:
- Type names, class names, method signatures, and package/module paths (these are from a reviewed spec)
- Synchronization mechanisms (locks, atomics, volatile, concurrent collections, mutexes, channels)
- Defensive null/nil/None checks and input validation
- Inline documentation blocks (Javadoc, GoDoc, docstrings, XML doc comments, JSDoc)
- Import statements (these have been verified)
- Test files

## Focus Areas:
- Reduce code duplication within new files
- Simplify overly complex control flow
- Improve variable naming for clarity (only private/local variables, not public API)
- Remove dead code or unreachable branches
- Ensure consistent formatting and style within new code

Read {LANG_CONSTRAINTS_DIR}/LANGUAGE-CONSTRAINTS-{SDK_NAME}.md for language-specific rules.

If `code-simplifier` is not available as an agent type, use `general-purpose` with this same prompt.
</prompt-template>

---

## Error Recovery

### Build Failure

The 3-attempt limit is within a single agent run. The agent runs `{BUILD_CMD}`, reads the error, fixes the code, and re-runs — up to 3 build-fix cycles. If build times out, the agent uses `{BUILD_CMD_INCR}`. If the agent reports BLOCKED in its output file, the orchestrator does NOT relaunch it.

1. Agent reports BLOCKED: orchestrator records exact error in PROGRESS.md
2. Continue to next WU if it doesn't depend on the blocked one
3. If dependent: mark dependent WU as `SKIPPED (dependency WU-{N} blocked)`

### Test Failure

Same 3-attempt limit within the agent run.

1. Agent disables failing tests using `{TEST_DISABLE}` with explanation
2. Agent reports PARTIAL in its output file
3. Continue to next WU — partial implementation is usable

### Dependency Cascade

If a WU is BLOCKED and other WUs depend on it:
1. Check if the dependent WU can partially proceed (some REQs may not need the blocked dependency)
2. If fully blocked: mark dependent WU as `SKIPPED (dependency WU-{N} blocked)`
3. Attempt any independent WUs that don't depend on the blocked one
4. Record the full cascade in PROGRESS.md and ORCHESTRATOR-STATE.md

### QA Break Recovery

QA agents always run sequentially (code-review FIRST, then code-simplifier — never simultaneously).

1. Run `{BUILD_CMD}` after each QA agent completes
2. If build fails: restore from backup (`tar xzf /tmp/kubemq-qa-backup-phase{N}/backup.tar.gz`)
3. If code-review broke build: skip code-simplifier, note in PROGRESS.md
4. If code-simplifier broke build: restore backup, re-apply code-review only, note in PROGRESS.md

---

## Parallel Agent Launch Protocol

Before launching agents in parallel:

1. **File conflict check:** Read IMPLEMENTATION-PLAN.md's File Conflict Analysis section. Only launch WUs where the Resolution is `PARALLEL` or where the WU is not part of a serialized pair.

2. **Automatic serialization triggers:** Even if IMPLEMENTATION-PLAN.md says PARALLEL, serialize if ANY of these files are modified by multiple WUs in the group:
   - The SDK's `{BUILD_CONFIG_FILE}` (from SDK config table)
   - `README.md`
   - Main client class files (from SPEC-SUMMARY.md's most-modified file)

3. **Launch:** Use the Agent tool with multiple parallel calls in a single message. Each agent runs independently.

4. **Post-parallel verification:** After all parallel agents complete:
   - Read each agent's output file (`wu-{N}-output.md`) and check for overlapping file creation
   - Run `{BUILD_CMD}` to verify no conflicts
   - If build fails: merge conflicting files in WU-number order (lower first), re-run build

---

## Agent Launch Configuration

When launching implementation agents:

```
subagent_type: "general-purpose"
description: "Implement WU-{N}: Spec {SPEC_NUM}"
prompt: {IMPL-AGENT-PROMPT with all variables substituted}
```

When launching QA agents (always sequentially — code-review FIRST, then code-simplifier):

```
# Code Review (run FIRST)
subagent_type: "feature-dev:code-reviewer"
description: "QA review Phase {N} changes"
prompt: {QA-REVIEW-PROMPT with all variables substituted}

# Code Simplifier (run SECOND, only after code-review passes build)
subagent_type: "code-simplifier"
description: "Simplify Phase {N} code"
prompt: {QA-SIMPLIFIER-PROMPT with all variables substituted}
```

**Fallback:** If `feature-dev:code-reviewer` or `code-simplifier` agent types are not available in the execution environment, use `general-purpose` with the same prompt text.
