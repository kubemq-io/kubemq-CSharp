---
name: gap-close-spec
description: Convert gap research into detailed implementation specs per GS category. Creates a coverage checklist, generates specs using parallel agents, then runs two review rounds (architect + implementer). Saves all review reports for retrospective analysis.
argument-hint: SDK name (go, java, csharp, python, js)
user-invocable: true
allowed-tools: Read, Grep, Glob, Write, Edit, Bash, Agent, WebSearch, WebFetch, AskUserQuestion
---

# Gap Close Specification Generator

Generate implementation specs from gap research: $ARGUMENTS

## Overview

Converts a completed gap research report into detailed, implementable specifications — one per GS category (01-13). Each spec contains interface definitions, method signatures, data structures, error flows, config options, test scenarios, and concrete file paths to create/modify. A developer can implement directly from these specs.

**KEY PRINCIPLE:** Every gap research finding must trace to a specific, actionable spec item. Nothing is lost in translation.

---

## Step 1: Validate Input

**$ARGUMENTS is REQUIRED.** Must be one of:
- `go` — Go SDK
- `java` — Java SDK
- `csharp` — C# / .NET SDK
- `python` — Python SDK
- `js` — JS/TS SDK

**If empty:** STOP, ask: "Which SDK should I generate specs for? Specify one: go, java, csharp, python, js"

---

## Step 2: Load Reference Documents

Before launching agents, read these files to understand the full context.

**IMPORTANT:** Also load the language-specific constraints file if it exists: `clients/skills/gap-close-spec/LANGUAGE-CONSTRAINTS-{SDK_NAME}.md`. This file contains language-specific pitfalls and rules that spec agents must follow. If it doesn't exist for the target SDK, skip this step.

### Gap Research (primary input)
- `clients/gap-research/{SDK_NAME}-gap-research.md` — The combined gap research report (all 13 categories)
- `clients/gap-research/{SDK_NAME}-tier1-status.md` — Compact Tier 1 status
- `clients/gap-research/{SDK_NAME}-tier2-status.md` — Compact Tier 2 status

### Golden Standard (requirements source)
- `clients/sdk-golden-standard.md` — Tier definitions, parity matrix
- All 13 category specs: `clients/golden-standard/01-error-handling.md` through `clients/golden-standard/13-performance.md`

### Assessment (current state)
Use the assessment file mapping:

| SDK | Assessment File |
|-----|----------------|
| go | `clients/assesments/GO_ASSESSMENT_REPORT.md` |
| java | `clients/assesments/JAVA_ASSESSMENT_REPORT.md` |
| csharp | `clients/assesments/C#-ASSESSMENT-REPORT.md` |
| python | `clients/assesments/PYTHON_SDK_ASSESSMENT_REPORT.md` |
| js | `clients/assesments/JS-ASSESSMENT-REPORT.md` |

### SDK Source Code (for concrete file references)
Identify the SDK source directory. Use the mapping:

| SDK | Source Root |
|-----|-----------|
| java | The current repository root (look for `src/main/java/`) |
| go | Look for `clients/kubemq-go/` or similar |
| csharp | Look for `clients/kubemq-csharp/` or similar |
| python | Look for `clients/kubemq-python/` or similar |
| js | Look for `clients/kubemq-js/` or similar |

If the source root is not found, ask the user for the correct path.

---

## Step 2.5: Generate Type Registry

Before writing any specs, generate a **Type Registry** that pre-defines all new public types across all categories. This prevents the #1 source of review issues: cross-spec type inconsistency (naming conflicts, duplicate definitions, package mismatches).

### Process

1. Read the gap research report and identify ALL new types that will be introduced:
   - Exception/error types (from categories 01, 02, 03)
   - Configuration types (from categories 02, 03, 05)
   - Interface/abstract types (from categories 07, 09, 10)
   - Utility types (from categories 04, 05, 11, 13)

2. For each type, define its canonical identity:
   - **Class name** (verified against JDK for name collisions — see language constraints)
   - **Package** (full qualified package path)
   - **Extends/Implements** (parent class or interface)
   - **Key constructor parameters** (types and names)
   - **Owning spec** (which spec is responsible for the canonical definition)

3. Write to `clients/gap-close-specs/{SDK_NAME}/TYPE-REGISTRY.md`:

```
# {SDK_LABEL} SDK — Type Registry

**Generated:** {date}
**Purpose:** Canonical type definitions shared across all spec agents to prevent naming conflicts and duplicate definitions.

## How to Use This Registry

- **Spec agents:** Before defining a new public type, check this registry. If the type already exists, use the EXACT name, package, and constructor signature listed here.
- **If you need a type not in the registry:** Add a comment in your spec: `// NEW TYPE — not in registry, needs addition` and define it fully in your spec.
- **Cross-spec references:** When referencing a type owned by another spec, use: `// Defined in {XX}-spec.md, see TYPE-REGISTRY.md`

## Artifact Ownership

These artifacts are owned by exactly one spec. Other specs MUST NOT redefine them:

| Artifact | Owning Spec | Other Specs Reference As |
|----------|-------------|------------------------|
| CI config (`ci.yml`) | 04-testing-spec.md | "See spec 04 for canonical CI definition" |
| CHANGELOG format | 11-packaging-spec.md | "See spec 11 for canonical CHANGELOG" |
| SDK_VERSION constant | 11-packaging-spec.md | "See spec 11 for version management" |
| CONTRIBUTING.md | 06-documentation-spec.md | "See spec 06 for contribution guide" |
| {add others as discovered} | | |

## Exception Types

| # | Class Name | Package | Extends | Constructor Args | Owning Spec |
|---|-----------|---------|---------|-----------------|-------------|
| 1 | {name} | {package} | {parent} | {args} | {spec} |

## Configuration Types

| # | Class Name | Package | Purpose | Owning Spec |
|---|-----------|---------|---------|-------------|
| 1 | {name} | {package} | {purpose} | {spec} |

## Interface Types

| # | Interface Name | Package | Key Methods | Owning Spec |
|---|---------------|---------|-------------|-------------|
| 1 | {name} | {package} | {methods} | {spec} |

## Utility / Other Types

| # | Class Name | Package | Purpose | Owning Spec |
|---|-----------|---------|---------|-------------|
| 1 | {name} | {package} | {purpose} | {spec} |

## JDK Name Collision Check

These proposed names were checked against `java.lang.*`, `java.util.*`, `java.util.concurrent.*`, `java.io.*` (or equivalent for target language):

| Proposed Name | Collision? | Resolution |
|--------------|-----------|------------|
| {name} | {Yes — java.util.X / No} | {Renamed to KubeMQX / No action} |
```

**VERIFICATION GATE:** The type registry must be complete before proceeding. Every exception type, configuration type, and public interface mentioned in the gap research must appear in the registry.

---

## Step 3: Generate Coverage Checklist

Before writing any specs, create a coverage checklist that maps every gap research finding to a spec category. This ensures nothing is missed.

Read the gap research report and extract ALL items that need specification:
- Every REQ-* with status PARTIAL, MISSING, NOT_ASSESSED, or EXCESS
- Every remediation work item
- Every dependency relationship
- Every item in the Critical Path, Quick Wins, and Features to Remove sections
- COMPLIANT items (brief confirmation entries)

Write to `clients/gap-close-specs/{SDK_NAME}/COVERAGE-CHECKLIST.md` using this format:

```
# {SDK_LABEL} SDK — Gap Close Spec Coverage Checklist

**Generated:** {date}
**Source:** clients/gap-research/{SDK_NAME}-gap-research.md

## Coverage Matrix

| # | REQ-* | Gap Status | Priority | Spec Depth | Spec File | Spec Section | Covered |
|---|-------|-----------|----------|-----------|-----------|-------------|---------|
| 1 | REQ-ERR-1 | MISSING | P0 | Full | 01-error-handling-spec.md | §1.1 | [ ] |
| 2 | REQ-ERR-2 | MISSING | P0 | Full | 01-error-handling-spec.md | §1.2 | [ ] |
| ... | | | | | | | |
| N | REQ-PERF-6 | COMPLIANT | — | Confirmation | 13-performance-spec.md | §13.6 | [ ] |

**Spec Depth key:** Full = complete spec section with interface definitions, implementation steps, tests. Confirmation = brief entry confirming compliance (5-10 lines).

## Cross-Category Dependencies

| # | From Spec | To Spec | Dependency | Covered |
|---|-----------|---------|------------|---------|
| 1 | 01-error-handling | 07-code-quality | REQ-ERR-1 depends on REQ-CQ-1 | [ ] |
| ... | | | | |

## Remediation Items Not Tied to a Single REQ

| # | Item | Source | Spec File | Covered |
|---|------|--------|-----------|---------|
| 1 | Remove grpc-alts dependency | Features to Remove | 07-code-quality-spec.md | [ ] |
| ... | | | | |

**Total items:** {n}
**Covered:** 0 / {n}

## REQ Count Verification

| Category | REQ Count in Checklist | Expected (from GS) | Match? |
|----------|----------------------|--------------------|---------|
| 01 Error Handling | {n} | 9 (ERR-1 to ERR-9) | {Yes/No} |
| 02 Connection | {n} | 6 (CONN-1 to CONN-6) | {Yes/No} |
| 03 Auth & Security | {n} | 6 (AUTH-1 to AUTH-6) | {Yes/No} |
| 04 Testing | {n} | 5 (TEST-1 to TEST-5) | {Yes/No} |
| 05 Observability | {n} | 5 (OBS-1 to OBS-5) | {Yes/No} |
| 06 Documentation | {n} | 7 (DOC-1 to DOC-7) | {Yes/No} |
| 07 Code Quality | {n} | 7 (CQ-1 to CQ-7) | {Yes/No} |
| 08 API Completeness | {n} | 3 (API-1 to API-3) | {Yes/No} |
| 09 API Design & DX | {n} | 5 (DX-1 to DX-5) | {Yes/No} |
| 10 Concurrency | {n} | 5 (CONC-1 to CONC-5) | {Yes/No} |
| 11 Packaging | {n} | 4 (PKG-1 to PKG-4) | {Yes/No} |
| 12 Compatibility | {n} | 5 (COMPAT-1 to COMPAT-5) | {Yes/No} |
| 13 Performance | {n} | 6 (PERF-1 to PERF-6) | {Yes/No} |
| **Total** | {n} | **73** | {Yes/No} |
```

**VERIFICATION GATE:** After generating the checklist, verify that the REQ count per category matches the expected count from the GS. If any category has a mismatch, re-read the gap research and GS spec for that category to find the missing items. Do NOT proceed to Step 4 until all counts match.

---

## Step 4: Determine Spec Split

All 13 GS categories get a spec file, regardless of gap status:

| # | Category | Spec File | Content Level |
|---|----------|-----------|---------------|
| 01 | Error Handling | `01-error-handling-spec.md` | Full spec (has gaps) |
| 02 | Connection & Transport | `02-connection-transport-spec.md` | Full spec (has gaps) |
| 03 | Auth & Security | `03-auth-security-spec.md` | Full spec (has gaps) |
| 04 | Testing | `04-testing-spec.md` | Full spec (has gaps) |
| 05 | Observability | `05-observability-spec.md` | Full spec (has gaps) |
| 06 | Documentation | `06-documentation-spec.md` | Full spec (has gaps) |
| 07 | Code Quality | `07-code-quality-spec.md` | Full spec (has gaps) |
| 08 | API Completeness | `08-api-completeness-spec.md` | Confirmation + minor items |
| 09 | API Design & DX | `09-api-design-dx-spec.md` | Full spec (has gaps) |
| 10 | Concurrency | `10-concurrency-spec.md` | Full spec (has gaps) |
| 11 | Packaging | `11-packaging-spec.md` | Full spec (has gaps) |
| 12 | Compatibility | `12-compatibility-spec.md` | Full spec (has gaps) |
| 13 | Performance | `13-performance-spec.md` | Full spec (has gaps) |

For categories where all REQ-* items are COMPLIANT, the spec is a short confirmation document (~20 lines) stating current compliance and any polish items.

---

## Step 5: Launch Spec Generation Agents

To manage context, split into **three batches** by related categories:

- **Batch 1 (Foundation):** Categories 01, 02, 03, 07 — Error Handling, Connection, Auth, Code Quality. These are interdependent foundation layers.
- **Batch 2 (Features):** Categories 04, 05, 06, 08, 09 — Testing, Observability, Documentation, API Completeness, API Design. These build on the foundation.
- **Batch 3 (Operations):** Categories 10, 11, 12, 13 — Concurrency, Packaging, Compatibility, Performance. These are operational concerns.

### Batch Execution

Launch all agents within a batch in parallel. Wait for batch completion before launching the next batch. This ensures foundation specs exist before feature specs that reference them.

**For each category**, launch one agent using SPEC-AGENT-PROMPT (see below), substituting:
- `{SDK_LABEL}` — Display name (e.g., "Java")
- `{SDK_NAME}` — Lowercase key (e.g., "java")
- `{CATEGORY_NUM}` — Two-digit GS category number (e.g., "01")
- `{CATEGORY_NAME}` — Category name (e.g., "Error Handling & Resilience")
- `{GS_SPEC_FILE}` — Path to the GS category spec (e.g., `clients/golden-standard/01-error-handling.md`)
- `{GAP_RESEARCH_FILE}` — `clients/gap-research/{SDK_NAME}-gap-research.md`
- `{ASSESSMENT_FILE}` — Assessment file from mapping table
- `{OUTPUT_FILE}` — `clients/gap-close-specs/{SDK_NAME}/{CATEGORY_NUM}-{slug}-spec.md`
- `{BATCH_NUM}` — 1, 2, or 3
- `{PRIOR_SPECS}` — For Batch 2+, list the spec files from prior batches that this agent should read for cross-references
- `{LANGUAGE_CONSTRAINTS_FILE}` — `clients/skills/gap-close-spec/LANGUAGE-CONSTRAINTS-{SDK_NAME}.md` (if exists)
- `{TYPE_REGISTRY_FILE}` — `clients/gap-close-specs/{SDK_NAME}/TYPE-REGISTRY.md`

### Variable Construction for Reviewer/Fixer Prompts

When launching reviewer and fixer agents, construct these variables from the batch tables:
- `{SPEC_FILES_LIST}` — All spec output files in the batch (e.g., for Batch 1: `01-error-handling-spec.md, 02-connection-transport-spec.md, 03-auth-security-spec.md, 07-code-quality-spec.md`)
- `{GS_SPEC_FILES_LIST}` — All GS category specs corresponding to the batch (e.g., for Batch 1: `clients/golden-standard/01-error-handling.md, clients/golden-standard/02-connection-transport.md, clients/golden-standard/03-auth-security.md, clients/golden-standard/07-code-quality.md`)
- `{REVIEW_OUTPUT_FILE}` — `clients/gap-close-specs/{SDK_NAME}/review-r{ROUND}-batch{BATCH_NUM}.md`
- `{REVIEW_FILE}` (for fixer) — Same as `{REVIEW_OUTPUT_FILE}` from the corresponding review step
- `{R1_REVIEW_FILES}` (for R2 reviewers) — All 3 Round 1 review files: `review-r1-batch1.md, review-r1-batch2.md, review-r1-batch3.md`

### Cross-Batch Dependencies

Batch 2 and 3 agents MUST read relevant specs from prior batches:
- Batch 2 agents read: `01-error-handling-spec.md`, `02-connection-transport-spec.md`, `03-auth-security-spec.md`, `07-code-quality-spec.md` (for error types, connection state machine, auth configuration, architecture layers)
- Batch 3 agents read: `01-error-handling-spec.md`, `02-connection-transport-spec.md` (for async API, shutdown, and performance baseline references)

**Validation gate:** After each batch completes, verify each output file exists and meets minimum line counts based on gap status:
- FULLY_COMPLIANT categories: at least 20 lines (confirmation spec)
- Categories with 1-3 gap items: at least 100 lines
- Categories with 4+ gap items: at least 200 lines
If any agent produced output below the threshold, re-launch that specific agent. If an agent fails completely (error, timeout, empty output), re-launch it independently — do not block the entire batch. Log any failures in RETROSPECTIVE-NOTES for process improvement.

---

## Step 5.5: Cross-Spec Consistency Check

After each batch completes (and before launching reviews), run a lightweight consistency check agent. This agent reads ALL specs produced so far and flags:

1. **Duplicate type definitions** — Same class/interface defined in multiple specs with different signatures
2. **Package name mismatches** — Same logical type placed in different packages across specs
3. **Conflicting artifact definitions** — Multiple specs defining the same file (CI config, CHANGELOG, etc.)
4. **Broken cross-references** — Spec A references a type "defined in spec B" but spec B doesn't define it
5. **Type registry violations** — Types defined in specs that don't match the TYPE-REGISTRY.md entries

### Process

Launch **one consistency checker agent** after each batch completes:

**Agent prompt:** "Read all spec files in `clients/gap-close-specs/{SDK_NAME}/` and the TYPE-REGISTRY.md. For each spec, extract all public type definitions (classes, interfaces, exceptions) with their full package paths. Compare across specs and flag: (1) duplicate names with different packages, (2) duplicate names with different signatures, (3) types referenced but not defined in any spec, (4) types that don't match the TYPE-REGISTRY.md. Write a brief report to `clients/gap-close-specs/{SDK_NAME}/consistency-check-batch{N}.md`. If conflicts are found, edit the conflicting specs to align with the TYPE-REGISTRY.md (registry is authoritative)."

**Gate:** All consistency issues must be resolved before proceeding to review. The consistency checker agent should fix issues directly, not just report them.

---

## Step 5.7: Global Cross-Spec Consistency Check

After ALL three batches have completed (and their per-batch consistency checks in Step 5.5), run **one final global consistency check** across all 13 specs. This catches cross-batch conflicts that batch-level checks miss.

**Agent prompt:** "Read ALL spec files in `clients/gap-close-specs/{SDK_NAME}/` and the TYPE-REGISTRY.md. Focus specifically on: (1) shared files modified by specs in different batches (e.g., `pyproject.toml`, `__init__.py`, `ClientConfig`), (2) config fields added by specs across batches that may conflict in `__post_init__` validation order, (3) type references crossing batch boundaries (Batch 2/3 referencing Batch 1 types). Write a report to `clients/gap-close-specs/{SDK_NAME}/consistency-check.md`. Fix conflicts directly — registry is authoritative."

**Gate:** All cross-batch conflicts resolved before Step 6.

---

## Step 6: Update Coverage Checklist

After all spec agents complete, update the coverage checklist:
1. Read each spec file and extract the REQ-* items it covers
2. Mark each item as `[x]` in the checklist
3. Identify any uncovered items — these are gaps in the specs that need to be addressed
4. If uncovered items exist, launch targeted agents to add them to the appropriate spec files

**Gate:** ALL items in the coverage checklist must be `[x]` before proceeding to review.

---

## Step 7: Expert Review — Round 1 (Architecture Review)

Launch **one reviewer agent per batch** (3 agents total, in parallel). Each reviewer is a senior SDK architect who validates the technical design.

### Reviewer Configuration

| Batch | Reviewer Expertise | Spec Files to Review | Review Output |
|-------|-------------------|---------------------|---------------|
| 1 | Senior {SDK_LABEL} architect, gRPC/transport expert | 01, 02, 03, 07 specs | `clients/gap-close-specs/{SDK_NAME}/review-r1-batch1.md` |
| 2 | Senior {SDK_LABEL} architect, testing/observability expert | 04, 05, 06, 08, 09 specs | `clients/gap-close-specs/{SDK_NAME}/review-r1-batch2.md` |
| 3 | Senior {SDK_LABEL} architect, ops/packaging expert | 10, 11, 12, 13 specs | `clients/gap-close-specs/{SDK_NAME}/review-r1-batch3.md` |

Use ARCHITECT-REVIEWER-PROMPT (see below), substituting appropriate variables.

---

## Step 8: Apply Review Fixes — Round 1

After all Round 1 reviewers complete, launch **one fixer agent per batch** (3 agents, in parallel).

Use FIXER-AGENT-PROMPT (see below) with `{ROUND}` = `1`.

---

## Step 8.5: Update Type Registry After R1 Fixes

After all Round 1 fixes are applied, launch a lightweight agent to re-read all spec files and update TYPE-REGISTRY.md to reflect any type changes made during R1 fixes. This prevents the stale-registry problem (discovered in Python R2-B1 where `CredentialProvider` was split into sync/async variants but the registry was not updated).

**Agent prompt:** "Read all spec files in `clients/gap-close-specs/{SDK_NAME}/` and extract all public type definitions (classes, interfaces, exceptions, protocols) with their full signatures. Compare against `clients/gap-close-specs/{SDK_NAME}/TYPE-REGISTRY.md`. Update the registry to match the current spec content. Flag any discrepancies found."

---

## Step 9: Expert Review — Round 2 (Implementability Review)

**CHECKPOINT:** Before launching Round 2 reviewers, verify ALL fixer agents from Step 8 have completed AND the TYPE-REGISTRY.md has been updated (Step 8.5). Check that each spec file has been modified (where fixes were needed) and each review file contains a "Fixes Applied (Round 1)" section with a change log.

After all Round 1 fixes are verified applied, launch **one reviewer agent per batch** (3 agents, in parallel). Each reviewer is a senior developer who validates that the specs are implementable.

Use IMPLEMENTER-REVIEWER-PROMPT (see below), substituting appropriate variables.

Review output files: `clients/gap-close-specs/{SDK_NAME}/review-r2-batch{N}.md`

---

## Step 10: Apply Review Fixes — Round 2 (Final)

After all Round 2 reviewers complete, launch fixer agents one final time with `{ROUND}` = `2`.

---

## Step 10.5: Update Type Registry After R2 Fixes

Same process as Step 8.5. After all Round 2 fixes are applied, update TYPE-REGISTRY.md one final time to ensure it reflects the definitive state of all types across all specs.

---

## Step 11: Final Coverage Verification

After all review-fix iterations:
1. Re-read the coverage checklist
2. Verify ALL items are still `[x]` (fixes may have moved or removed items)
3. Update any changed mappings
4. Write final coverage statistics

---

## Step 11.5: Resolve Open Questions

After final coverage verification, collect ALL open questions from the 6 review files (R1 and R2, 3 batches each). Present them to the user with proposed decisions for each. After user confirmation, embed the decisions into the relevant spec files.

### Process

1. Read all 6 review files and extract every item in "Open Questions" sections.
2. Read SPEC-SUMMARY.md for any additional risks or unresolved items.
3. For each open question, propose a concrete decision with rationale.
4. Present all proposed decisions to the user for confirmation.
5. After confirmation, update the relevant spec files to embed each decision (modify code snippets, add notes, update configuration tables, etc.).
6. Update SPEC-SUMMARY.md to replace the "Unresolved Open Questions" section with a "Resolved Decisions" table.

**Gate:** All open questions resolved and embedded in specs before generating the final summary.

---

## Step 12: Generate Summary

Create `clients/gap-close-specs/{SDK_NAME}/SPEC-SUMMARY.md` using the SUMMARY-TEMPLATE below.

Read ONLY the executive summary section from each spec file and the coverage checklist to avoid context overflow.

---

## Step 12.5: Generate Retrospective Notes

After the summary, create `clients/gap-close-specs/{SDK_NAME}/RETROSPECTIVE-NOTES.md` by analyzing all 6 review report files (3 from R1, 3 from R2). Extract:

1. **Patterns of issues** — What types of issues were found most frequently across batches? (e.g., "missing test scenarios," "incorrect file paths," "inconsistent type names")
2. **Rules to add** — Based on repeated issues, what new rules should be added to the SPEC-AGENT-PROMPT to prevent these in future runs?
3. **Template gaps** — Which spec template sections were consistently weak, missing, or produced low-quality output?
4. **Review process improvements** — What review dimensions caught the most issues? What dimensions caught nothing? Should any be added/removed?
5. **Cross-batch patterns** — Did later batches have fewer issues (benefiting from prior batch specs) or more (due to cross-spec complexity)?

This file enables iterative improvement of the skill after each run.

---

## Step 13: Report

Present to user:

    ## Gap Close Specs Complete

    **Output Directory:** clients/gap-close-specs/{SDK_NAME}/
    **Summary:** clients/gap-close-specs/{SDK_NAME}/SPEC-SUMMARY.md
    **Checklist:** clients/gap-close-specs/{SDK_NAME}/COVERAGE-CHECKLIST.md
    **Retrospective:** clients/gap-close-specs/{SDK_NAME}/RETROSPECTIVE-NOTES.md
    **Specs Generated:** {count} (13 categories)
    **Review Rounds:** 2 (architect + implementer)
    **Coverage:** {n}/{n} gap items covered (100%)

    ### Review Impact
    - Round 1 (Architecture): {summary}
    - Round 2 (Implementability): {summary}

    ### Risks and Open Questions
    - {count} unresolved items (see summary)

    ### Recommended Next Step
    Use the specs to begin implementation, starting with the Phase 1 foundation specs
    (01-error-handling, 02-connection, 07-code-quality).
    Review RETROSPECTIVE-NOTES.md for process improvements before running this skill on the next SDK.

---

## SPEC-AGENT-PROMPT

<prompt-template id="spec-agent">
You are a senior {SDK_LABEL} SDK architect writing a detailed implementation specification for the KubeMQ {SDK_LABEL} SDK.

## Your Task

Convert the gap research findings for GS Category {CATEGORY_NUM} ({CATEGORY_NAME}) into a concrete, implementable specification. The spec must be detailed enough that a developer can implement directly from it without needing to consult other documents.

## Input Files — READ ALL OF THESE

1. **Gap Research Report:** Read `{GAP_RESEARCH_FILE}` — find the section for Category {CATEGORY_NUM}. Extract ALL REQ-* items, their statuses, remediation notes, and dependencies.
2. **Golden Standard Spec:** Read `{GS_SPEC_FILE}` — the target requirements for this category
3. **Assessment Report:** Read `{ASSESSMENT_FILE}` — current state evidence and file:line references
4. **Golden Standard Index:** Read `clients/sdk-golden-standard.md` — for cross-category context
5. **SDK Source Code:** Explore the SDK source tree to understand current file structure, naming conventions, and patterns. Use Glob and Grep to find relevant existing code.
6. **Gap Research Review Files:** Read `clients/gap-research/{SDK_NAME}-review-r1.md` and `clients/gap-research/{SDK_NAME}-review-r2.md` — focus on the "Additional {SDK_LABEL}-Specific Recommendations" sections and "Effort Estimate Corrections" tables. These contain language-specific architectural insights (e.g., ClientInterceptor chain patterns, CompletableFuture exception unwrapping, Maven BOM usage, SLF4J 2.x compatibility) that must inform the spec design.
7. **Prior Batch Specs (if Batch 2+):** Read these specs from prior batches for cross-references: {PRIOR_SPECS}
8. **Type Registry:** Read `{TYPE_REGISTRY_FILE}` — canonical type definitions. All new types MUST match registry entries exactly. See Rule 26.
9. **Language Constraints (if exists):** Read `{LANGUAGE_CONSTRAINTS_FILE}` — language-specific pitfalls and rules from prior runs

## CRITICAL: Source Code Exploration

You MUST explore the actual SDK source code to:
- Identify existing files that need modification (with current line numbers)
- Understand current naming conventions, package structure, and patterns
- Find existing interfaces/classes that the spec will extend or modify
- Verify file paths referenced in the assessment report still exist
- Understand import patterns and dependency usage

Use Glob to find relevant files, Grep to search for patterns, and Read to examine specific files.

## Spec Structure

Write to `{OUTPUT_FILE}` using this format:

--- BEGIN SPEC FORMAT ---

# {SDK_LABEL} SDK — Category {CATEGORY_NUM}: {CATEGORY_NAME} Implementation Spec

**Status:** {GAPS_EXIST | COMPLIANT_WITH_POLISH | FULLY_COMPLIANT}
**Gap Research Source:** {GAP_RESEARCH_FILE}
**Golden Standard:** {GS_SPEC_FILE}
**Priority:** {P0 | P1 | P2 | P3} (from gap research)
**Estimated Effort:** {total effort for this category}
**Breaking Changes:** {Yes — list / No}

---

## Executive Summary

**Current State:** {brief description of what exists today}
**Target State:** {brief description of what needs to exist}
**Key Changes:** {bulleted list of major changes}
**Dependencies on Other Specs:** {list specs this depends on, with specific items}
**Specs That Depend on This:** {list specs that depend on this one}

---

## Prerequisites

Before implementing this spec:
1. {spec/REQ that must be done first, with reason}
2. {spec/REQ}

---

## Detailed Specifications

### SPEC-{CATEGORY_NUM}-1: {First REQ-* title}

**REQ Reference:** {REQ-ID} from {GS_SPEC_FILE}
**Gap Status:** {COMPLIANT | PARTIAL | MISSING | NOT_ASSESSED | EXCESS}
**Priority:** {P0-P3}
**Effort:** {S/M/L/XL}
**Breaking Change:** {Yes — describe impact / No}

#### Current State

{What exists today. Reference specific files with line numbers from the SDK source code.}

```{language}
// Current code at path/to/file.ext:line
{relevant current code snippet}
```

#### Target State

{What must exist after implementation. Be precise about interfaces, classes, methods.}

#### Interface / Type Definitions

**Compilability checklist** (mentally verify before writing):
- [ ] All imports resolve to real types (JDK, dependencies, or types defined in this spec/another spec)
- [ ] All referenced types exist (no undefined types in signatures or bodies)
- [ ] Generics are correctly bounded and consistent
- [ ] Checked exceptions (if applicable) are declared in `throws` clauses
- [ ] Type names do not collide with standard library classes (see Rule 20)
- [ ] Type matches TYPE-REGISTRY.md entry exactly (name, package, constructor signature)

```{language}
// New/modified types to create
// Include ALL imports at the top of each snippet
{concrete interface/class definitions with all methods, fields, annotations}
// For types defined in other specs:
// Stub: XyzType defined in XX-spec.md (package: com.example.xyz)
```

#### Implementation Details

1. **Step 1:** {concrete action with file path}
   - Create/modify `path/to/file.ext`
   - {specific change description}

2. **Step 2:** {concrete action}
   - {details}

#### Sync Equivalent

{If this spec item has an async implementation above, show the sync counterpart here.
Options: (a) full sync code, (b) "same pattern, replace `await` with direct call",
(c) show the sync wrapper code, (d) "sync deferred to spec XX — reason."
Omit this section ONLY for items that are sync-only or async-only with no counterpart.}

#### Migration Notes

{If this is a breaking change or modifies existing behavior:}
- **What breaks:** {specific API/behavior change}
- **Migration path:** {how users should migrate}
- **Deprecation period:** {timeline, e.g., "deprecate in 2.x, remove in 3.0"}

#### Configuration

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| {name} | {type} | {value} | {description} |

#### Error Handling

{How errors are produced/handled in this spec. Reference error types from 01-error-handling-spec.md if applicable.}

#### Thread Safety

{Thread safety guarantees for types introduced or modified by this spec item. Omit for items that don't involve concurrency. MUST specify the exact synchronization mechanism — never state "thread-safe" without specifying how.}
- **{TypeName}:** {Thread-safe / Not thread-safe / Immutable}
- **Synchronization mechanism:** {REQUIRED — specify exactly: e.g., "ConcurrentHashMap for state map, ReentrantLock for state transitions, AtomicInteger for retry counter, volatile for shutdown flag"}
- **CAS/spin loops:** {If applicable: bounded retry count, backoff strategy}
- **Lock ordering:** {If multiple locks: acquisition order to prevent deadlocks}

#### Logging

{Log statements to add for this spec item. Omit for items that don't produce operational events.}

| Event | Level | Message Template | Structured Fields |
|-------|-------|-----------------|-------------------|
| {event} | {INFO/DEBUG/WARN/ERROR} | {template} | {key=value pairs} |

#### Observability Impact

{OTel spans/metrics to emit for this spec item. Cross-reference to 05-observability-spec.md. Omit for items without operational impact.}
- **Spans:** {span names and attributes, or "None"}
- **Metrics:** {metric names and labels, or "None"}

#### Test Scenarios

| # | Scenario | Type | Setup | Action | Assert |
|---|----------|------|-------|--------|--------|
| 1 | {scenario name} | Unit | {setup: mocks, fixtures, state} | {actual method call being tested} | {expected result or error} |
| 2 | {scenario name} | Integration | {setup: server, client config} | {actual method call} | {expected result} |
| 3 | {scenario name} | Edge case | {setup: boundary condition} | {actual method call} | {expected error or behavior} |

{Tests MUST call the actual method being tested (see Rule 32). The Action column must show a real method invocation, not a precondition assertion.}

#### Acceptance Criteria Checklist

- [ ] {criterion from GS, verbatim}
- [ ] {criterion}
- [ ] {criterion}

#### Integration Verification Criteria

{MANDATORY for any spec item that creates a new class, manager, executor, or infrastructure component.
For each new component, list the production file that MUST import and call it. A REQ is NOT
complete until every row in this table is verified by the implementer.}

| New Component | Must Be Imported By | Must Be Called In | Call Context |
|---------------|---------------------|-------------------|-------------|
| {ClassName} | {production/file path} | {method name} | {when/why it's called} |

{If the integration target belongs to a different spec, add a cross-reference:
"Wiring owned by spec XX, WU-N. This spec creates the component; spec XX wires it.
Both WUs must complete for this REQ to be DONE."}

{Omit this section ONLY for spec items that modify existing code without creating new
standalone components (e.g., adding a field, changing a default, documentation-only changes).}

#### Breaking Change Audit

{Mandatory per Rule 31. Mark each category "none" or list the specific changes.}

- [ ] Default value changes: {list or "none"}
- [ ] Method signature changes: {list or "none"}
- [ ] Serialization/wire format changes: {list or "none"}
- [ ] Type annotation narrowing: {list or "none"}
- [ ] Import path changes: {list or "none"}
- [ ] Behavioral changes (same API, different result): {list or "none"}

---

### SPEC-{CATEGORY_NUM}-2: {Next REQ-* title}

{Same structure for each REQ-* in this category}

---

## Files to Create

| # | File Path | Purpose | Complexity |
|---|-----------|---------|------------|
| 1 | `src/main/java/io/kubemq/sdk/error/KubeMQException.java` | Base exception type | M |
| ... | | | |

## Files to Modify

| # | File Path | Changes | Impact |
|---|-----------|---------|--------|
| 1 | `src/main/java/io/kubemq/sdk/client/KubeMQClient.java` | Add error wrapping, update return types | Medium |
| ... | | | |

## Files to Delete / Deprecate

| # | File Path | Reason | Timeline |
|---|-----------|--------|----------|
| 1 | {path} | {reason} | {when} |

## Implementation Order

Within this category, implement in this order:

1. {SPEC-XX-N} — {reason it's first, e.g., "foundation type needed by all others"}
2. {SPEC-XX-N} — {reason}
3. {SPEC-XX-N} — {reason}

## Cross-Category Integration Points

| This Spec Item | Integrates With | How |
|----------------|----------------|-----|
| SPEC-{XX}-1 | SPEC-{YY}-N from {other spec} | {description of integration} |

### Integration Wiring Code

{Per Rule 33: Show the actual import statements, constructor calls, and initialization code
that connects this spec's types to types from other specs. Text references are insufficient —
show the code. This section is especially critical for Batch 2 and 3 specs.}

```{language}
// Example: how this spec's types are wired to types from other specs
// import {type} from {spec XX's package}
// constructor call showing integration
```

## Implementation Commit Sequence

A concrete sequence of commits that allows incremental implementation with tests at each step. Each intermediate state MUST compile and pass existing tests.

1. **Commit 1:** "{commit message}" — Create/modify: `{file paths}` — Depends on: {nothing / commit N} — Tests: {what tests to add/run}
2. **Commit 2:** "{commit message}" — Create/modify: `{file paths}` — Depends on: commit 1 — Tests: {what tests}
3. {continue for all implementation steps}

**Rollback points:** {identify safe rollback points where the system is in a consistent state}

--- END SPEC FORMAT ---

## IMPORTANT RULES

1. **Source code first.** Before writing any spec, explore the actual SDK source code. Every file path, class name, and method reference must be verified against the real codebase.
2. **Concrete, not abstract.** Include actual interface/class definitions, method signatures, and type names. "Add an error type" is too vague. Show the actual class definition with fields, constructors, and methods.
3. **Preserve what works.** COMPLIANT items get a brief confirmation section, not a rewrite. Don't spec changes to things that already work.
4. **Breaking changes are explicit.** Every breaking change must be flagged with migration path and timeline. Group breaking changes at the end of the spec for visibility.
5. **Test scenarios are concrete.** Each test scenario must describe the setup, action, and assertion — not just a title.
6. **File paths are real.** Use actual file paths from the SDK source tree. Verify they exist using Glob/Grep.
7. **Dependencies are bidirectional.** State both what this spec depends on and what depends on this spec.
8. **Language-idiomatic.** All code examples must use {SDK_LABEL} idioms. Java uses builders, Go uses functional options, etc.
9. **Cross-reference gap research.** Every spec item must trace to a specific REQ-* and gap status from the gap research.
10. **Implementation order matters.** Within each spec, define the order items should be implemented based on internal dependencies.
11. **Effort is calibrated.** Effort estimates must be {SDK_LABEL}-specific and account for test writing time.
12. **No orphaned items.** Every REQ-* in this category must appear in the spec, even COMPLIANT ones (as confirmation entries).
13. **Evaluate ALL normative GS text.** Evaluate ALL normative requirements in the GS spec, including body text paragraphs, tables, and notes — not just the acceptance criteria checklist. Requirements stated in body text are equally binding and must be reflected in the spec.
14. **Future Enhancement sections.** Check for "Future Enhancement" subsections in the GS spec. Design-ahead items (e.g., PartialFailureError, IdempotencyKey, Retry-After) must be reflected in interface designs even if not implemented immediately. Add a "Future-Proofing" note in the relevant spec section.
15. **Flag GS internal inconsistencies.** If you discover conflicting defaults, requirements, or acceptance criteria between two GS specs, flag the conflict explicitly in the spec (e.g., as an "Open Question") rather than silently choosing one value.
16. **COMPLIANT-only categories.** If the gap research shows ALL REQ-* items in this category as COMPLIANT, produce a confirmation spec (20-30 lines) with: category header, status FULLY_COMPLIANT, list of confirmed REQ items, any polish items noted in gap research. Skip interface definitions, implementation steps, and test scenarios.
17. **Context management for large categories.** If a category has more than 6 REQ-* items, prioritize depth on MISSING/PARTIAL items and use brief confirmation entries (5-10 lines) for COMPLIANT items. Focus context budget on the items that need real specification.
18. **NOT_ASSESSED items.** For gap research items marked NOT_ASSESSED where no current-state evidence exists, write the spec as if the item is MISSING but add a "Verification Needed" flag. The implementer must verify current state before starting work. If exploring the SDK source code reveals evidence of the feature, update the status accordingly.
19. **No import aliases.** Some languages (e.g., Java) do not support import aliases (`import X as Y`). Use fully-qualified names or static imports where needed. Check the language constraints file for your target language.
20. **Verify JDK/stdlib name collisions.** Before naming any new class, check if the name exists in the language's standard library (e.g., `java.lang`, `java.util`, `java.util.concurrent`, `java.io` for Java). If it does, prefix with `KubeMQ` or use a distinct name. Document the collision check and rationale.
21. **Every code snippet must be self-contained.** Include all imports, class declarations, and referenced types. If a type is defined in another spec, add a stub definition comment: `// Stub: defined in XX-spec.md`. Agents should mentally trace every import to verify it resolves to a real type.
22. **No undefined method references.** Every method referenced in implementation steps must have a complete signature and body somewhere in the spec. If deferred to another spec, provide a stub interface with the full signature and a `// Defined in XX-spec.md` comment.
23. **Canonical artifact ownership.** When multiple specs touch the same artifact (CI config, CHANGELOG, CONTRIBUTING.md, version constants), exactly one spec owns the canonical definition as recorded in TYPE-REGISTRY.md. Other specs reference it with: "See spec XX for canonical definition." Never duplicate artifact definitions.
24. **Thread safety in examples.** All code examples involving shared mutable state must specify the exact synchronization mechanism used (what lock, atomic, volatile, or concurrent collection). CAS loops must have bounded retry with backoff. Semaphores/locks must be released in `finally` blocks. Never state "thread-safe" without specifying how.
25. **`provided` scope isolation.** For `provided`-scope or optional dependencies (OTel, SLF4J, logging facades), all importing classes must be loaded lazily via factory/proxy pattern. Direct imports in eagerly-loaded classes are forbidden — they cause `NoClassDefFoundError` at runtime.
26. **Cross-spec type registry compliance.** Before defining a new public type, check `TYPE-REGISTRY.md` for existing definitions. Use the EXACT name, package, and constructor signature from the registry. If the type is not in the registry, flag it with `// NEW TYPE — not in registry` for post-batch consistency check.

27. **Type annotation/signature verification.** Every code snippet's type annotations MUST mentally resolve to concrete types — not modules, not undefined generics, not forward references without imports. For each Protocol/interface, document how BOTH sync and async callers use it. If a Protocol method is async, provide the sync equivalent or state it's async-only.
28. **Dead code elimination pass.** Every method, import, or parameter defined in a code snippet MUST have at least one call site shown in the spec. If a method is not called anywhere in the spec, either (a) show the call site, (b) remove it, or (c) document it as "called by spec XX §Y" with a specific cross-reference. Unreferenced code is a review magnet.
29. **Sync/async parity.** For every async implementation shown, the spec MUST address the sync equivalent. Options: (a) show the full sync code, (b) state "same pattern, replace `await` with direct call," (c) show the sync wrapper code, or (d) explicitly state "sync variant deferred to spec XX." Omitting the sync path without explanation is a review-flaggable gap.
30. **stdlib/library API citation.** Any code using stdlib or third-party APIs MUST cite the specific API it relies on (e.g., "per `asyncio.Semaphore` docs" or "per gRPC `channel_ready_future()`"). Assumed API semantics are the #3 source of review issues. Verify: (a) the API exists, (b) it accepts the arguments used, (c) it's available in the minimum supported language version.
31. **Breaking change completeness checklist.** Every spec MUST include a "Breaking Change Audit" section with these mandatory categories: default value changes, method signature changes, serialization/wire format changes, type annotation narrowing, import path changes, behavioral changes (same API, different result). Mark each "none" or list the changes. Omitting a category is a review-flaggable gap.
32. **Test assertions must test the SUT.** Test code snippets MUST call the actual method being tested and assert on its output or side-effects. `assert len(body) > max_size` is NOT a test — it's an assertion about a constant. Tests MUST call the method that performs the validation and assert the expected error/result. All test imports MUST be complete — every name used in the test MUST have a visible import.
33. **Late-batch integration code.** Specs in Batch 2 and 3 that reference types or infrastructure from earlier batches MUST show the actual integration/wiring code: import statements, constructor calls, configuration setup. Text references like "uses ReconnectionManager from spec 02" are insufficient for later batches. The later the spec in the dependency order, the more explicit its cross-spec integration code must be.
34. **Integration wiring is not deferrable.** Every new infrastructure component (class, manager, executor, handler) MUST have its integration call site shown in the spec — either in this spec's own implementation code, or as an explicit cross-reference to the spec that owns the wiring. If a ReconnectionManager is created, the spec MUST show the code in AsyncTransport (or equivalent) that calls `start_reconnection()`. If the wiring belongs to another spec, the "Integration Verification Criteria" table MUST list the owning spec and the exact WU. A component without a documented integration path will be flagged as incomplete during review.

## Language-Specific Constraints

If a language constraints file exists at `{LANGUAGE_CONSTRAINTS_FILE}`, read it and follow ALL rules listed there. These are language-specific pitfalls discovered from prior runs that prevent common errors.

## Type Registry

Read `{TYPE_REGISTRY_FILE}` before writing any type definitions. This registry is authoritative for:
- Class/interface names and packages
- Constructor signatures
- Artifact ownership (which spec owns CI config, CHANGELOG, etc.)

If you need to define a type not in the registry, provide the full definition and flag it for the post-batch consistency check.
</prompt-template>

---

## ARCHITECT-REVIEWER-PROMPT

<prompt-template id="architect-reviewer">
You are a senior {SDK_LABEL} SDK architect reviewing implementation specifications for the KubeMQ {SDK_LABEL} SDK. You have extensive experience designing production {SDK_LABEL} SDKs with gRPC, messaging, and OpenTelemetry.

## Your Task

Review the implementation specs for Batch {BATCH_NUM} categories. Validate technical design, API consistency, dependency correctness, and architectural soundness. This is review round 1 of 2 (Architecture Review).

## Input Files — READ ALL OF THESE

1. **Spec files to review:** {SPEC_FILES_LIST}
2. **Gap Research Report:** Read `{GAP_RESEARCH_FILE}` — to verify specs cover all findings
3. **Golden Standard specs for reviewed categories:** {GS_SPEC_FILES_LIST}
4. **All 13 Golden Standard category specs:** Read `clients/golden-standard/01-error-handling.md` through `clients/golden-standard/13-performance.md` — for cross-category validation
5. **Golden Standard Index:** Read `clients/sdk-golden-standard.md` — for tier definitions and targets
6. **Coverage Checklist:** Read `clients/gap-close-specs/{SDK_NAME}/COVERAGE-CHECKLIST.md`
7. **Type Registry:** Read `clients/gap-close-specs/{SDK_NAME}/TYPE-REGISTRY.md` — verify all types match registry
8. **SDK Source Code:** Explore the actual source to verify file paths and current state claims

## Review Dimensions

### 1. Source Code Verification
- Are current-state code snippets accurate and up-to-date?
- Do referenced file paths exist in the SDK source tree?
- Are line numbers current?
- Do import patterns and naming conventions match the real codebase?

### 2. Completeness
- Does every REQ-* from the gap research appear in the spec?
- Are all acceptance criteria from the GS covered?
- Are COMPLIANT items confirmed (not omitted)?
- Are cross-category dependencies fully specified?

### 3. Technical Correctness
- Are interface/class definitions valid {SDK_LABEL} code?
- Are type signatures correct (generics, exceptions, nullability)?
- Do proposed implementations actually satisfy the GS requirements?
- Are there architectural conflicts between specs?

### 4. Cross-Spec Type Consistency
- Do all type/class/interface names match the TYPE-REGISTRY.md entries exactly?
- Are package paths consistent across specs for the same type?
- Is artifact ownership respected (only one spec defines CI config, CHANGELOG, etc.)?
- Are cross-spec references using the correct type names and packages?

### 5. Integration Wiring Completeness
- Does every spec item that creates a new standalone component (class, manager, executor) include an "Integration Verification Criteria" table?
- For each entry in the table: is the target file realistic (does it exist in the SDK source)?
- If wiring is cross-spec: is the owning spec and WU explicitly named?
- Are there any components with no documented call site? Flag as CRITICAL — a component without a caller is dead code.

### 6. Dependency Accuracy
- Are all cross-spec dependencies correctly identified?
- Is the implementation order feasible given dependencies?
- Are there circular dependencies?
- Are external library dependencies appropriate and up-to-date?

### 7. Code Snippet Validity
- Do all code snippets compile (mentally trace imports, generics, exceptions)?
- Are there import aliases or other invalid syntax for the target language?
- Do type names avoid collisions with standard library classes?
- Are `provided`-scope dependencies loaded lazily (no direct imports in eagerly-loaded classes)?

### 8. Testability
- Are test scenarios sufficient to verify the spec?
- Are edge cases covered?
- Are integration test requirements realistic (infrastructure needed)?
- Can the spec be implemented incrementally with tests at each step?
- Do test scenarios include Setup/Action/Assert columns (not just expected behavior)?
- Do tests call the actual method being tested (not just verify preconditions)?

### 9. Execution Path Tracing
- For each spec, trace ONE complete call path from public API entry point to transport/infrastructure call.
- Verify every method called along the path is defined (in this spec or cross-referenced to another spec).
- Flag methods that are defined but never called along any traced path (dead code).
- Flag methods that are called but never defined in any spec (missing implementation detail).
- This dimension specifically targets the dead-code and missing-detail issue patterns.

## Output

Write to `{REVIEW_OUTPUT_FILE}` using this format:

# {SDK_LABEL} SDK Specs — Architecture Review (Round 1, Batch {BATCH_NUM})

**Reviewer:** Senior {SDK_LABEL} SDK Architect
**Specs Reviewed:** {spec file list}
**Date:** {date}

## Review Summary

| Spec | Issues Found | Critical | Major | Minor |
|------|-------------|----------|-------|-------|
| {spec} | {n} | {n} | {n} | {n} |
| ... | | | | |

## Critical Issues (MUST FIX)

### C-{n}: {title}
**Spec:** {spec file}
**Section:** {section}
**Current:** {what the spec says}
**Should be:** {what it should say}
**Reason:** {why, with GS or source code evidence}

## Major Issues (SHOULD FIX)

### M-{n}: {title}
**Spec:** {spec file}
**Current:** {what spec says}
**Recommended:** {improvement}
**Rationale:** {why}

## Minor Issues (NICE TO FIX)

### m-{n}: {title}
**Current:** {what spec says}
**Suggested:** {improvement}

## Missing Coverage

| # | REQ-* | Expected In | Status |
|---|-------|------------|--------|
| 1 | {req} | {spec file} | Not found in spec |

## Cross-Spec Consistency Issues

| # | Spec A | Spec B | Inconsistency |
|---|--------|--------|---------------|
| 1 | {spec} | {spec} | {description} |

## {SDK_LABEL}-Specific Recommendations

{Language-specific insights, better patterns, ecosystem considerations}

## Open Questions

{Genuine ambiguities that cannot be resolved from input files alone. Document rather than making silent assumptions.}

| # | Spec | Question | Context |
|---|------|----------|---------|
| 1 | {spec} | {question} | {why it matters} |

## IMPORTANT RULES

1. **Verify against source code.** Don't just review the spec text — check that file paths exist and current code matches claims.
2. **Check GS coverage.** Every REQ-* and acceptance criterion must be addressed in the spec. Read ALL GS category specs, not just the ones being reviewed, to catch cross-category issues.
3. **Be constructive.** Every issue must include what should change.
4. **Don't rewrite.** Flag issues; the fixer will edit. Don't produce alternative specs.
5. **Severity discipline.** Critical = wrong design that would fail GS criteria or incorrect source code references. Major = suboptimal but functional. Minor = polish.
6. **Cross-spec consistency.** Check that types/interfaces referenced across specs match TYPE-REGISTRY.md. Flag any type that doesn't match the registry.
7. **Open Questions.** If you encounter a genuine ambiguity that cannot be resolved from the input files, document it in the Open Questions section rather than making a silent assumption.
8. **Reduced depth for content-only specs.** For documentation-focused specs (e.g., 06-documentation) that don't define new types or code, focus review on completeness and accuracy. Skip deep technical correctness and code snippet validation checks.
</prompt-template>

---

## IMPLEMENTER-REVIEWER-PROMPT

<prompt-template id="implementer-reviewer">
You are a senior {SDK_LABEL} developer reviewing implementation specifications for the KubeMQ {SDK_LABEL} SDK. You will be the one implementing these specs, so you are reviewing for practical implementability.

## Your Task

Review the implementation specs for Batch {BATCH_NUM} categories from the perspective of a developer who will implement them. Validate that the specs are concrete enough, the implementation steps are feasible, and nothing critical is missing. This is review round 2 of 2 (Implementability Review).

## Round-Specific Focus

The specs have already been through an architecture review (Round 1). Focus on:
1. Verifying that Round 1 fixes were correctly applied
2. **Practical implementability** — Can I actually build this?
3. **Missing implementation details** — What questions would I have while coding?
4. **Test feasibility** — Can the test scenarios be written as described?
5. **Incremental implementation** — Can I implement and test in small steps?

Read the Round 1 review files to see what was already addressed: {R1_REVIEW_FILES}

## Input Files — READ ALL OF THESE

1. **Spec files to review:** {SPEC_FILES_LIST}
2. **Round 1 review files:** {R1_REVIEW_FILES}
3. **SDK Source Code:** Explore the actual source to verify claims and check feasibility
4. **Gap Research Report:** `{GAP_RESEARCH_FILE}`
5. **Golden Standard specs for reviewed categories:** {GS_SPEC_FILES_LIST} — to verify acceptance criteria coverage
6. **Golden Standard Index:** Read `clients/sdk-golden-standard.md`
7. **Type Registry:** Read `clients/gap-close-specs/{SDK_NAME}/TYPE-REGISTRY.md` — verify types match

## Review Dimensions

### 1. Implementation Clarity
- Can each step be implemented without ambiguity?
- Are there missing intermediate steps?
- Are code snippets correct and compilable?
- Are imports and dependencies specified?

### 2. Incremental Build Path
- Can the spec be implemented incrementally (not all-or-nothing)?
- Is there a natural commit sequence?
- Can each intermediate state compile and pass existing tests?
- Are there safe rollback points?

### 3. Test Feasibility
- Can each test scenario be implemented with available tools?
- Are mock/stub requirements clear?
- Are integration test infrastructure requirements documented?
- Are test data/fixtures defined?

### 4. Edge Cases
- Are error paths fully specified?
- What happens with null/empty inputs?
- Concurrent access scenarios covered?
- Resource cleanup on failure?

### 5. Performance Implications
- Are there obvious performance concerns in the design?
- Are allocations reasonable?
- Are there potential memory leaks?
- Are there blocking calls on critical paths?

### 6. Commit Sequence Validation
- Does the spec include an "Implementation Commit Sequence" section?
- Can each commit compile independently and pass existing tests?
- Are dependencies between commits correctly ordered?
- Does the commit sequence match the spec's "Implementation Order" section? Flag conflicts.

### 7. Round 1 Fix Verification
- Were all Critical and Major issues from Round 1 correctly resolved?
- Any regressions introduced by fixes?

## Output

Write to `{REVIEW_OUTPUT_FILE}` using this format:

# {SDK_LABEL} SDK Specs — Implementability Review (Round 2, Batch {BATCH_NUM})

**Reviewer:** Senior {SDK_LABEL} Developer (Implementer)
**Specs Reviewed:** {spec file list}
**Date:** {date}

## Round 1 Fix Verification

| R1 Issue | Status | Verification |
|----------|--------|-------------|
| C-1 | VERIFIED/REGRESSION | {check} |
| ... | | |

## Review Summary

| Spec | Issues Found | Critical | Major | Minor |
|------|-------------|----------|-------|-------|
| ... | | | | |

## Critical Issues (MUST FIX)
{only NEW issues not in R1}

## Major Issues (SHOULD FIX)
{only NEW issues}

## Minor Issues (NICE TO FIX)
{only NEW issues}

## Missing Implementation Details

| # | Spec | Section | What's Missing |
|---|------|---------|---------------|
| 1 | {spec} | {section} | {detail needed to implement} |

## Required: Commit Sequence Validation

For each spec, validate the "Implementation Commit Sequence" section that should already exist in the spec. Verify that: (1) each commit can compile independently, (2) dependencies are correctly ordered, (3) tests are added at each step, (4) it matches the "Implementation Order" section. If the spec is MISSING a commit sequence section, flag it as a Major issue. If conflicts exist between the commit sequence and implementation order, flag the discrepancy.

### {spec file}
1. **Commit 1:** {what to implement and test}
2. **Commit 2:** {what to implement and test}
{...}

### {next spec file}
{...}

## Global Implementation Order

{A master cross-spec implementation sequence covering ALL specs reviewed in this batch.
Order items by dependency (leaf items first), then by priority. This becomes a key input
to the SPEC-SUMMARY.md and eliminates post-hoc ordering work.}

| Step | Spec | Item | Depends On | Effort |
|------|------|------|------------|--------|
| 1 | {spec} | {spec item} | (none) | {effort} |
| 2 | {spec} | {spec item} | Step 1 | {effort} |
| ... | | | | |

## Open Questions

{Genuine ambiguities that cannot be resolved from input files alone.}

| # | Spec | Question | Context |
|---|------|----------|---------|
| 1 | {spec} | {question} | {why it matters} |

## IMPORTANT RULES

1. **Think like an implementer.** Ask "can I build this right now with just this spec?"
2. **Check code snippets.** Do the code examples compile? Are imports correct?
3. **Verify file paths.** Do the files-to-create/modify paths make sense in the current project structure?
4. **Don't duplicate R1.** Only raise NEW issues. Verify R1 fixes, don't re-review them.
5. **Commit sequence is required.** A concrete commit sequence proves the spec is incrementally implementable. Flag conflicts with the spec's Implementation Order.
6. **Be practical.** Flag theoretical concerns only if they have real implementation impact.
7. **Severity discipline.** Critical = spec cannot be implemented as written (compile errors, missing steps, wrong file paths). Major = implementable but will cause rework (missing edge case, wrong test setup). Minor = polish.
8. **Open Questions.** Document genuine ambiguities rather than making silent assumptions.
9. **Review ALL specs in the batch.** Even specs with zero R1 issues must be reviewed from the implementability perspective. The architecture review may not catch practical implementation gaps. Do not skip "clean" specs.
</prompt-template>

---

## FIXER-AGENT-PROMPT

<prompt-template id="fixer-agent">
You are a senior technical editor fixing KubeMQ {SDK_LABEL} SDK implementation specifications based on expert review feedback.

## Your Task

Read the expert review and apply ALL Critical and Major fixes to the spec files. Minor fixes are applied at your discretion (apply if trivial, skip if subjective). This is fix round {ROUND} of 2.

## Input Files — READ ALL OF THESE

1. **Spec files to fix:** {SPEC_FILES_LIST}
2. **Other spec files in this SDK:** Read other spec files in `clients/gap-close-specs/{SDK_NAME}/` that may reference types/interfaces modified by fixes — check for cascading impacts
3. **Review feedback:** Read `{REVIEW_FILE}` — the review to apply
4. **Golden Standard specs** (as needed): `clients/golden-standard/*.md`
5. **SDK Source Code** (as needed): verify corrected file paths and code references

## Fix Process

### Phase 1: Triage
Read the entire review. Categorize each issue:
- **Critical (C-*):** MUST fix.
- **Major (M-*):** SHOULD fix.
- **Minor (m-*):** Apply if trivial, skip if subjective.
- **Missing coverage:** Add missing REQ-* entries or details.

### Phase 2: Apply Fixes
For each issue, edit the appropriate spec file:
1. **Design corrections:** Update interfaces, types, method signatures
2. **Missing details:** Add implementation steps, code examples, test scenarios
3. **Dependency fixes:** Update cross-spec references and implementation order
4. **Breaking change updates:** Add/correct migration paths
5. **Executive Summary:** Update if category-level changes occurred

### Phase 3: Reconcile
After all fixes:
1. Verify cross-spec references are still valid
2. If a type definition or interface was changed, grep all spec files for references to the old type name and update them
3. Update the coverage checklist if items moved between specs
4. Verify implementation order within each spec

## Output

Edit spec files in place. Append a change log to the review file under:

    ## Fixes Applied (Round {ROUND})

    | Issue | Status | Change Made |
    |-------|--------|-------------|
    | C-1 | FIXED | {brief description} |
    | M-1 | FIXED | {brief description} |
    | m-1 | SKIPPED | {reason} |

    **Total:** {n} fixed, {n} skipped

## IMPORTANT RULES

1. **Fix, don't rewrite.** Targeted edits only.
2. **Critical issues are mandatory.** Every C-* must be fixed.
3. **Preserve correct content.** Don't break what works.
4. **Verify against source code.** If fixing a file path, verify it exists.
5. **Update cross-references.** A change in one spec may affect references in others.
6. **Cautionary notes over skipping.** When evidence is ambiguous, add a note rather than skipping.
7. **Track your work.** Every fix or skip must appear in the change log.
</prompt-template>

---

## SUMMARY-TEMPLATE

<template id="summary">

# {SDK_LABEL} SDK — Gap Close Spec Summary

**Generated:** {date}
**SDK:** {SDK_LABEL}
**Specs Generated:** 13
**Coverage:** {n}/{n} gap items (100%)

---

## Spec Overview

| # | Category | Spec File | Status | Priority | Effort | Breaking Changes | Specs Created | Specs Modified |
|---|----------|-----------|--------|----------|--------|-----------------|---------------|----------------|
| 01 | Error Handling | 01-error-handling-spec.md | {status} | {P0-P3} | {effort} | {Yes/No} | {n} files | {n} files |
| ... | | | | | | | | |
| 13 | Performance | 13-performance-spec.md | {status} | {P0-P3} | {effort} | {Yes/No} | {n} files | {n} files |

---

## Implementation Sequence

Based on cross-spec dependencies:

### Phase 1: Foundation
1. {spec with effort and reason}
2. {spec}

### Phase 2: Core Features
1. {spec}
2. {spec}

### Phase 3: Quality & Polish
1. {spec}
2. {spec}

---

## Breaking Changes Summary

| # | Spec | Change | Impact | Migration | Timeline |
|---|------|--------|--------|-----------|----------|
| 1 | {spec} | {change} | {impact} | {migration path} | {when} |

---

## New Files to Create (All Specs Combined)

| # | File Path | Spec Source | Purpose |
|---|-----------|-----------|---------|
| 1 | {path} | {spec} | {purpose} |

**Total new files:** {n}

---

## Files to Modify (All Specs Combined)

| # | File Path | Spec(s) | Changes |
|---|-----------|---------|---------|
| 1 | {path} | {spec list} | {summary of changes} |

**Total modified files:** {n}

---

## Total Effort

| Category | Effort | Priority |
|----------|--------|----------|
| {category} | {S/M/L/XL} | {P0-P3} |
| ... | | |
| **Total** | {estimated days} | |

---

## Risks and Open Questions

| # | Source | Risk/Question | Impact | Recommendation |
|---|--------|--------------|--------|----------------|
| 1 | {spec or review file} | {description} | {impact if not resolved} | {suggested action} |

---

## Review Impact

- **Round 1 (Architecture):** {n} issues ({n} critical, {n} major, {n} minor), {n} fixed
- **Round 2 (Implementability):** {n} issues ({n} critical, {n} major, {n} minor), {n} fixed

---

## Recommended Next Steps

1. Begin implementation with Phase 1 specs
2. Set up CI pipeline (04-testing-spec.md) early for safety net
3. Implement foundation types (error hierarchy, architecture layers) before features

</template>
