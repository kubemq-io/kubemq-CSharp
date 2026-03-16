---
name: gap-research
description: Run a detailed gap analysis comparing each SDK's current state (from assessment reports) against the Golden Standard requirements. Produces per-SDK gap research documents with prioritized remediation items. Runs all SDKs in parallel using a two-pass approach (Tier 1 then Tier 2) to ensure thorough coverage.
argument-hint: "all" or a specific SDK name (go, java, csharp, python, js)
user-invocable: true
allowed-tools: Read, Grep, Glob, Write, Edit, Bash, Agent, WebSearch, WebFetch
---

# SDK Gap Research

Analyze SDK gaps against Golden Standard: $ARGUMENTS

## Overview

Systematic gap analysis that compares each SDK's current assessment scores and findings against the finalized Golden Standard requirements. Produces detailed, actionable gap research documents that serve as the foundation for per-SDK implementation plans.

**KEY PRINCIPLE:** Exhaustive and concrete. Every requirement in the Golden Standard must be traced to a specific current-state finding in the assessment. No gap is too small to document.

---

## Step 1: Validate Input

**$ARGUMENTS is REQUIRED.** Must be one of:
- `all` — Run gap analysis for all 5 SDKs in parallel
- `go` — Go SDK only
- `java` — Java SDK only
- `csharp` — C# / .NET SDK only
- `python` — Python SDK only
- `js` — JS/TS SDK only

**If empty:** STOP, ask: "Which SDK(s) should I analyze? Use `all` for all 5 SDKs, or specify one: go, java, csharp, python, js"

---

## Step 2: Load Reference Documents

Before launching agents, read these files to understand the full context:

### Golden Standard Index
- `clients/sdk-golden-standard.md` — Tier definitions, parity matrix, current scores

### Assessment Reports (map SDK name to file)

| SDK | Assessment File |
|-----|----------------|
| go | `clients/assesments/GO_ASSESSMENT_REPORT.md` |
| java | `clients/assesments/JAVA_ASSESSMENT_REPORT.md` |
| csharp | `clients/assesments/C#-ASSESSMENT-REPORT.md` |
| python | `clients/assesments/PYTHON_SDK_ASSESSMENT_REPORT.md` |
| js | `clients/assesments/JS-ASSESSMENT-REPORT.md` |

### Golden Standard Category Specs

| GS # | Category | File | Tier |
|-------|----------|------|------|
| 01 | Error Handling | `clients/golden-standard/01-error-handling.md` | 1 |
| 02 | Connection & Transport | `clients/golden-standard/02-connection-transport.md` | 1 |
| 03 | Auth & Security | `clients/golden-standard/03-auth-security.md` | 1 |
| 04 | Testing | `clients/golden-standard/04-testing.md` | 1 |
| 05 | Observability | `clients/golden-standard/05-observability.md` | 1 |
| 06 | Documentation | `clients/golden-standard/06-documentation.md` | 1 |
| 07 | Code Quality | `clients/golden-standard/07-code-quality.md` | 1 |
| 08 | API Completeness | `clients/golden-standard/08-api-completeness.md` | 2 |
| 09 | API Design & DX | `clients/golden-standard/09-api-design-dx.md` | 2 |
| 10 | Concurrency | `clients/golden-standard/10-concurrency.md` | 2 |
| 11 | Packaging | `clients/golden-standard/11-packaging.md` | 2 |
| 12 | Compatibility | `clients/golden-standard/12-compatibility-lifecycle.md` | 2 |
| 13 | Performance | `clients/golden-standard/13-performance.md` | 2 |

---

## Step 3: Launch Parallel Agents (Two-Pass Architecture)

To avoid context window overflow, each SDK is analyzed in **two passes**:
- **Pass A:** Tier 1 categories (01-07) — 7 categories, ~50 REQ-* items
- **Pass B:** Tier 2 categories (08-13) — 6 categories, ~23 REQ-* items

For `all`: Launch **10 agents** simultaneously (2 per SDK).
For a single SDK: Launch **2 agents**.

**CRITICAL:** All agents for the same pass MUST be launched in a SINGLE message to maximize parallelism. Launch Pass A agents first, then Pass B agents in a second message (or all 10 together if feasible).

### Agent Launch Table

| SDK | SDK_NAME | SDK_LABEL | ASSESSMENT_FILE | Pass A Output | Pass B Output |
|-----|----------|-----------|-----------------|---------------|---------------|
| go | go | Go | `clients/assesments/GO_ASSESSMENT_REPORT.md` | `clients/gap-research/go-tier1-gaps.md` | `clients/gap-research/go-tier2-gaps.md` |
| java | java | Java | `clients/assesments/JAVA_ASSESSMENT_REPORT.md` | `clients/gap-research/java-tier1-gaps.md` | `clients/gap-research/java-tier2-gaps.md` |
| csharp | csharp | C# / .NET | `clients/assesments/C#-ASSESSMENT-REPORT.md` | `clients/gap-research/csharp-tier1-gaps.md` | `clients/gap-research/csharp-tier2-gaps.md` |
| python | python | Python | `clients/assesments/PYTHON_SDK_ASSESSMENT_REPORT.md` | `clients/gap-research/python-tier1-gaps.md` | `clients/gap-research/python-tier2-gaps.md` |
| js | js | JS/TS | `clients/assesments/JS-ASSESSMENT-REPORT.md` | `clients/gap-research/js-tier1-gaps.md` | `clients/gap-research/js-tier2-gaps.md` |

### Agent Prompts

- **Pass A agents:** Use TIER1-AGENT-PROMPT, substituting `{SDK_LABEL}`, `{SDK_NAME}`, `{ASSESSMENT_FILE}`, and `{OUTPUT_FILE}` = Pass A Output path from the table above.
- **Pass B agents:** Use TIER2-AGENT-PROMPT, substituting `{SDK_LABEL}`, `{SDK_NAME}`, `{ASSESSMENT_FILE}`, and `{OUTPUT_FILE}` = Pass B Output path from the table above.

**Validation gate:** After all agents complete, verify each output file exists and is at least 50 lines. If any agent produced empty or malformed output, re-launch that specific agent before proceeding to Step 4.

---

## Step 4: Merge Per-SDK Reports

After all agents complete, for each SDK, merge the Tier 1 and Tier 2 output files into a single combined report:

Output: `clients/gap-research/{sdk}-gap-research.md`

The merged file should:
1. Combine both Executive Summary tables into one (add a `Tier` column: `1` for categories 01-07, `2` for 08-13)
2. Concatenate all category sections (01-13)
3. Merge dependency graphs (include cross-tier dependencies)
4. Merge effort summaries
5. Produce a unified implementation sequence across all tiers

**For `all`:** Process one SDK merge at a time to manage context. If context is limited, read only the Executive Summary and Effort Summary from each tier file, then concatenate the category sections.

---

## Step 5: Expert Review — Round 1

After all merged reports are ready, launch **one reviewer agent per SDK** in parallel. Each reviewer is a subject-matter expert for that SDK's language ecosystem who deeply validates the gap research findings.

For `all`: Launch **5 reviewer agents** simultaneously.
For a single SDK: Launch **1 reviewer agent**.

### Reviewer Agent Configuration

| SDK | Reviewer Expertise | Input File | Review Output |
|-----|-------------------|------------|---------------|
| go | Senior Go SDK architect, expert in gRPC-Go, OTel-Go | `clients/gap-research/go-gap-research.md` | `clients/gap-research/go-review-r1.md` |
| java | Senior Java SDK architect, expert in gRPC-Java, OTel-Java, Maven | `clients/gap-research/java-gap-research.md` | `clients/gap-research/java-review-r1.md` |
| csharp | Senior .NET SDK architect, expert in gRPC-dotnet, OTel-dotnet, NuGet | `clients/gap-research/csharp-gap-research.md` | `clients/gap-research/csharp-review-r1.md` |
| python | Senior Python SDK architect, expert in grpcio, OTel-Python, PyPI | `clients/gap-research/python-gap-research.md` | `clients/gap-research/python-review-r1.md` |
| js | Senior JS/TS SDK architect, expert in grpc-js, OTel-JS, npm | `clients/gap-research/js-gap-research.md` | `clients/gap-research/js-review-r1.md` |

Use REVIEWER-AGENT-PROMPT (see below), substituting `{SDK_LABEL}`, `{SDK_NAME}`, `{GAP_RESEARCH_FILE}`, `{ASSESSMENT_FILE}`, `{REVIEW_OUTPUT_FILE}`, and `{ROUND}` = `1`. Use the ASSESSMENT_FILE from the Agent Launch Table in Step 3.

---

## Step 6: Apply Review Fixes — Round 1

After all Round 1 reviewer agents complete, launch **one fixer agent per SDK** in parallel. Each fixer reads the review feedback and updates the gap research report.

For `all`: Launch **5 fixer agents** simultaneously.
For a single SDK: Launch **1 fixer agent**.

Use FIXER-AGENT-PROMPT (see below), substituting:
- `{SDK_LABEL}`, `{SDK_NAME}`, `{GAP_RESEARCH_FILE}`, `{ASSESSMENT_FILE}`, `{REVIEW_FILE}` = `clients/gap-research/{SDK_NAME}-review-r1.md`, `{ROUND}` = `1`
- Use the ASSESSMENT_FILE from the Agent Launch Table in Step 3.

The fixer agent **edits the gap research file in place** — it does not create a new file.

---

## Step 7: Expert Review — Round 2

**CHECKPOINT:** Before launching Round 2 reviewers, verify ALL fixer agents from Step 6 have completed. Check that each `{SDK_NAME}-gap-research.md` has been modified and each `{SDK_NAME}-review-r1.md` contains a "Fixes Applied (Round 1)" section.

After all Round 1 fixes are confirmed applied, launch reviewer agents again for a second pass. Same configuration as Step 5 but:

- `{REVIEW_OUTPUT_FILE}` = `clients/gap-research/{SDK_NAME}-review-r2.md`
- `{ROUND}` = `2`
- The reviewer reads the **updated** gap research file (already fixed from Round 1)

All other variables (`{SDK_LABEL}`, `{SDK_NAME}`, `{GAP_RESEARCH_FILE}`, `{ASSESSMENT_FILE}`) remain the same as Step 5.

---

## Step 8: Apply Review Fixes — Round 2 (Final)

After all Round 2 reviewer agents complete, launch fixer agents one final time:

- `{REVIEW_FILE}` = `clients/gap-research/{SDK_NAME}-review-r2.md`
- `{ROUND}` = `2`
- All other variables same as Step 6.

This is the **last fixup pass**. After this step, the gap research reports are finalized.

---

## Step 9: Generate Summary

After all review-fix iterations are complete, create `clients/gap-research/GAP-RESEARCH-SUMMARY.md` using the SUMMARY-TEMPLATE below.

To avoid overloading context, read ONLY these sections from each gap report:
- Executive Summary (Gap Overview table, Critical Path, Quick Wins)
- Effort Summary table
- The compact status files (`{SDK_NAME}-tier1-status.md` and `{SDK_NAME}-tier2-status.md`) written by each analysis agent

Synthesize:
1. Cross-SDK gap heatmap (which gaps are universal vs SDK-specific)
2. Prioritized remediation order (which SDK to fix first for each category)
3. Shared work items (things that can be done once and ported)
4. Per-SDK effort estimates (S/M/L/XL per category)
5. Recommended implementation sequence

---

## Step 10: Report

Present to user:

    ## Gap Research Complete

    **Output Directory:** clients/gap-research/
    **Summary:** clients/gap-research/GAP-RESEARCH-SUMMARY.md
    **SDK Reports:** {count} generated
    **Review Rounds:** 2 (per SDK)
    **Total Gaps Found:** {count across all SDKs}

    ### Cross-SDK Highlights
    - {universal gaps}
    - {SDK-specific critical gaps}

    ### Review Impact
    - Round 1: {summary of changes made}
    - Round 2: {summary of changes made}

    ### Recommended Next Step
    Use the summary's recommended implementation order to decide which SDK to plan first.

---

## CATEGORY MAPPING: Assessment Reports → Golden Standard

**CRITICAL:** Assessment reports use DIFFERENT category numbering than the Golden Standard. Use this mapping table when cross-referencing:

| Assessment Cat # | Assessment Topic | Golden Standard Cat # | Golden Standard Topic | Tier |
|------------------|-----------------|----------------------|----------------------|------|
| 1 | API Completeness & Feature Parity | 08 | API Completeness | 2 |
| 2 | API Design & DX | 09 | API Design & DX | 2 |
| 3 | Connection & Transport | 02 | Connection & Transport | 1 |
| 4 | Error Handling & Resilience | 01 | Error Handling & Resilience | 1 |
| 5 | Auth & Security | 03 | Auth & Security | 1 |
| 6 | Concurrency & Thread Safety | 10 | Concurrency & Thread Safety | 2 |
| 7 | Observability | 05 | Observability | 1 |
| 8 | Code Quality & Architecture | 07 | Code Quality & Architecture | 1 |
| 9 | Testing | 04 | Testing | 1 |
| 10 | Documentation | 06 | Documentation | 1 |
| 11 | Packaging & Distribution | 11 | Packaging & Distribution | 2 |
| 12 | Compatibility, Lifecycle & Supply Chain | 12 | Compatibility & Lifecycle | 2 |
| 13 | Performance | 13 | Performance | 2 |

---

## TIER1-AGENT-PROMPT

<prompt-template id="tier1-agent">
You are a senior SDK architect conducting a comprehensive gap analysis for the KubeMQ {SDK_LABEL} SDK.

## Your Task

Compare the SDK's current state (from the assessment report) against every Tier 1 requirement in the Golden Standard (categories 01-07). For each requirement, determine its status and produce a detailed gap research document.

## Input Files — READ ALL OF THESE

1. **Assessment Report:** Read `{ASSESSMENT_FILE}` — this is the current state of the SDK
2. **Golden Standard Index:** Read `clients/sdk-golden-standard.md` — tier definitions and targets
3. **Tier 1 Category Specs (read all 7):**
   - `clients/golden-standard/01-error-handling.md`
   - `clients/golden-standard/02-connection-transport.md`
   - `clients/golden-standard/03-auth-security.md`
   - `clients/golden-standard/04-testing.md`
   - `clients/golden-standard/05-observability.md`
   - `clients/golden-standard/06-documentation.md`
   - `clients/golden-standard/07-code-quality.md`
4. **Known GS Inconsistencies (if exists):** Read `clients/golden-standard/KNOWN-INCONSISTENCIES.md` — pre-documented conflicts between GS specs. Use the listed resolutions instead of re-discovering these conflicts.
5. **Language Constraints (if exists):** Read `clients/skills/gap-close-spec/LANGUAGE-CONSTRAINTS-{SDK_NAME}.md` — language-specific pitfalls. Verify all remediation suggestions use valid {SDK_LABEL} syntax and patterns per these rules.

## CRITICAL: Category Mapping

The assessment report uses DIFFERENT category numbers than the Golden Standard. Use this mapping:

| Assessment Cat # | Assessment Topic | Maps to GS Cat # | GS Topic |
|---|---|---|---|
| 3 | Connection & Transport | 02 | Connection & Transport |
| 4 | Error Handling | 01 | Error Handling |
| 5 | Auth & Security | 03 | Auth & Security |
| 7 | Observability | 05 | Observability |
| 8 | Code Quality | 07 | Code Quality |
| 9 | Testing | 04 | Testing |
| 10 | Documentation | 06 | Documentation |

## CRITICAL: Assessment-to-REQ Matching

Assessment reports use their own criterion numbering (e.g., "4.1 Error types", "4.2 Retry support") that does NOT map 1:1 to Golden Standard REQ-* identifiers (e.g., REQ-ERR-1, REQ-ERR-2). You must match by **semantic content**, not by number:
- Assessment criterion "4.1 Error types" corresponds to REQ-ERR-1 "Typed Error Hierarchy"
- Assessment criterion "3.3 Keepalive" corresponds to REQ-CONN-3 "gRPC Keepalive Configuration"
- When no assessment criterion corresponds to a REQ-*, mark it **NOT_ASSESSED**

Some Golden Standard REQ-* items were added AFTER the assessment was conducted (via the adjudication process). These will have no assessment coverage. Mark them NOT_ASSESSED and flag them in the Executive Summary as "Unassessed Requirements (added post-assessment)" with a count.

## Score Usage

Use the assessment score as-is for the "Current" column in the Gap Overview table. Do NOT re-score against the Golden Standard rubric. The gap analysis at the REQ-* acceptance criterion level provides the real detail. The category-level score is for orientation only.

## Granularity Guidance

Assessment criteria (e.g., 4.1, 4.2) may cover multiple REQ-* items or only part of one. Use the assessment evidence (file:line references, score justifications) as data points, but evaluate each REQ-* acceptance criterion independently.

## Analysis Process

For EACH of the 7 Tier 1 categories, and for EACH requirement (REQ-*) within that category:

### A. Current State Assessment
- Find the corresponding section in the assessment report using the mapping table
- Extract: current score, specific findings, evidence cited (file:line references)
- Note what exists today vs what's missing

### B. Gap Identification
For each acceptance criterion in the requirement, assign ONE status:
- **COMPLIANT** — Assessment evidence confirms this criterion is met
- **PARTIAL** — Some aspects exist but incomplete (specify what's missing)
- **MISSING** — No evidence of this capability in the assessment
- **NOT_ASSESSED** — Assessment didn't evaluate this specific criterion (may be a post-assessment requirement)
- **EXCESS** — Feature exists but contradicts or exceeds the Golden Standard (e.g., REST transport when GS says gRPC-only). Document as a deprecation/removal candidate.

### C. Remediation Analysis
For each gap (PARTIAL, MISSING, or EXCESS):
- **What needs to change:** Specific code/feature that must be added/modified/removed
- **Complexity:** S (less than 1 day), M (1-3 days), L (3-5 days), XL (more than 5 days)
  - Effort estimates must account for {SDK_LABEL}-specific implementation complexity. The same requirement may be S in one language and M in another.
- **Dependencies:** Does this gap depend on fixing another gap first?
- **Risk:** What breaks if this isn't fixed? (production impact, user impact)
- **Language-specific notes:** Any {SDK_LABEL}-idiomatic considerations

### D. Priority Classification
Each gap gets a priority based on these rules (applied in order):
- **P0 — Blocker:** Any Tier 1 REQ-* with 3+ MISSING acceptance criteria, OR any Tier 1 category with assessment score below 3.0
- **P1 — Critical:** Any Tier 1 REQ-* with 1-2 MISSING acceptance criteria, OR any Tier 1 category with assessment score 3.0-3.5
- **P2 — Important:** Any Tier 1 REQ-* that is PARTIAL only (no MISSING criteria), OR any Tier 2 REQ-* with MISSING criteria
- **P3 — Nice-to-have:** PARTIAL items in Tier 2, documentation-only improvements, polish items

**NOT_ASSESSED priority rules:**
- NOT_ASSESSED criteria do NOT count as MISSING for P0/P1 threshold calculation. Only confirmed MISSING items count toward "3+ MISSING."
- Entire REQs that are NOT_ASSESSED get provisional priority **P1** (not P0) until fresh evaluation confirms their actual status.
- NOT_ASSESSED items MUST be listed separately in the Executive Summary under "Unassessed Requirements" — do NOT mix them into the P0 Critical Path.
- Effort estimates for NOT_ASSESSED items are provisional and MUST be prefixed with "~" (e.g., "~M (~2d)").

### E. Status Rollup Formula

Apply these formulas EXACTLY — do not use judgment for status aggregation:

**Criterion → REQ-level rollup:**
- If ANY criterion is **MISSING** → REQ status = **MISSING**
- If no MISSING but ANY criterion is **PARTIAL** → REQ status = **PARTIAL**
- If ALL criteria are **COMPLIANT** (or COMPLIANT + NOT_ASSESSED only) → REQ status = **COMPLIANT**
- NOT_ASSESSED criteria do NOT make a REQ MISSING. They are listed separately.

**REQ → Category-level rollup:**
- If ANY REQ is **MISSING** → Category status = **MISSING**
- If no MISSING but ANY REQ is **PARTIAL** → Category status = **PARTIAL**
- If ALL REQs are **COMPLIANT** → Category status = **COMPLIANT**
- NOT_ASSESSED REQs do NOT affect category rollup but must be noted.

## Output Files

Write TWO files. Create the `clients/gap-research/` directory if it doesn't exist.

### File 1: Full Report
Write to `{OUTPUT_FILE}` using the GAP-REPORT format below.

### File 2: Compact Status (for summary generation)
Write to `clients/gap-research/{SDK_NAME}-tier1-status.md` — a compact file with ONLY:
- The Gap Overview table (one row per category)
- The Effort Summary table
- List of all P0 items (one line each)
- List of all NOT_ASSESSED items with count

## GAP-REPORT FORMAT

--- BEGIN GAP-REPORT FORMAT ---

# {SDK_LABEL} SDK — Tier 1 Gap Research Report

**Assessment Score:** {weighted score} / 5.0
**Target Score:** 4.0+
**Gap:** +{delta}
**Assessment Date:** {date from report}
**Repository:** {repo URL from report}

---

## Executive Summary

### Gap Overview

| GS Category | Assessment Cat # | Current | Target | Gap | Status | Priority | Effort |
|-------------|-----------------|---------|--------|-----|--------|----------|--------|
| 01 Error Handling | 4 | {score} | 4.0 | {delta} | {worst status across REQs} | {highest priority} | {total effort} |
| 02 Connection | 3 | {score} | 4.0 | {delta} | ... | ... | ... |
| 03 Auth & Security | 5 | {score} | 4.0 | {delta} | ... | ... | ... |
| 04 Testing | 9 | {score} | 4.0 | {delta} | ... | ... | ... |
| 05 Observability | 7 | {score} | 4.0 | {delta} | ... | ... | ... |
| 06 Documentation | 10 | {score} | 4.0 | {delta} | ... | ... | ... |
| 07 Code Quality | 8 | {score} | 4.0 | {delta} | ... | ... | ... |

### Unassessed Requirements (added post-assessment)
{Count} requirements have no assessment coverage. These were added to the Golden Standard after the SDK assessment was conducted and require fresh evaluation:
- {REQ-ID}: {brief description}
- ...

### Critical Path (P0 items that must be fixed first)
1. {item with REQ reference}
2. {item}

### Quick Wins (high impact, low effort)
1. {item}
2. {item}

### Features to Remove or Deprecate
{List any EXCESS items that contradict the Golden Standard, e.g., REST transport, deprecated APIs}
- {item}: {reason for removal}

---

## Category 01: Error Handling & Resilience

**Current Score:** {score} (Assessment Cat 4) | **Target:** 4.0+ | **Gap:** {delta} | **Priority:** {P0-P3}

### REQ-ERR-1: Typed Error Hierarchy

**Status:** {COMPLIANT | PARTIAL | MISSING | NOT_ASSESSED}

**Current State:**
{What exists today, with file:line references from assessment. If NOT_ASSESSED: "No assessment coverage for this requirement. Added post-assessment."}

**Gap Analysis:**

| Acceptance Criterion | Status | Detail |
|---------------------|--------|--------|
| All methods return SDK-typed errors | {C/P/M/?} | {specific detail from assessment evidence} |
| Error types support unwrapping | {C/P/M/?} | {detail} |
| Error codes documented and stable | {C/P/M/?} | {detail} |
| RequestID field present | {C/P/M/?} | {detail} |

**Remediation:**
- **What:** {specific changes needed — be concrete, e.g., "Create errors.go with KubeMQError struct implementing error interface"}
- **Complexity:** {S/M/L/XL — calibrated for {SDK_LABEL}}
- **Dependencies:** {other REQs that must be done first, or "None"}
- **Language-specific:** {any {SDK_LABEL}-idiomatic notes, e.g., "Go: use errors.Is/As/Unwrap pattern"}

{Continue for ALL REQ-ERR-* requirements in this category...}

---

## Category 02: Connection & Transport

**Current Score:** {score} (Assessment Cat 3) | **Target:** 4.0+ | **Gap:** {delta} | **Priority:** {P0-P3}

{Same structure for all REQ-CONN-* requirements}

---

{Continue for ALL 7 Tier 1 categories: 01-07}

---

## Dependency Graph

List gaps that depend on other gaps being fixed first:

    REQ-CQ-1 (architecture) ──> REQ-ERR-1 (typed errors) ──> REQ-ERR-2 (classification) ──> REQ-ERR-3 (retry)
    REQ-CONN-1 (reconnection) ──> REQ-ERR-8 (streaming errors)
    REQ-CQ-1 (architecture) ──> REQ-OBS-1 (traces)
    REQ-ERR-2 (classification) ──> REQ-CONN-1 (reconnect buffer errors)
    REQ-AUTH-4 (credential provider) ──> REQ-AUTH-6 (cert reconnection)
    {... add all SDK-specific dependency chains ...}

---

## Implementation Sequence

Recommended order of implementation based on dependencies and priority:

### Phase 1: Foundation (must do first)
1. {item with REQ reference and complexity}
2. {item}

### Phase 2: Core Features
1. {item}
2. {item}

### Phase 3: Quality & Polish
1. {item}
2. {item}

---

## Effort Summary

**Counting methodology:** Each row counts individual remediation work items (one per distinct implementation task). This may differ from the REQ count or acceptance criteria count. A single REQ-* may produce multiple work items if its remediation involves independent tasks.

| Priority | Count | Effort Distribution |
|----------|-------|-------------------|
| P0 | {n} | {n}S + {n}M + {n}L + {n}XL |
| P1 | {n} | {n}S + {n}M + {n}L + {n}XL |
| P2 | {n} | {n}S + {n}M + {n}L + {n}XL |
| P3 | {n} | {n}S + {n}M + {n}L + {n}XL |
| **Total** | {n} | |

---

## Cross-Category Dependencies

| This Gap | Depends On | Reason |
|----------|-----------|--------|
| REQ-ERR-3 (retry) | REQ-ERR-2 (classification) | Retry logic needs to know which errors are retryable |
| REQ-OBS-1 (traces) | REQ-CQ-1 (architecture) | OTel instrumentation needs clear layer separation |
| {continue for all dependencies} | | |

---

## Preliminary Type Inventory

Types that will need to be created or modified based on remediation items. This inventory feeds into the gap-close-spec Type Registry to prevent cross-spec naming conflicts.

### Exception / Error Types

| # | Proposed Name | Category | Source REQ | Purpose | Breaking? |
|---|--------------|----------|-----------|---------|-----------|
| 1 | {name} | {cat} | {REQ-*} | {purpose} | {Yes/No} |

### Configuration Types

| # | Proposed Name | Category | Source REQ | Purpose |
|---|--------------|----------|-----------|---------|
| 1 | {name} | {cat} | {REQ-*} | {purpose} |

### Interface / Abstract Types

| # | Proposed Name | Category | Source REQ | Purpose |
|---|--------------|----------|-----------|---------|
| 1 | {name} | {cat} | {REQ-*} | {purpose} |

### Other New Types

| # | Proposed Name | Category | Source REQ | Purpose |
|---|--------------|----------|-----------|---------|
| 1 | {name} | {cat} | {REQ-*} | {purpose} |

**Note:** These are preliminary names. The gap-close-spec phase will verify them against the language's standard library for name collisions and finalize packages/signatures in the Type Registry.

---

## Shared Artifacts

Artifacts referenced by multiple categories. Ownership must be assigned during the spec phase to prevent duplicate definitions.

| # | Artifact | Referenced By | Suggested Owner | Notes |
|---|----------|-------------|-----------------|-------|
| 1 | CI/CD pipeline config | {REQ-TEST-4, REQ-CQ-3, ...} | 04-testing | {notes} |
| 2 | CHANGELOG | {REQ-PKG-1, REQ-DOC-3, ...} | 11-packaging | {notes} |
| 3 | Version constant / SDK_VERSION | {REQ-PKG-2, REQ-COMPAT-1, ...} | 11-packaging | {notes} |
| 4 | CONTRIBUTING.md | {REQ-DOC-5, REQ-CQ-7, ...} | 06-documentation | {notes} |
| {n} | {artifact} | {REQ references} | {suggested owning spec} | {notes} |

--- END GAP-REPORT FORMAT ---

## IMPORTANT RULES

1. **Be exhaustive.** Every REQ-* in every Tier 1 category spec must appear in the output. Do not skip requirements even if the SDK scores well.
2. **Use assessment evidence.** Cite specific file:line references from the assessment report. If the assessment didn't cover a criterion, mark it NOT_ASSESSED.
3. **Be concrete in remediation.** "Add error types" is too vague. "Create `errors.go` with `KubeMQError` struct implementing `error` interface, wrapping gRPC `status.Error` with fields: Code, Message, Operation, Channel, IsRetryable, Cause" is concrete.
4. **Dependency ordering matters.** Some gaps can't be fixed until others are done (e.g., retry needs error classification first). Map ALL dependencies.
5. **Language-idiomatic.** Remediation must respect {SDK_LABEL}'s conventions. Go uses functional options, Java uses builders, Python uses kwargs, C# uses options objects, JS/TS uses options literals.
6. **Don't invent current state.** If the assessment doesn't mention a feature, it's MISSING or NOT_ASSESSED. Don't assume things exist without evidence.
7. **Honest priority assignment.** Apply P0-P3 rules strictly. P0 applies when a Tier 1 CATEGORY has 3+ REQ-* items with MISSING status (not just acceptance criteria within a single REQ). A single REQ-* with 3+ MISSING acceptance criteria is also P0. VALIDATION: For each P0 category, verify at least one P0 REQ exists within it. A P2 REQ inside a P0 category is inconsistent — it should be at least P1 unless it has zero MISSING criteria.
8. **Research only, no code changes.** Read files, analyze, write the report. Do not modify any source code.
9. **Post-assessment requirements.** Requirements added after the assessment (via adjudication) will have no assessment coverage. Mark as NOT_ASSESSED with note "Added post-assessment. Requires fresh evaluation."
10. **EXCESS items.** If the SDK has features that contradict the Golden Standard (e.g., REST transport when GS says gRPC-only), mark as EXCESS and list in "Features to Remove or Deprecate."
11. **Language-weighted effort.** Effort estimates (S/M/L/XL) must account for {SDK_LABEL}-specific implementation complexity, not abstract/generic estimates.
12. **Evaluate ALL normative text.** Evaluate ALL normative requirements in each REQ-* section, including body text paragraphs, tables, and "Future Enhancement" sections — not just the acceptance criteria checklist. Requirements stated in body text are equally binding.
13. **Future Enhancement sections.** For each REQ-*, check for "Future Enhancement" subsections. These contain design-ahead items that SHOULD be noted in the remediation even if not implemented immediately (e.g., PartialFailureError, IdempotencyKey, Retry-After).
14. **Flag GS internal inconsistencies.** If you discover conflicting defaults, requirements, or acceptance criteria between two GS specs, flag the conflict explicitly in your output rather than silently choosing one value.
15. **REQ-level status consistency.** A REQ-* overall status must be the WORST status across its acceptance criteria. If any criterion is MISSING, the REQ cannot be COMPLIANT. If any is PARTIAL and none is MISSING, the REQ is PARTIAL.
16. **Flag breaking changes in remediation.** For each remediation, assess whether it is a breaking change (changes public API signatures, removes methods, renames packages/imports). Breaking changes must be flagged and deferred to the next major version per REQ-PKG-2. Adjust priority accordingly (breaking changes are typically P3 unless critical).
17. **Backward compatibility of remediation.** Remediation must consider backward compatibility. If a change would alter existing behavior for current users (e.g., changing default logger, changing default timeout), flag this and provide a migration path.
18. **Populate the Preliminary Type Inventory.** For every remediation that introduces a new type (exception, configuration class, interface), add it to the Preliminary Type Inventory section. This feeds the downstream gap-close-spec Type Registry and prevents naming conflicts.
19. **Identify shared artifacts.** When a remediation references an artifact (CI config, CHANGELOG, CONTRIBUTING.md, version constant) that could be touched by multiple categories, add it to the Shared Artifacts section with suggested ownership.
20. **Status rollup formula.** Apply the formula from Section E exactly. MISSING if ANY criterion is MISSING. PARTIAL if no MISSING but ANY is PARTIAL. COMPLIANT only if ALL are COMPLIANT/NOT_ASSESSED. Never use judgment for rollup — use the formula. Category-level status uses the worst REQ status. EDGE CASE: When language limitations make a criterion infeasible, use NOT_APPLICABLE (excluded from rollup) instead of MISSING. This prevents false P0 escalation for language-inherent gaps.
21. **NOT_ASSESSED priority handling.** NOT_ASSESSED items do NOT count toward P0/P1 thresholds. Assign provisional priority P1 until assessed. List separately. Prefix effort with "~".
22. **Summary table verification.** After writing all detailed sections, re-derive EVERY value in the Executive Summary, Effort Summary, and any "By Priority"/"By Tier" tables by counting from the detailed sections. Cross-check: (a) REQ counts per category match actual REQs written, (b) priority counts match actual priorities assigned, (c) effort totals are arithmetic sums of individual estimates, (d) calendar week estimates are consistent with effort totals. If any mismatch, fix the summary tables before finalizing. Specifically: (e) Executive Summary effort column must arithmetically match Effort Summary section for each category, (f) Priority counts in Critical Path must match priorities assigned in detailed sections, (g) calendar week estimates must be derivable from effort totals.
23. **Effort includes tests.** Every effort estimate MUST include test writing time. As a rule of thumb, add 30-50% to pure implementation time for test coverage. A 2-day implementation with tests is M (3 days), not M (2 days). State the test portion explicitly in the estimate. Multiplier guidance by type: Features with multiple error codes: +50%. State machines with transition tests: +40%. Integration tests requiring infrastructure: +50%. Simple utilities: +30%.
24. **Breaking change effort.** If remediation involves a breaking change, add 0.5-1 day for the deprecation layer, migration documentation, and backward compatibility shim. Mark this overhead explicitly: "M (2d impl + 1d breaking change mitigation)".
25. **Deduplication check.** Before finalizing, scan all remediation items for overlapping deliverables. If two REQ-* items produce the same artifact (e.g., CHANGELOG referenced by both REQ-PKG-4 and REQ-DOC-6), assign the effort to ONE REQ and mark the other as "effort counted under REQ-{X}, 0d additional". Never double-count effort for the same deliverable.
26. **Use known GS inconsistencies.** If `clients/golden-standard/KNOWN-INCONSISTENCIES.md` exists, use its listed resolutions for known conflicts instead of re-discovering them. If you find a NEW inconsistency not in the file, flag it prominently in the report and note it is newly discovered.
27. **Language constraint validation.** If a language constraints file exists, verify every remediation suggestion against it. Flag any remediation that uses invalid syntax, wrong API, or anti-patterns for the target language (e.g., Java import aliases, Python `sys.getsizeof()` for payload sizing, Go implicit constructors). Even without a constraints file, note modern language idioms relevant to remediation (e.g., current async patterns, error handling patterns, logging standards). The remediation should use the latest stable patterns for the minimum supported language version.
28. **GS completeness mapping.** Before finalizing, create a cross-reference: for each GS REQ-*, list each acceptance criterion and verify it is evaluated in the report. Mark each "accounted for." If any criterion is missing from the report, add it before finalizing.
29. **Category rollup table.** Every category section (ERR, CONN, AUTH, etc.) must end with a rollup table showing: REQ-* | Status | Priority | Effort. Verify rollup table statuses match individual REQ sections.
30. **Phase-level effort accounting.** When requirements from multiple tiers are sequenced into a single phase, calculate and display the phase total separately, including cross-tier prerequisites explicitly.
31. **Language qualifier handling.** When GS says "where language supports it" or similar, evaluate per-language capability. If the criterion is not feasible in the target language, mark NOT_APPLICABLE (not MISSING). Document the language limitation.
32. **Criterion granularity for compound items.** Acceptance criteria with multiple distinct sub-requirements must be broken into separate rows in the evaluation table. Never collapse 3+ sub-requirements into a single status row.
</prompt-template>

---

## TIER2-AGENT-PROMPT

<prompt-template id="tier2-agent">
You are a senior SDK architect conducting a comprehensive gap analysis for the KubeMQ {SDK_LABEL} SDK.

## Your Task

Compare the SDK's current state (from the assessment report) against every Tier 2 requirement in the Golden Standard (categories 08-13). For each requirement, determine its status and produce a detailed gap research document.

## Input Files — READ ALL OF THESE

1. **Assessment Report:** Read `{ASSESSMENT_FILE}` — this is the current state of the SDK
2. **Golden Standard Index:** Read `clients/sdk-golden-standard.md` — tier definitions and targets
3. **Tier 2 Category Specs (read all 6):**
   - `clients/golden-standard/08-api-completeness.md`
   - `clients/golden-standard/09-api-design-dx.md`
   - `clients/golden-standard/10-concurrency.md`
   - `clients/golden-standard/11-packaging.md`
   - `clients/golden-standard/12-compatibility-lifecycle.md`
   - `clients/golden-standard/13-performance.md`
4. **Known GS Inconsistencies (if exists):** Read `clients/golden-standard/KNOWN-INCONSISTENCIES.md` — pre-documented conflicts between GS specs. Use listed resolutions.
5. **Language Constraints (if exists):** Read `clients/skills/gap-close-spec/LANGUAGE-CONSTRAINTS-{SDK_NAME}.md` — verify remediation suggestions against language-specific rules.

## CRITICAL: Category Mapping

The assessment report uses DIFFERENT category numbers than the Golden Standard. Use this mapping:

| Assessment Cat # | Assessment Topic | Maps to GS Cat # | GS Topic |
|---|---|---|---|
| 1 | API Completeness & Feature Parity | 08 | API Completeness |
| 2 | API Design & DX | 09 | API Design & DX |
| 6 | Concurrency & Thread Safety | 10 | Concurrency |
| 11 | Packaging & Distribution | 11 | Packaging |
| 12 | Compatibility, Lifecycle & Supply Chain | 12 | Compatibility |
| 13 | Performance | 13 | Performance |

## CRITICAL: Assessment-to-REQ Matching

Assessment reports use their own criterion numbering (e.g., "1.1 Events Pub/Sub", "2.3 Consistent verbs") that does NOT map 1:1 to Golden Standard REQ-* identifiers (e.g., REQ-API-1, REQ-DX-3). You must match by **semantic content**, not by number:
- Assessment criterion "1.1 Events" corresponds to REQ-API-1 section on Events
- Assessment criterion "2.1 Idiomatic configuration" corresponds to REQ-DX-1
- When no assessment criterion corresponds to a REQ-*, mark it **NOT_ASSESSED**

Some Golden Standard REQ-* items were added AFTER the assessment was conducted (via the adjudication process). These will have no assessment coverage. Mark them NOT_ASSESSED and flag them as "Unassessed Requirements (added post-assessment)" with a count.

## Score Usage

Use the assessment score as-is for the "Current" column. Do NOT re-score. The REQ-* level analysis provides the real detail.

## Granularity Guidance

Assessment criteria (e.g., 1.1, 1.2) may cover multiple REQ-* items or only part of one. Use the assessment evidence (file:line references, score justifications) as data points, but evaluate each REQ-* acceptance criterion independently.

## Analysis Process

For EACH of the 6 Tier 2 categories, and for EACH requirement (REQ-*) within that category:

### A. Current State Assessment
- Find the corresponding section in the assessment report using the mapping table
- Extract: current score, specific findings, evidence cited (file:line references)
- Note what exists today vs what's missing

### B. Gap Identification
For each acceptance criterion, assign ONE status:
- **COMPLIANT** — Assessment evidence confirms criterion is met
- **PARTIAL** — Some aspects exist but incomplete (specify what's missing)
- **MISSING** — No evidence of this capability
- **NOT_ASSESSED** — Assessment didn't evaluate this criterion
- **EXCESS** — Feature exists but contradicts the Golden Standard

### C. Remediation Analysis
For each gap (PARTIAL, MISSING, or EXCESS):
- **What needs to change:** Specific code/feature that must be added/modified/removed
- **Complexity:** S (less than 1 day), M (1-3 days), L (3-5 days), XL (more than 5 days)
  - Effort estimates must account for {SDK_LABEL}-specific complexity.
- **Dependencies:** Does this gap depend on fixing another gap first?
- **Risk:** Impact if not fixed
- **Language-specific notes:** Any {SDK_LABEL}-idiomatic considerations

### D. Priority Classification
Each gap gets a priority:
- **P0 — Blocker:** NOT applicable for Tier 2 (Tier 2 categories are not gate blockers)
- **P1 — Critical:** NOT typical for Tier 2, but possible if a Tier 2 REQ-* is a prerequisite for a Tier 1 REQ-*
- **P2 — Important:** Any Tier 2 REQ-* with MISSING acceptance criteria
- **P3 — Nice-to-have:** PARTIAL items, documentation improvements, polish

**NOT_ASSESSED priority rules:** Same as Tier 1 — do NOT count toward priority thresholds. Assign provisional priority. Prefix effort with "~". List separately.

### E. Status Rollup Formula

Same formula as Tier 1 — apply EXACTLY:
- **Criterion → REQ:** MISSING if ANY criterion MISSING. PARTIAL if no MISSING but ANY PARTIAL. COMPLIANT only if ALL COMPLIANT/NOT_ASSESSED.
- **REQ → Category:** Use worst REQ status. NOT_ASSESSED REQs do not affect rollup.

## Output Files

Write TWO files. Create `clients/gap-research/` directory if it doesn't exist.

### File 1: Full Report
Write to `{OUTPUT_FILE}` using this structure:

    # {SDK_LABEL} SDK — Tier 2 Gap Research Report

    **Assessment Score:** {weighted score} / 5.0
    **Target Score:** 4.0+
    **Gap:** +{delta}

    ## Executive Summary

    ### Gap Overview

    | GS Category | Assessment Cat # | Current | Target | Gap | Status | Priority | Effort |
    |-------------|-----------------|---------|--------|-----|--------|----------|--------|
    | 08 API Completeness | 1 | {score} | 4.0 | {delta} | {worst status} | {priority} | {effort} |
    | 09 API Design & DX | 2 | ... | ... | ... | ... | ... | ... |
    | 10 Concurrency | 6 | ... | ... | ... | ... | ... | ... |
    | 11 Packaging | 11 | ... | ... | ... | ... | ... | ... |
    | 12 Compatibility | 12 | ... | ... | ... | ... | ... | ... |
    | 13 Performance | 13 | ... | ... | ... | ... | ... | ... |

    ### Unassessed Requirements / Critical Path / Quick Wins / Features to Remove
    {same sections as Tier 1 format}

    ## Category 08: API Completeness
    {same per-REQ structure as Tier 1: Current State, Gap Analysis table, Remediation}

    {... continue for all 6 Tier 2 categories ...}

    ## Dependency Graph / Implementation Sequence / Effort Summary / Cross-Category Dependencies
    {same structure as Tier 1 format}

    ## Preliminary Type Inventory
    {same structure as Tier 1 format — list all new types from Tier 2 remediation items}

    ## Shared Artifacts
    {same structure as Tier 1 format — list artifacts referenced by multiple Tier 2 categories}

### File 2: Compact Status
Write to `clients/gap-research/{SDK_NAME}-tier2-status.md` — compact file with:
- Gap Overview table (one row per category)
- Effort Summary table
- List of P2 items with MISSING acceptance criteria only (one line each — skip PARTIAL-only P2 items to keep compact)
- List of all NOT_ASSESSED items with count

## IMPORTANT RULES

1. **Be exhaustive.** Every REQ-* in every Tier 2 category spec must appear in the output.
2. **Use assessment evidence.** Cite file:line references. If assessment didn't cover a criterion, mark NOT_ASSESSED.
3. **Be concrete in remediation.** Specific files, types, methods — not vague descriptions.
4. **Dependency ordering.** Map all dependencies, including cross-tier (e.g., REQ-PERF-2 depends on REQ-CONN-6 from Tier 1).
5. **Language-idiomatic.** Remediation respects {SDK_LABEL}'s conventions.
6. **Don't invent current state.** No assessment evidence = MISSING or NOT_ASSESSED.
7. **Honest priority.** Tier 2 items are P2/P3 unless they are prerequisites for Tier 1 work. VALIDATION: For each P0 category, verify at least one P0 REQ exists within it. A P2 REQ inside a P0 category is inconsistent — it should be at least P1 unless it has zero MISSING criteria.
8. **Research only.** Do not modify source code.
9. **Post-assessment requirements.** Mark as NOT_ASSESSED with note.
10. **EXCESS items.** List features that contradict the Golden Standard.
11. **Language-weighted effort.** Calibrate S/M/L/XL for {SDK_LABEL} specifically.
12. **Evaluate ALL normative text.** Evaluate ALL normative requirements in each REQ-* section, including body text paragraphs, tables, and "Future Enhancement" sections — not just the acceptance criteria checklist. Requirements stated in body text are equally binding.
13. **Future Enhancement sections.** For each REQ-*, check for "Future Enhancement" subsections. These contain design-ahead items that SHOULD be noted in the remediation even if not implemented immediately.
14. **Flag GS internal inconsistencies.** If you discover conflicting defaults, requirements, or acceptance criteria between two GS specs, flag the conflict explicitly in your output rather than silently choosing one value.
15. **REQ-level status consistency.** A REQ-* overall status must be the WORST status across its acceptance criteria. If any criterion is MISSING, the REQ cannot be COMPLIANT. If any is PARTIAL and none is MISSING, the REQ is PARTIAL.
16. **Flag breaking changes in remediation.** For each remediation, assess whether it is a breaking change (changes public API signatures, removes methods, renames packages/imports). Breaking changes must be flagged and deferred to the next major version. Adjust priority accordingly.
17. **Backward compatibility of remediation.** Remediation must consider backward compatibility. If a change would alter existing behavior for current users, flag this and provide a migration path.
18. **Populate the Preliminary Type Inventory.** For every remediation that introduces a new type (exception, configuration class, interface), add it to the Preliminary Type Inventory section. This feeds the downstream gap-close-spec Type Registry.
19. **Identify shared artifacts.** When a remediation references an artifact that could be touched by multiple categories, add it to the Shared Artifacts section with suggested ownership.
20. **Status rollup formula.** MISSING if ANY criterion is MISSING. PARTIAL if no MISSING but ANY is PARTIAL. COMPLIANT only if ALL are COMPLIANT/NOT_ASSESSED. Category-level uses the worst REQ status. Apply this formula exactly — no judgment calls. EDGE CASE: When language limitations make a criterion infeasible, use NOT_APPLICABLE (excluded from rollup) instead of MISSING. This prevents false P0 escalation for language-inherent gaps.
21. **NOT_ASSESSED priority handling.** NOT_ASSESSED items do NOT count toward priority thresholds. Assign provisional priority based on confirmed statuses only. Prefix effort with "~".
22. **Summary table verification.** After writing all detailed sections, re-derive EVERY value in summary tables by counting from detailed sections. Cross-check REQ counts, priority counts, effort totals, and calendar estimates. Fix any mismatches. Specifically: (a) Executive Summary effort column must arithmetically match Effort Summary section for each category, (b) Priority counts in Critical Path must match priorities assigned in detailed sections, (c) calendar week estimates must be derivable from effort totals.
23. **Effort includes tests.** Add 30-50% to pure implementation time for test coverage. State the test portion explicitly. Multiplier guidance by type: Features with multiple error codes: +50%. State machines with transition tests: +40%. Integration tests requiring infrastructure: +50%. Simple utilities: +30%.
24. **Breaking change effort.** Add 0.5-1 day for deprecation layer, migration docs, and compat shim. Mark explicitly.
25. **Deduplication check.** Scan for overlapping deliverables across REQs. Assign effort to ONE and mark others as "effort counted under REQ-{X}, 0d additional".
26. **Use known GS inconsistencies.** Use pre-documented resolutions from `KNOWN-INCONSISTENCIES.md` if it exists. Flag newly discovered inconsistencies prominently.
27. **Language constraint validation.** Verify remediation suggestions against the language constraints file if it exists. Flag invalid syntax, wrong APIs, or anti-patterns. Even without a constraints file, note modern language idioms relevant to remediation (e.g., current async patterns, error handling patterns, logging standards). The remediation should use the latest stable patterns for the minimum supported language version.
28. **GS completeness mapping.** Before finalizing, create a cross-reference: for each GS REQ-*, list each acceptance criterion and verify it is evaluated in the report. Mark each "accounted for." If any criterion is missing from the report, add it before finalizing.
29. **Category rollup table.** Every category section (ERR, CONN, AUTH, etc.) must end with a rollup table showing: REQ-* | Status | Priority | Effort. Verify rollup table statuses match individual REQ sections.
30. **Phase-level effort accounting.** When requirements from multiple tiers are sequenced into a single phase, calculate and display the phase total separately, including cross-tier prerequisites explicitly.
31. **Language qualifier handling.** When GS says "where language supports it" or similar, evaluate per-language capability. If the criterion is not feasible in the target language, mark NOT_APPLICABLE (not MISSING). Document the language limitation.
32. **Criterion granularity for compound items.** Acceptance criteria with multiple distinct sub-requirements must be broken into separate rows in the evaluation table. Never collapse 3+ sub-requirements into a single status row.
</prompt-template>

---

## REVIEWER-AGENT-PROMPT

<prompt-template id="reviewer-agent">
You are an expert {SDK_LABEL} SDK architect and subject-matter expert conducting a deep review of a KubeMQ SDK gap research document. You have extensive experience building production {SDK_LABEL} SDKs, particularly those involving gRPC, messaging, and OpenTelemetry.

## Your Task

Deeply review the gap research report for the KubeMQ {SDK_LABEL} SDK. Validate every finding, identify errors, missing items, incorrect priorities, wrong effort estimates, and any language-specific issues the original analysis got wrong. This is review round {ROUND} of 2.

## Round-Specific Focus

If this is Round 1: Conduct a full review across all 6 dimensions below.

If this is Round 2: The gap research has already been through one review+fix cycle. Focus on:
1. Verifying that all Critical and Major issues from the Round 1 review were correctly resolved.
2. Any residual issues or regressions introduced by the Round 1 fixer.
3. Any new issues only visible after Round 1 corrections (e.g., cascading priority changes).
Do NOT re-raise issues that were fully addressed in Round 1. Read `clients/gap-research/{SDK_NAME}-review-r1.md` to see what was already flagged and fixed.

## Input Files — READ ALL OF THESE

1. **Gap Research Report:** Read `{GAP_RESEARCH_FILE}` — this is what you are reviewing
2. **Assessment Report:** Read `{ASSESSMENT_FILE}` — original assessment data to cross-check findings
3. **Golden Standard Index:** Read `clients/sdk-golden-standard.md` — tier definitions and targets
4. **Tier 1 Specs:** Read all 7 files in `clients/golden-standard/01-*.md` through `07-*.md`
5. **Tier 2 Specs:** Read all 6 files in `clients/golden-standard/08-*.md` through `13-*.md`
6. **Language Constraints (if exists):** Read `clients/skills/gap-close-spec/LANGUAGE-CONSTRAINTS-{SDK_NAME}.md` — language-specific pitfalls to validate remediation suggestions against
7. **Known GS Inconsistencies (if exists):** Read `clients/golden-standard/KNOWN-INCONSISTENCIES.md` — verify the report uses listed resolutions for known conflicts

## Review Dimensions

For each category section in the gap research report, evaluate:

### 1. Accuracy
- Are the status assignments (COMPLIANT/PARTIAL/MISSING/NOT_ASSESSED/EXCESS) correct?
- Does the cited assessment evidence actually support the assigned status?
- Are there assessment findings that were missed or misinterpreted?
- Are any COMPLIANT items actually PARTIAL or MISSING?
- Are any MISSING items actually PARTIAL (evidence exists but was overlooked)?

### 2. Completeness
- Are ALL REQ-* items from the Golden Standard covered?
- Are ALL acceptance criteria within each REQ-* evaluated?
- Are there post-assessment requirements that should be NOT_ASSESSED but were marked differently?
- Are dependency chains complete? Missing upstream/downstream links?

### 3. Language-Specific Correctness
- Do the remediation suggestions use correct {SDK_LABEL} idioms and patterns?
- Are library/framework references accurate and up-to-date?
- Are effort estimates realistic for {SDK_LABEL}? (e.g., Go error wrapping is simpler than Java exception hierarchies)
- Are there {SDK_LABEL}-specific approaches that would be significantly simpler or more complex than estimated?
- **Language constraints check:** If a language constraints file exists, verify that remediation suggestions don't violate any rules listed there (e.g., Java import aliases, Python async/sync duplication, Go goroutine leaks, C# ConfigureAwait, JS CommonJS/ESM).
- Do proposed type names in the Preliminary Type Inventory avoid standard library collisions?

### 4. Priority Validation
- Do priority assignments follow the P0-P3 rules strictly?
- Are there items classified as P2 that should be P0/P1 (Tier 1 with 3+ MISSING criteria)?
- Are there P0 items that are actually P1 (fewer MISSING criteria than claimed)?
- Are NOT_ASSESSED items correctly excluded from P0/P1 threshold counts?
- Are NOT_ASSESSED items listed separately (not mixed into Critical Path)?

### 5. Remediation Quality
- Are remediation descriptions concrete enough to implement?
- Are there better {SDK_LABEL}-idiomatic approaches not mentioned?
- Are dependency orderings correct (can't implement X before Y)?
- Are there quick wins that were missed?
- Are effort estimates properly calibrated for {SDK_LABEL}? (S/M/L/XL)
- Do effort estimates include test writing time (30-50% overhead)?
- Are breaking change items priced with deprecation/migration overhead?

### 6. Cross-Category Consistency
- Do findings in one category contradict findings in another?
- Are shared dependencies (e.g., error types needed by both retry and observability) consistently tracked?
- Does the implementation sequence respect all dependency chains?
- Are there duplicate effort allocations for the same deliverable across REQs?

### 7. Status Rollup & Summary Verification
- Does every REQ-level status correctly follow the rollup formula? (MISSING if ANY criterion MISSING, PARTIAL if ANY PARTIAL, COMPLIANT only if ALL COMPLIANT/NOT_ASSESSED)
- Does every category-level status use the worst REQ status?
- Are there cases where the same criterion pattern (e.g., 3 COMPLIANT + 1 MISSING) gets different statuses in different REQs?
- Do Executive Summary table values (counts, priorities, efforts) exactly match the detailed sections?
- Are effort totals arithmetic sums of individual estimates?
- Are "By Priority" / "By Tier" tables consistent with detailed data?
- Are compound acceptance criteria broken into separate rows?
- Are category rollup tables present and consistent with individual REQ statuses?

## Output

Write your review to `{REVIEW_OUTPUT_FILE}` using this format:

--- BEGIN REVIEW FORMAT ---

# {SDK_LABEL} SDK Gap Research — Expert Review (Round {ROUND})

**Reviewer Expertise:** Senior {SDK_LABEL} SDK Architect
**Document Reviewed:** {GAP_RESEARCH_FILE}
**Review Date:** {date}

---

## Review Summary

| Dimension | Issues Found | Severity Distribution |
|-----------|-------------|----------------------|
| Accuracy | {count} | {n} Critical, {n} Major, {n} Minor |
| Completeness | {count} | {n} Critical, {n} Major, {n} Minor |
| Language-Specific | {count} | {n} Critical, {n} Major, {n} Minor |
| Priority | {count} | {n} Critical, {n} Major, {n} Minor |
| Remediation Quality | {count} | {n} Critical, {n} Major, {n} Minor |
| Cross-Category | {count} | {n} Critical, {n} Major, {n} Minor |
| Status Rollup & Summary | {count} | {n} Critical, {n} Major, {n} Minor |
| **Total** | {count} | |

---

## Critical Issues (MUST FIX)

Issues that materially affect the correctness or usefulness of the gap research.

### C-{n}: {title}
**Category:** {GS category}
**Dimension:** {Accuracy/Completeness/etc.}
**Current in report:** {what the report says}
**Should be:** {what it should say}
**Evidence:** {why, with references to assessment or Golden Standard}

{repeat for all critical issues}

---

## Major Issues (SHOULD FIX)

Issues that reduce quality but don't invalidate findings.

### M-{n}: {title}
**Category:** {GS category}
**Dimension:** {dimension}
**Current:** {what report says}
**Recommended:** {what it should say}
**Rationale:** {why}

{repeat for all major issues}

---

## Minor Issues (NICE TO FIX)

Polish items, wording improvements, non-blocking suggestions.

### m-{n}: {title}
**Current:** {what report says}
**Suggested:** {improvement}

{repeat for all minor issues}

---

## Missing Items

Requirements or acceptance criteria not covered in the gap research:

| # | REQ-* | Acceptance Criterion | Should Be |
|---|-------|---------------------|-----------|
| 1 | {req} | {criterion} | {suggested status and remediation} |
| ... | | | |

---

## Effort Estimate Corrections

| REQ-* | Current Estimate | Corrected Estimate | Reason |
|-------|-----------------|-------------------|--------|
| {req} | {current S/M/L/XL} | {corrected} | {why, {SDK_LABEL}-specific reasoning} |
| ... | | | |

---

## Additional {SDK_LABEL}-Specific Recommendations

{Any language-specific insights, better approaches, ecosystem considerations not in the original report}

--- END REVIEW FORMAT ---

## IMPORTANT RULES

1. **Be thorough.** Check every single REQ-* in the gap research against the Golden Standard spec. Do not skim.
2. **Be specific.** "This seems wrong" is not useful. "REQ-ERR-2 is marked COMPLIANT but assessment criterion 4.2 scored 1/5 with no error classification found" is useful.
3. **Be fair.** If the gap research is correct, say so. Don't manufacture issues.
4. **Be constructive.** Every issue must include what it should be changed to.
5. **Language expertise.** Use your deep {SDK_LABEL} knowledge. If a Go remediation suggests Java patterns, flag it.
6. **Cross-check evidence.** Read the assessment report yourself. Verify that cited evidence actually says what the gap research claims.
7. **Research only.** Do not modify the gap research file. Only write the review output file.
8. **Severity discipline.** Critical = wrong status/priority that changes implementation plan. Major = incomplete/imprecise that reduces quality. Minor = wording/formatting/polish.
</prompt-template>

---

## FIXER-AGENT-PROMPT

<prompt-template id="fixer-agent">
You are a senior technical editor fixing a KubeMQ {SDK_LABEL} SDK gap research document based on expert review feedback.

## Your Task

Read the expert review and apply ALL Critical and Major fixes to the gap research report. Minor fixes are applied at your discretion (apply if trivial, skip if subjective). This is fix round {ROUND} of 2.

## Input Files — READ ALL OF THESE

1. **Gap Research Report:** Read `{GAP_RESEARCH_FILE}` — this is the file you will modify
2. **Expert Review:** Read `{REVIEW_FILE}` — the review feedback to apply
3. **Golden Standard specs** (as needed): `clients/golden-standard/*.md` — reference for any factual corrections
4. **Assessment Report:** `{ASSESSMENT_FILE}` — to verify corrected evidence citations

## Fix Process

### Phase 1: Triage
Read the entire review. Categorize each issue:
- **Critical issues (C-*):** MUST fix. These are factual errors in status, priority, or completeness.
- **Major issues (M-*):** SHOULD fix. These improve quality and accuracy.
- **Minor issues (m-*):** Apply if trivial (typos, formatting). Skip if subjective (wording preferences).
- **Missing items:** Add any requirements or criteria the review identified as absent.
- **Effort corrections:** Update effort estimates where the reviewer provides {SDK_LABEL}-specific reasoning.

### Phase 2: Apply Fixes
For each issue, edit the gap research file:
1. **Status corrections:** Change COMPLIANT/PARTIAL/MISSING/NOT_ASSESSED/EXCESS as directed
2. **Priority recalculations:** If statuses changed, recalculate priority using the P0-P3 rules
3. **Remediation updates:** Incorporate better {SDK_LABEL}-idiomatic approaches
4. **Missing items:** Add new REQ-* entries or acceptance criteria rows
5. **Effort estimate updates:** Adjust S/M/L/XL where the reviewer gave concrete reasoning
6. **Executive Summary:** Update the Gap Overview table if any category-level statuses or priorities changed
7. **Dependency graph:** Update if new dependencies were identified
8. **Implementation sequence:** Reorder if priorities or dependencies changed

### Phase 3: Reconcile
After all fixes:
1. Verify the Executive Summary tables match the detailed sections
2. Verify effort totals are recalculated
3. Verify P0/P1 counts in Critical Path section match
4. Update the compact status file (`clients/gap-research/{SDK_NAME}-tier1-status.md` and/or `clients/gap-research/{SDK_NAME}-tier2-status.md`) to reflect changes
5. Verify category rollup tables match individual REQ statuses after fixes
6. If phase-level effort was affected, recalculate phase totals including cross-tier prerequisites

## Output

Edit `{GAP_RESEARCH_FILE}` in place. Also update the compact status files if any category-level data changed.

Write a brief change log to the end of the review file (`{REVIEW_FILE}`) under a new section:

    ## Fixes Applied (Round {ROUND})

    | Issue | Status | Change Made |
    |-------|--------|-------------|
    | C-1 | FIXED | {brief description} |
    | C-2 | FIXED | {brief description} |
    | M-1 | FIXED | {brief description} |
    | M-3 | SKIPPED | {reason} |
    | m-2 | FIXED | {brief description} |
    | m-5 | SKIPPED | Subjective wording preference |

    **Total:** {n} fixed, {n} skipped

## IMPORTANT RULES

1. **Fix, don't rewrite.** Make targeted edits. Do not restructure the entire document.
2. **Critical issues are mandatory.** Every C-* must be fixed or explicitly justified as wrong.
3. **Preserve correct content.** Do not introduce new errors while fixing old ones.
4. **Evidence-based only.** Only change statuses if the reviewer provides concrete evidence (assessment citations, Golden Standard references).
5. **Recalculate cascading effects.** A status change may affect: priority, effort summary, executive summary, critical path, implementation sequence.
6. **Update compact status.** The status files feed the summary generator — they MUST reflect fixes.
7. **No gold-plating.** Do not add new analysis beyond what the review requested. Fix what's flagged, nothing more.
8. **Track your work.** Every fix or skip must appear in the change log appended to the review file.
9. **Cautionary notes over skipping.** When a reviewer flags weak evidence for a COMPLIANT/PARTIAL status but doesn't provide counter-evidence, add a cautionary note (e.g., "Audit recommended during implementation") rather than skipping entirely. Only skip when the reviewer's suggestion is purely subjective or out of scope.
</prompt-template>

---

## SUMMARY-TEMPLATE

Use this template for the cross-SDK summary file. Read ONLY the compact status files (`*-tier1-status.md`, `*-tier2-status.md`) and the Executive Summary sections from each full report to avoid context overflow.

<template id="summary">

# SDK Gap Research — Cross-SDK Summary

**Generated:** {date}
**SDKs Analyzed:** {list}

---

## Cross-SDK Gap Heatmap (by Category)

| GS Category | Tier | Go | Java | C# | Python | JS/TS | Universal? |
|-------------|------|-----|------|-----|--------|-------|------------|
| 01 Error Handling | 1 | {P0/P1/P2/P3/C} | ... | ... | ... | ... | {Yes/No} |
| 02 Connection | 1 | ... | ... | ... | ... | ... | ... |
| 03 Auth & Security | 1 | ... | ... | ... | ... | ... | ... |
| 04 Testing | 1 | ... | ... | ... | ... | ... | ... |
| 05 Observability | 1 | ... | ... | ... | ... | ... | ... |
| 06 Documentation | 1 | ... | ... | ... | ... | ... | ... |
| 07 Code Quality | 1 | ... | ... | ... | ... | ... | ... |
| 08 API Completeness | 2 | ... | ... | ... | ... | ... | ... |
| 09 API Design & DX | 2 | ... | ... | ... | ... | ... | ... |
| 10 Concurrency | 2 | ... | ... | ... | ... | ... | ... |
| 11 Packaging | 2 | ... | ... | ... | ... | ... | ... |
| 12 Compatibility | 2 | ... | ... | ... | ... | ... | ... |
| 13 Performance | 2 | ... | ... | ... | ... | ... | ... |

**Legend:** P0 = Blocker, P1 = Critical, P2 = Important, P3 = Nice-to-have, C = Compliant

---

## Cross-SDK Gap Heatmap (by REQ — Tier 1 only)

Focus on Tier 1 REQ-* items since these are gate blockers:

| Requirement | Go | Java | C# | Python | JS/TS | Universal? |
|------------|-----|------|-----|--------|-------|------------|
| REQ-ERR-1: Typed Errors | {C/P/M/?} | ... | ... | ... | ... | {Yes/No} |
| REQ-ERR-2: Classification | {C/P/M/?} | ... | ... | ... | ... | ... |
| {... all Tier 1 REQs ...} | | | | | | |

**Legend:** C = Compliant, P = Partial, M = Missing, ? = Not Assessed, X = Excess

---

## Universal Gaps (missing in ALL SDKs)

These can potentially be designed once and implemented across all SDKs:

| # | Requirement | Category | Priority | Shared Design? |
|---|------------|----------|----------|----------------|
| 1 | {req} | {cat} | {P0-P3} | {Yes — design pattern/type definitions can be shared / No — language-specific} |
| ... | | | | |

---

## Per-SDK Critical Path

### Go (Score: {score}, Gap: +{delta})
**P0 items:** {count}
{bulleted list}
**Unassessed requirements:** {count}
**Estimated total effort:** {breakdown}

### Java (Score: {score}, Gap: +{delta})
{same structure}

### C# (Score: {score}, Gap: +{delta})
{same structure}

### Python (Score: {score}, Gap: +{delta})
{same structure}

### JS/TS (Score: {score}, Gap: +{delta})
{same structure}

---

## Recommended Implementation Order

Based on the reference SDK strategy (Java first, then port):

1. **Java** — Highest current score (3.10), reference implementation. Patterns designed here are ported to others.
2. **{next}** — {reason based on gap analysis}
3. **{next}** — {reason}
4. **{next}** — {reason}
5. **{next}** — {reason}

---

## Shared Work Items

Work that can be done once and reused/ported across SDKs:

| # | Item | Reusable Artifact | Effort (once) |
|---|------|-------------------|---------------|
| 1 | Error type hierarchy design | Type definitions + classification table | M |
| 2 | OTel span naming conventions | Attribute constants file | S |
| 3 | Retry policy defaults | Configuration constants | S |
| 4 | gRPC error mapping table | Code mapping 17 gRPC codes | S |
| ... | | | |

---

## Category-Level Summary

### Tier 1 Categories (Gate Blockers)

| Category | Worst SDK | Best SDK | Universal Gaps | SDK-Specific Gaps | Total Gaps |
|----------|----------|---------|----------------|-------------------|------------|
| 01 Error Handling | {sdk}: {score} | {sdk}: {score} | {count} | {count} | {count} |
| 02 Connection | ... | ... | ... | ... | ... |
| 03 Auth & Security | ... | ... | ... | ... | ... |
| 04 Testing | ... | ... | ... | ... | ... |
| 05 Observability | ... | ... | ... | ... | ... |
| 06 Documentation | ... | ... | ... | ... | ... |
| 07 Code Quality | ... | ... | ... | ... | ... |

### Tier 2 Categories

| Category | Worst SDK | Best SDK | Universal Gaps | SDK-Specific Gaps | Total Gaps |
|----------|----------|---------|----------------|-------------------|------------|
| 08 API Completeness | ... | ... | ... | ... | ... |
| 09 API Design & DX | ... | ... | ... | ... | ... |
| 10 Concurrency | ... | ... | ... | ... | ... |
| 11 Packaging | ... | ... | ... | ... | ... |
| 12 Compatibility | ... | ... | ... | ... | ... |
| 13 Performance | ... | ... | ... | ... | ... |

---

## Effort Totals Across All SDKs

| SDK | P0 | P1 | P2 | P3 | Total Gaps | Unassessed |
|-----|----|----|----|----|------------|------------|
| Go | {n} | {n} | {n} | {n} | {n} | {n} |
| Java | {n} | {n} | {n} | {n} | {n} | {n} |
| C# | {n} | {n} | {n} | {n} | {n} | {n} |
| Python | {n} | {n} | {n} | {n} | {n} | {n} |
| JS/TS | {n} | {n} | {n} | {n} | {n} | {n} |
| **Total** | {n} | {n} | {n} | {n} | {n} | {n} |

</template>
