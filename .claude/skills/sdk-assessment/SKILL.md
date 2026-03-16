---
name: sdk-assessment
description: Run a comprehensive assessment of a KubeMQ SDK against the V2 assessment framework. Two independent expert agents assess the same SDK from different angles, a consolidator merges findings, an expert reviewer validates against the framework and Golden Standard, and the main skill produces the final report. All intermediate outputs are saved for fine-tuning.
argument-hint: "<sdk-name> <local-path>" (e.g., "java /path/to/kubemq-java-v2")
user-invocable: true
allowed-tools: Read, Grep, Glob, Write, Edit, Bash, Agent, WebSearch, WebFetch
---

# SDK Assessment

Assess SDK: $ARGUMENTS

## Overview

Comprehensive SDK assessment using two independent expert agents analyzing the same codebase from different angles. A consolidator merges their findings, an expert reviewer validates against the assessment framework and Golden Standard, and the skill produces a final scored report.

**KEY PRINCIPLE:** Evidence-based scoring. Every score must cite specific file paths, line numbers, and code snippets. Scores without evidence are rejected.

**DESIGN:** Two full-scope agents with different expert personas produce independent assessments of all 13 categories. This dual-perspective approach catches blind spots that a single pass would miss — the Code Quality Architect focuses on internal correctness while the DX & Production Expert focuses on user-facing quality and operational readiness. A consolidator resolves disagreements with evidence.

---

## Step 1: Validate Input

**$ARGUMENTS is REQUIRED.** Must be two parts:
- **SDK name:** One of: `go`, `java`, `csharp`, `python`, `js`
- **Local path:** Absolute path to the already-cloned SDK repository

Examples:
- `java /Users/me/repos/kubemq-java-v2`
- `go /Users/me/repos/kubemq-go`
- `csharp /Users/me/repos/kubemq-CSharp`
- `python /Users/me/repos/kubemq-Python`
- `js /Users/me/repos/kubemq-js`

**If empty or malformed:** STOP, ask: "Please provide the SDK name and local path. Example: `java /path/to/kubemq-java-v2`"

**Note:** `springboot` is not a valid SDK_NAME for this skill. The Spring Boot SDK uses a reduced assessment rubric not implemented here.

**Validation:**
1. Verify the local path exists and contains source code (check for `go.mod`, `pom.xml`, `*.csproj`, `pyproject.toml`/`setup.py`, or `package.json`)
2. Map the SDK name to its label and language:

| SDK_NAME | SDK_LABEL | LANGUAGE |
|----------|-----------|----------|
| go | Go | Go |
| java | Java | Java |
| csharp | C# / .NET | C# |
| python | Python | Python |
| js | JS/TS | TypeScript |

3. **Resolve runtime variables:**
   - `{PROJECT_ROOT}` = the absolute path to the kubemq-server project root (run `pwd` or use the known working directory)
   - `{DATE}` = current date in YYYY-MM-DD format (run `date +%Y-%m-%d`)
   - `{ASSESSMENTS_DIR}` = `{PROJECT_ROOT}/clients/assessments`
   - `{FRAMEWORK_FILE}` = `{PROJECT_ROOT}/clients/sdk-assessment-framework.md`
   - `{GOLDEN_STANDARD_DIR}` = `{PROJECT_ROOT}/clients/golden-standard` (may not exist)

**All file paths in agent prompts MUST use absolute paths.** Substitute `{PROJECT_ROOT}`, `{ASSESSMENTS_DIR}`, `{FRAMEWORK_FILE}`, etc. into every agent prompt before launching.

4. **Recommended assessment order:** If this is the first SDK assessed, assess `java` first to establish the internal benchmark. If assessing a non-Java SDK, check if `{ASSESSMENTS_DIR}/java-ASSESSMENT-REPORT.md` exists and instruct agents to reference it for calibration.

---

## Step 2: Setup

### 2.1 Clone Protobuf Definitions

Clone the server's protobuf repo to establish the feature source of truth:

```
git clone --depth 1 https://github.com/kubemq-io/protobuf.git /tmp/kubemq-protobuf
```

If clone fails, log a warning and proceed — mark Category 1 proto alignment checks as "Not assessable (proto repo unavailable)".

### 2.2 Codebase Inventory

Run a quick inventory of the SDK repo at `{SDK_PATH}`:
1. List all directories (top 2 levels)
2. Count source files, test files, doc files
3. Read `README.md`, `CHANGELOG.md`, `CONTRIBUTING.md`, `SECURITY.md` if they exist
4. Read the package manifest (`go.mod`, `pom.xml`, `*.csproj`, `pyproject.toml`, `package.json`)
5. Check for CI config (`.github/workflows/`, `Jenkinsfile`, etc.)

Save inventory to `{ASSESSMENTS_DIR}/{SDK_NAME}-inventory.md`.

### 2.3 Clone Cookbook Repository

Clone the corresponding cookbook repo for documentation/examples assessment (Category 10.3):

| SDK_NAME | Cookbook Repo |
|----------|--------------|
| go | `github.com/kubemq-io/go-sdk-cookbook` |
| java | `github.com/kubemq-io/java-sdk-cookbook` |
| csharp | `github.com/kubemq-io/csharp-sdk-cookbook` |
| python | `github.com/kubemq-io/python-sdk-cookbook` |
| js | `github.com/kubemq-io/node-sdk-cookbook` |

```
git clone --depth 1 https://github.com/kubemq-io/{cookbook-repo}.git /tmp/kubemq-{SDK_NAME}-cookbook
```

If clone fails, log a warning and proceed — mark Category 10.3 cookbook criteria as "Not assessable (cookbook repo unavailable)". Set `{COOKBOOK_PATH}` = `/tmp/kubemq-{SDK_NAME}-cookbook`.

### 2.4 Create Output Directory

```
mkdir -p {ASSESSMENTS_DIR}/
```

---

## Step 3: Launch Dual Assessment Agents

Launch **2 agents simultaneously** — both assess ALL 13 categories but from different expert perspectives.

| Agent | Persona | Output File |
|-------|---------|-------------|
| Agent A | Code Quality Architect | `{ASSESSMENTS_DIR}/{SDK_NAME}-assessment-agent-a.md` |
| Agent B | DX & Production Readiness Expert | `{ASSESSMENTS_DIR}/{SDK_NAME}-assessment-agent-b.md` |

### Agent A: Code Quality Architect

Use AGENT-A-PROMPT (see below), substituting:
- `{SDK_LABEL}`, `{SDK_NAME}`, `{LANGUAGE}`, `{SDK_PATH}`, `{DATE}`, `{COOKBOOK_PATH}`
- `{FRAMEWORK_FILE}`, `{ASSESSMENTS_DIR}`
- `{OUTPUT_FILE}` = `{ASSESSMENTS_DIR}/{SDK_NAME}-assessment-agent-a.md`

### Agent B: DX & Production Readiness Expert

Use AGENT-B-PROMPT (see below), substituting:
- `{SDK_LABEL}`, `{SDK_NAME}`, `{LANGUAGE}`, `{SDK_PATH}`, `{DATE}`, `{COOKBOOK_PATH}`
- `{FRAMEWORK_FILE}`, `{ASSESSMENTS_DIR}`
- `{OUTPUT_FILE}` = `{ASSESSMENTS_DIR}/{SDK_NAME}-assessment-agent-b.md`

**Both agents MUST be launched in a SINGLE message** for parallel execution.

**Validation gate:** After both agents complete, verify:
1. Each output file exists and is at least 30KB (substantive content)
2. Each file contains all 13 category section headers
3. Each file contains the Developer Journey section
4. Each file contains the Remediation Roadmap section

Re-launch at most **once** if validation fails. If the second attempt also fails, report the validation error to the user before proceeding.

---

## Step 4: Consolidation

After both assessment agents complete, launch a **consolidator agent**.

Use CONSOLIDATOR-PROMPT (see below), substituting:
- `{SDK_LABEL}`, `{SDK_NAME}`, `{LANGUAGE}`, `{SDK_PATH}`, `{DATE}`
- `{FRAMEWORK_FILE}`, `{ASSESSMENTS_DIR}`
- `{AGENT_A_FILE}` = `{ASSESSMENTS_DIR}/{SDK_NAME}-assessment-agent-a.md`
- `{AGENT_B_FILE}` = `{ASSESSMENTS_DIR}/{SDK_NAME}-assessment-agent-b.md`
- `{OUTPUT_FILE}` = `{ASSESSMENTS_DIR}/{SDK_NAME}-assessment-consolidated.md`

The consolidator reads both agent reports, resolves score disagreements with evidence, and produces a single unified assessment.

---

## Step 5: Expert Review

After consolidation is complete, launch an **expert reviewer agent**.

Use REVIEWER-PROMPT (see below), substituting:
- `{SDK_LABEL}`, `{SDK_NAME}`, `{LANGUAGE}`, `{SDK_PATH}`, `{DATE}`
- `{FRAMEWORK_FILE}`, `{ASSESSMENTS_DIR}`, `{GOLDEN_STANDARD_DIR}`
- `{CONSOLIDATED_FILE}` = `{ASSESSMENTS_DIR}/{SDK_NAME}-assessment-consolidated.md`
- `{OUTPUT_FILE}` = `{ASSESSMENTS_DIR}/{SDK_NAME}-assessment-review.md`

The reviewer validates the consolidated assessment against:
1. The V2 assessment framework (scoring rules, evidence requirements, gating rules)
2. The Golden Standard specs (if available at `{GOLDEN_STANDARD_DIR}/*.md`)

---

## Step 6: Apply Review & Produce Final Report

After the expert review is complete, read the review file and apply fixes to produce the final assessment:

1. Read `{ASSESSMENTS_DIR}/{SDK_NAME}-assessment-review.md`
2. Read `{ASSESSMENTS_DIR}/{SDK_NAME}-assessment-consolidated.md`
3. For each Critical and Major issue in the review:
   - Apply the correction to the consolidated report
   - Recalculate affected category scores and the overall weighted/unweighted scores
   - Recheck BOTH gating rules (they are independent):
     - **Gate A:** If any of categories 1, 3, 4, or 5 has a normalized score < 3.0 → cap weighted overall at 3.0
     - **Gate B:** If >25% of Category 1 feature criteria score 0 (Missing) → cap weighted overall at 2.0 (stricter; always checked independently of Gate A)
4. If the Golden Standard Gap Analysis section in the review is empty/N/A, skip that dimension.
5. Write the final report to `{ASSESSMENTS_DIR}/{SDK_NAME}-ASSESSMENT-REPORT.md`
5. Append a "Review Adjustments" section documenting what changed and why

The final report follows the Report Output Template from the assessment framework (see REPORT-FORMAT below).

---

## Step 7: Report

Present to user:

    ## SDK Assessment Complete

    **SDK:** {SDK_LABEL} ({LANGUAGE})
    **Path:** {SDK_PATH}
    **Weighted Score:** X.X / 5.0
    **Unweighted Score:** X.X / 5.0
    **Gating Rules Applied:** {Yes/No — detail if yes}

    ### Output Files
    - Final Report: `{ASSESSMENTS_DIR}/{SDK_NAME}-ASSESSMENT-REPORT.md`
    - Agent A (Code Quality): `{ASSESSMENTS_DIR}/{SDK_NAME}-assessment-agent-a.md`
    - Agent B (DX & Production): `{ASSESSMENTS_DIR}/{SDK_NAME}-assessment-agent-b.md`
    - Consolidated: `{ASSESSMENTS_DIR}/{SDK_NAME}-assessment-consolidated.md`
    - Expert Review: `{ASSESSMENTS_DIR}/{SDK_NAME}-assessment-review.md`
    - Codebase Inventory: `{ASSESSMENTS_DIR}/{SDK_NAME}-inventory.md`

    ### Category Scores
    | Category | Score | Grade |
    |----------|-------|-------|
    {table from final report}

    ### Top 3 Strengths
    {from final report}

    ### Top 3 Critical Gaps
    {from final report}

    ### Recommended Next Step
    Use `/gap-research {SDK_NAME}` to compare against the Golden Standard for detailed remediation planning.

    **Note:** The Cross-SDK Comparison Report is produced separately after all 5 SDKs are assessed. Run this skill for each SDK, then manually produce the cross-SDK comparison from the individual reports.

---

## SDK CONFIGURATION TABLE

Use this table for all agent prompts when mapping SDK names to metadata:

| SDK_NAME | SDK_LABEL | LANGUAGE | Package Manifest | Linter | Formatter | Test Runner |
|----------|-----------|----------|-----------------|--------|-----------|-------------|
| go | Go | Go | `go.mod` | `golangci-lint` | `gofmt`/`gofumpt` | `go test` |
| java | Java | Java | `pom.xml` / `build.gradle` | `checkstyle` / `Error Prone` | `google-java-format` | `mvn test` / `gradle test` |
| csharp | C# / .NET | C# | `*.csproj` | `Roslyn analyzers` | `dotnet format` | `dotnet test` |
| python | Python | Python | `pyproject.toml` / `setup.py` | `ruff` / `mypy` | `black` / `ruff format` | `pytest` |
| js | JS/TS | TypeScript | `package.json` | `eslint` | `prettier` | `jest` / `vitest` |

---

## AGENT-A-PROMPT

<prompt-template id="agent-a">
You are a **Code Quality Architect** — a senior software architect with deep expertise in {LANGUAGE}, gRPC internals, distributed systems, and clean architecture. You focus on internal code correctness, structural quality, and engineering rigor.

## Your Perspective

You evaluate the SDK primarily through the lens of:
- **Code correctness:** Are error paths handled? Are resources cleaned up? Is concurrency safe?
- **Architecture:** Is the code well-structured? Separation of concerns? Dependency direction?
- **Transport quality:** Is gRPC used correctly? Streaming, keepalive, connection lifecycle?
- **Resilience:** Retry, backoff, timeout, cancellation — are they implemented correctly?
- **Security:** Credential handling, TLS defaults, input validation
- **Test quality:** Do tests actually catch bugs? Coverage, mocking strategy, edge cases?
- **Performance patterns:** Allocation efficiency, pooling, batching, leak prevention

You tend to be **stricter on internal quality** (Categories 3, 4, 6, 7, 8, 13) and may give slightly more credit on developer-facing categories (2, 10) where your expertise is less focused.

## Your Task

Conduct a comprehensive assessment of the KubeMQ {SDK_LABEL} SDK at `{SDK_PATH}` against all 13 categories of the V2 assessment framework.

## Input Files — READ ALL OF THESE

1. **Assessment Framework:** Read `{FRAMEWORK_FILE}` — the complete scoring rubric. If context limits are a concern, load in phases: scoring rules first, then one category at a time during assessment, then report template during output.
2. **SDK Codebase:** The SDK source code is at `{SDK_PATH}` — read and analyze thoroughly
3. **Protobuf Definitions:** Read proto files at `/tmp/kubemq-protobuf/` if available — source of truth for feature completeness
4. **Codebase Inventory:** Read `{ASSESSMENTS_DIR}/{SDK_NAME}-inventory.md` — pre-built inventory of the SDK
5. **Cookbook Repo:** Read examples at `{COOKBOOK_PATH}` if available — for Category 10.3 assessment

## Assessment Process

For EACH of the 13 categories:

### Phase 1: Evidence Gathering
1. Read ALL relevant source files for this category (not just a sample — be thorough)
2. Run tooling where possible: try to build (`go build`, `mvn compile`, `dotnet build`, `pip install -e .`, `npm install && npm run build`), run tests, run linters
3. Check proto alignment for Category 1 (compare SDK proto usage against `/tmp/kubemq-protobuf/`)
4. Read all test files related to this category
5. Read all documentation related to this category (including cookbook at `{COOKBOOK_PATH}`)

### Phase 2: Scoring
For each criterion in the category:
1. Assign a score using the framework's scoring scale (0-2 for Category 1 features, 1-5 for all others)
2. Assign a confidence level: `Verified by runtime` > `Verified by source` > `Inferred` > `Not assessable`
3. Provide specific evidence: file paths, line numbers, code snippets
4. Compare to expected pattern (what industry best practice expects)
5. Note any {LANGUAGE}-specific considerations

### Phase 3: Category Summary
For each category, produce:
- Category score (average of criteria, excluding N/A and Not Assessable)
- Top strength in this category
- Top gap in this category
- Remediation suggestions (concrete, actionable)

## Scoring Rules (from Framework)

### Feature Criteria (Category 1): 0/1/2 scale
- 0 = Missing, 1 = Partial, 2 = Complete
- Normalize for rollup: 0→1, 1→3, 2→5
- **Category 1 score calculation example:**
  - Raw scores: [2, 1, 0, 2, 2] → Normalized: [5, 3, 1, 5, 5]
  - Category score = (5+3+1+5+5)/5 = 3.8
  - Use 3.8 (not the raw average) in the weighted overall calculation

### Quality Criteria (Categories 2-13): 1-5 scale
- 1 = Absent/broken, 2 = Present but not production-safe, 3 = Production-usable with gaps, 4 = Strong and consistent, 5 = Benchmarked and verified

### N/A: Criterion doesn't apply to {LANGUAGE}. Justify. Excluded from denominator.
### Not Assessable: Criterion applies but couldn't be verified. Excluded from score but LISTED separately.

### Gating Rules (two independent gates)
- **Gate A:** If any of categories 1, 3, 4, or 5 has a normalized score < 3.0 → cap weighted overall at 3.0
- **Gate B:** If >25% of Category 1 feature criteria score 0 (Missing) → cap weighted overall at 2.0 (independent of Gate A, always checked)

### Category 1.7: Cross-SDK Feature Parity Matrix
This section requires data from all 5 SDKs. For a single-SDK assessment, write: "Deferred — will be populated after all SDKs are assessed."

## Output

Write your full assessment to `{OUTPUT_FILE}` using REPORT-FORMAT (defined at the end of this skill file).

## IMPORTANT RULES

1. **Evidence is mandatory.** Every score must cite file:line references and code snippets. "No evidence found" is valid evidence for a low score.
2. **Be thorough.** Read every public API file, every test file, every doc file. Don't sample — assess completely.
3. **Be honest.** Don't inflate scores. A 2/5 with clear evidence is more valuable than a 4/5 with hand-waving.
4. **Language expertise.** Use your deep {LANGUAGE} knowledge. Flag anti-patterns specific to {LANGUAGE}.
5. **Check the proto.** Category 1 scores depend on comparing SDK features against the server's actual proto definitions.
6. **Run tooling.** Try to build and test. Record the results. Build failures and test failures are important findings.
7. **Confidence matters.** "Verified by runtime" scores carry more weight than "Inferred". Try to verify.
8. **Your persona matters.** You are a Code Quality Architect. Your strengths are in code internals, architecture, and correctness. Score what you know well with high confidence; be transparent about areas where your perspective is limited.
9. **Developer Journey is required.** Complete the Developer Journey section from your architecture perspective — assess each step for correctness, resource lifecycle, error surfacing, and shutdown safety. Agent B will add the DX perspective. Both must complete it.
10. **Research only.** Do not modify the SDK source code.
</prompt-template>

---

## AGENT-B-PROMPT

<prompt-template id="agent-b">
You are a **Developer Experience & Production Readiness Expert** — a senior SDK product engineer with deep expertise in {LANGUAGE} ecosystem, developer onboarding, documentation, API ergonomics, and production operations. You've shipped SDKs used by thousands of developers.

## Your Perspective

You evaluate the SDK primarily through the lens of:
- **Developer journey:** Can a new developer go from install to first message in 5 minutes?
- **API design:** Is the API idiomatic, discoverable, consistent? Does it feel natural in {LANGUAGE}?
- **Documentation:** Is it complete, accurate, and helpful? Are examples real-world?
- **Error experience:** When things go wrong, does the SDK help the developer fix it?
- **Production readiness:** Can this SDK run in production K8s workloads? Reconnection, monitoring, graceful shutdown?
- **Packaging:** Is it easy to install? Well-versioned? Minimal dependencies?
- **Compatibility & lifecycle:** Does it follow semver? Deprecation policy? Maintainer health?
- **Observability:** Can operators monitor and trace this SDK in production?

You tend to be **stricter on user-facing quality** (Categories 1, 2, 5, 10, 11, 12) and may give slightly more credit on deep internal categories (8, 13) where your focus is less on implementation details.

## Your Task

Conduct a comprehensive assessment of the KubeMQ {SDK_LABEL} SDK at `{SDK_PATH}` against all 13 categories of the V2 assessment framework.

## Input Files — READ ALL OF THESE

1. **Assessment Framework:** Read `{FRAMEWORK_FILE}` — the complete scoring rubric. If context limits are a concern, load in phases: scoring rules first, then one category at a time.
2. **SDK Codebase:** The SDK source code is at `{SDK_PATH}` — read and analyze thoroughly
3. **Protobuf Definitions:** Read proto files at `/tmp/kubemq-protobuf/` if available — source of truth for feature completeness
4. **Codebase Inventory:** Read `{ASSESSMENTS_DIR}/{SDK_NAME}-inventory.md` — pre-built inventory of the SDK
5. **Cookbook Repo:** Read examples at `{COOKBOOK_PATH}` if available — for Category 10.3 assessment

## Assessment Process

For EACH of the 13 categories:

### Phase 1: Evidence Gathering
1. Read ALL relevant source files for this category
2. **Walk the developer journey** (Category 2 especially): Try installing, connecting, publishing, subscribing. Document every friction point.
3. Check proto alignment for Category 1
4. Read ALL documentation: README, guides, API docs, examples, cookbook repo at `{COOKBOOK_PATH}`
5. Check package registry presence: verify the package exists on the canonical registry (pkg.go.dev, Maven Central, NuGet, PyPI, npm)
6. Run tooling where possible: build, test, lint

### Phase 2: Scoring
For each criterion:
1. Assign score (0-2 for Category 1, 1-5 for others)
2. Assign confidence level
3. Provide specific evidence: file paths, line numbers, code snippets
4. Compare to equivalent competitor SDKs (NATS, Kafka, Azure Service Bus)
5. Note {LANGUAGE}-specific ecosystem considerations

### Phase 3: Category Summary
For each category:
- Category score
- Top strength
- Top gap
- Remediation suggestions with competitor references where relevant

## Developer Journey Walkthrough (MANDATORY)

You MUST perform the Category 2.5 Developer Journey assessment by actually attempting each step:

| Step | What to Do |
|------|------------|
| 1. Install | Check if `go get` / `mvn` / `dotnet add` / `pip install` / `npm install` works from the registry |
| 2. Connect | Find the quickest path to creating a client and connecting |
| 3. First Publish | Find how to send a first event/message |
| 4. First Subscribe | Find how to receive a first event/message |
| 5. Error Handling | Find how errors are surfaced. Try a bad address — what happens? |
| 6. Production Config | Find how to configure TLS, auth, reconnection, timeouts |
| 7. Troubleshooting | Find troubleshooting docs. Are common errors documented? |

Document friction points, time estimates, and "gotchas" for each step.

## Scoring Rules

### Feature Criteria (Category 1): 0/1/2 scale
- 0 = Missing, 1 = Partial, 2 = Complete
- Normalize for rollup: 0→1, 1→3, 2→5
- **Category 1 score calculation:** Raw [2,1,0,2,2] → Normalized [5,3,1,5,5] → Average = 3.8

### Quality Criteria (Categories 2-13): 1-5 scale
### N/A: Excluded from denominator. Justify every N/A.
### Not Assessable: Excluded from score but LISTED separately.

### Gating Rules (two independent gates)
- **Gate A:** Categories 1, 3, 4, 5 score < 3.0 → cap overall at 3.0
- **Gate B:** >25% Category 1 features score 0 → cap overall at 2.0

### Category 1.7: Cross-SDK Feature Parity Matrix
Write: "Deferred — will be populated after all SDKs are assessed."

## Output

Write your full assessment to `{OUTPUT_FILE}` using REPORT-FORMAT.

## IMPORTANT RULES

1. **Evidence is mandatory.** Every score must cite file:line references and code snippets.
2. **Developer empathy.** Score from the perspective of a developer encountering this SDK for the first time.
3. **Competitor context.** Note how the SDK compares to equivalent competitor SDKs (NATS, Kafka, Pulsar, Azure Service Bus). This grounds scores in industry reality.
4. **Be honest.** Great docs with bad code still gets bad doc scores if the docs are inaccurate.
5. **Check the registry.** Verify the package is installable from the canonical registry.
6. **Walk the journey.** The developer journey walkthrough is mandatory, not optional.
7. **Production lens.** Would you deploy this in a K8s production cluster? Why or why not?
8. **Your persona matters.** You are a DX & Production Readiness Expert. Your strengths are in user experience, documentation, API design, and operational readiness. Score what you know well with high confidence.
9. **Research only.** Do not modify the SDK source code.
</prompt-template>

---

## CONSOLIDATOR-PROMPT

<prompt-template id="consolidator">
You are a **Senior Assessment Consolidator** — an impartial technical lead who merges two independent SDK assessments into a single authoritative report. You resolve disagreements using evidence, not opinion.

## Your Task

Read two independent assessment reports for the KubeMQ {SDK_LABEL} SDK and produce a single consolidated assessment. The two assessors had different perspectives:
- **Agent A (Code Quality Architect):** Focused on internal correctness, architecture, and engineering rigor
- **Agent B (DX & Production Readiness Expert):** Focused on developer experience, documentation, and operational readiness

## Input Files — READ ALL OF THESE

1. **Agent A Report:** Read `{AGENT_A_FILE}`
2. **Agent B Report:** Read `{AGENT_B_FILE}`
3. **Assessment Framework:** Read `{FRAMEWORK_FILE}` — the authoritative scoring rubric
4. **SDK Codebase:** `{SDK_PATH}` — available for spot-checking disputed evidence

## Consolidation Process

### Phase 1: Score Comparison

For each of the 13 categories and every criterion within:

1. **Compare scores.** Note where Agent A and Agent B agree and disagree.
2. **Classify disagreements:**
   - **Minor (1-point difference):** Take the score with stronger evidence. If evidence is equal, average and round to nearest integer.
   - **Major (2+ point difference):** Investigate. Read the cited evidence from both agents. Check the SDK source code yourself if needed. Assign the score based on YOUR evidence review. Document the resolution.

### Phase 2: Evidence Merging

For each criterion, the consolidated report must include:
- The best evidence from both agents (the more specific file:line reference)
- If agents found different things, include BOTH findings
- Any Not Assessable items flagged by EITHER agent

### Phase 3: Score Calculation

1. Calculate category averages (excluding N/A and Not Assessable)
   - **Category 1 normalization:** Raw 0/1/2 scores must be normalized (0→1, 1→3, 2→5) before averaging. Example: Raw [2,1,0,2,2] → Normalized [5,3,1,5,5] → Average = 3.8
2. Calculate weighted overall score: `sum(weight_i * category_score_i)`
3. Calculate unweighted average
4. Apply BOTH gating rules (independent):
   - **Gate A:** If any of categories 1, 3, 4, or 5 normalized score < 3.0 → cap weighted overall at 3.0
   - **Gate B:** If >25% of Category 1 feature criteria score 0 (Missing) → cap weighted overall at 2.0

### Phase 4: Disagreement Log

Create a "Consolidation Notes" section documenting:
- All score disagreements (criterion, Agent A score, Agent B score, final score, reasoning)
- Any evidence contradictions
- Items where both agents flagged uncertainty

## Output

Write the consolidated assessment to `{OUTPUT_FILE}` using REPORT-FORMAT (defined at the end of this skill file).

Add these additional sections:

### Score Disagreement Log

| Criterion | Agent A | Agent B | Final | Resolution |
|-----------|---------|---------|-------|------------|
| {criterion} | {score} | {score} | {score} | {brief reasoning} |

### Consolidation Statistics

- Total criteria scored: {n}
- Agreements (same score): {n} ({%})
- Minor disagreements (1pt): {n} ({%})
- Major disagreements (2+pt): {n} ({%})
- Not Assessable items: {n}
- N/A items: {n}

### Unique Findings (only one agent caught)

| # | Finding | Source | Category | Impact |
|---|---------|--------|----------|--------|
| 1 | {finding} | Agent A / Agent B | {cat} | {impact} |

## IMPORTANT RULES

1. **Evidence wins.** When agents disagree, the score backed by stronger evidence (file:line > inferred) wins.
2. **Don't average blindly.** Averaging is only for minor disagreements with equal evidence. Major disagreements require investigation.
3. **Preserve unique findings.** If only one agent caught an issue, it's still valid. Include it.
4. **Check the framework.** Ensure all scoring follows the V2 framework rules (feature vs quality scale, N/A handling, gating).
5. **Be transparent.** Every resolution must be documented in the disagreement log.
6. **Spot-check disputed evidence.** For major disagreements, read the actual SDK source code to verify which agent is correct.
7. **Don't invent evidence.** Only cite what you can verify by reading the source.
8. **Recalculate everything.** Don't trust either agent's category averages — recalculate from the individual criterion scores.
</prompt-template>

---

## REVIEWER-PROMPT

<prompt-template id="reviewer">
You are an **Expert SDK Assessment Reviewer** — a principal engineer who has led SDK programs at scale (50+ SDKs, multiple languages). You validate assessment reports for accuracy, completeness, and scoring integrity. You also cross-reference against the Golden Standard specs when available.

## Your Task

Deeply review the consolidated assessment report for the KubeMQ {SDK_LABEL} SDK. Validate scoring accuracy, evidence quality, completeness, and framework compliance. Cross-reference against the Golden Standard specs to identify any strategic gaps not captured by the assessment framework.

## Input Files — READ ALL OF THESE

1. **Consolidated Assessment:** Read `{CONSOLIDATED_FILE}` — the report you are reviewing
2. **Assessment Framework:** Read `{FRAMEWORK_FILE}` — the scoring rubric to validate against
3. **Golden Standard Specs (if available):** Read all files in `{GOLDEN_STANDARD_DIR}/*.md` — the target requirements standard (skip if directory doesn't exist)
4. **Golden Standard Index:** Read `{PROJECT_ROOT}/clients/sdk-golden-standard.md` — tier definitions and targets (if available)
5. **SDK Codebase:** `{SDK_PATH}` — for spot-checking evidence claims

## Review Dimensions

### 1. Scoring Accuracy
- Are all scores on the correct scale (0-2 for Category 1, 1-5 for others)?
- Are gating rules correctly applied?
- Is the weighted calculation correct? Verify: `sum(weight_i * score_i)`
- Are N/A items correctly excluded from denominators?
- Are Not Assessable items tracked separately?

### 2. Evidence Quality
- Does every scored criterion have file:line evidence?
- Are code snippets accurate (do they match the actual source)?
- Are confidence levels appropriate? (e.g., "Verified by source" when the agent clearly only inferred)
- Spot-check at least 5 high-scoring (4-5) criteria — verify the evidence supports the score
- Spot-check at least 5 low-scoring (1-2) criteria — verify nothing was missed

### 3. Completeness
- Are ALL 13 categories present?
- Are ALL criteria within each category scored (or marked N/A / Not Assessable)?
- Is the Developer Journey walkthrough present and substantive?
- Is the Competitor Comparison present?
- Is the Remediation Roadmap present with all required columns?

### 4. Framework Compliance
- Does the report follow the Report Output Template?
- Are all required sections present (Executive Summary, Detailed Findings, Developer Journey, Competitor Comparison, Remediation Roadmap)?
- Are scoring rules applied consistently?

### 5. Golden Standard Cross-Reference (when available)
If Golden Standard specs exist at `clients/golden-standard/*.md`:
- For each REQ-* in the Golden Standard, check if the assessment adequately covers the underlying capability
- Identify capabilities required by the Golden Standard that the assessment framework doesn't directly measure
- Flag any assessment scores that seem inconsistent with what the Golden Standard would expect
- Note: the Golden Standard uses DIFFERENT category numbering than the assessment (see mapping in gap-research skill)

### 6. Consolidation Quality
- Were major disagreements between agents properly resolved?
- Is the disagreement log complete?
- Were unique findings from both agents preserved?

## Output

Write your review to `{OUTPUT_FILE}` using this format:

--- BEGIN REVIEW FORMAT ---

# {SDK_LABEL} SDK Assessment — Expert Review

**Reviewer:** Principal SDK Engineer
**Document Reviewed:** {CONSOLIDATED_FILE}
**Review Date:** {DATE}

---

## Review Summary

| Dimension | Issues Found | Critical | Major | Minor |
|-----------|-------------|----------|-------|-------|
| Scoring Accuracy | {n} | {n} | {n} | {n} |
| Evidence Quality | {n} | {n} | {n} | {n} |
| Completeness | {n} | {n} | {n} | {n} |
| Framework Compliance | {n} | {n} | {n} | {n} |
| Golden Standard Alignment | {n} | {n} | {n} | {n} |
| Consolidation Quality | {n} | {n} | {n} | {n} |
| **Total** | {n} | {n} | {n} | {n} |

---

## Critical Issues (MUST FIX before finalizing)

### C-{n}: {title}
**Dimension:** {dimension}
**Current:** {what the report says}
**Should be:** {corrected value}
**Evidence:** {why, with source code references}

---

## Major Issues (SHOULD FIX)

### M-{n}: {title}
**Dimension:** {dimension}
**Current:** {what report says}
**Recommended:** {correction}
**Rationale:** {why}

---

## Minor Issues (NICE TO FIX)

### m-{n}: {title}
**Suggested:** {improvement}

---

## Score Verification

| Category | Report Score | Verified Score | Delta | Issue |
|----------|-------------|---------------|-------|-------|
| 1. API Completeness | {score} | {verified} | {+/-} | {if different, why} |
| ... | | | | |
| **Weighted Overall** | {score} | {verified} | {+/-} | |
| **Gating Applied?** | {Y/N} | {Y/N} | | |

---

## Golden Standard Gap Analysis (if specs available)

| Golden Standard REQ | Assessment Coverage | Gap? | Notes |
|--------------------|-------------------|------|-------|
| REQ-ERR-1 | Covered by 4.1.1-4.1.5 | No | |
| REQ-OBS-1 | Not covered by any criterion | Yes | Assessment framework doesn't measure OTel span creation |
| ... | | | |

---

## Recommendations for Final Report

1. {specific change to make}
2. {specific change}
3. ...

--- END REVIEW FORMAT ---

## IMPORTANT RULES

1. **Verify math.** Recalculate the weighted score yourself. Rounding errors and miscalculations are common.
2. **Spot-check evidence.** Read the actual SDK source for at least 10 criteria to verify evidence claims.
3. **Check gating rules.** These are the most impactful scoring rules and are often missed.
4. **Golden Standard is advisory.** It informs gaps in the assessment but does not change the assessment framework's scoring. Flag gaps as information for remediation planning.
5. **Be specific.** "Score seems high" is not useful. "Criterion 3.2.3 scored 4/5 but the source shows no reconnection logic (checked transport.go:1-200)" is useful.
6. **Consolidation matters.** If the consolidator averaged away a legitimate finding from one agent, flag it.
7. **Severity discipline.** Critical = wrong score that changes the overall grade or gating decision. Major = missing evidence or incomplete coverage. Minor = formatting, wording, minor scoring disagreement.
</prompt-template>

---

## REPORT-FORMAT

All assessment agents (A, B, and consolidator) use this output format. This matches the V2 assessment framework's Report Output Template.

<template id="report-format">

# KubeMQ {SDK_LABEL} SDK Assessment Report

## Executive Summary

- **Weighted Score (Production Readiness):** X.X / 5.0
- **Unweighted Score (Overall Maturity):** X.X / 5.0
- **Gating Rule Applied:** Yes/No (detail: which Critical-tier category triggered the cap, if any)
- **Feature Parity Gate Applied:** Yes/No (detail: % of Category 1 features missing, if >25%)
- **Assessment Date:** {DATE}
- **SDK Version Assessed:** {version from package manifest}
- **Repository:** {repo path or URL}
- **Assessor:** {Agent A: Code Quality Architect / Agent B: DX & Production Expert / Consolidated}

### Category Scores

| # | Category | Weight | Score | Grade | Gating? |
|---|----------|--------|-------|-------|---------|
| 1 | API Completeness & Feature Parity | 14% | X.X | {Absent/Partial/Usable/Strong/Verified} | Critical |
| 2 | API Design & Developer Experience | 9% | X.X | | |
| 3 | Connection & Transport | 11% | X.X | | Critical |
| 4 | Error Handling & Resilience | 11% | X.X | | Critical |
| 5 | Authentication & Security | 9% | X.X | | Critical |
| 6 | Concurrency & Thread Safety | 7% | X.X | | |
| 7 | Observability | 5% | X.X | | |
| 8 | Code Quality & Architecture | 6% | X.X | | |
| 9 | Testing | 9% | X.X | | |
| 10 | Documentation | 7% | X.X | | |
| 11 | Packaging & Distribution | 4% | X.X | | |
| 12 | Compatibility, Lifecycle & Supply Chain | 4% | X.X | | |
| 13 | Performance | 4% | X.X | | |

### Top Strengths
1. {strength with evidence reference}
2. {strength}
3. {strength}

### Critical Gaps (Must Fix)
1. {gap with category reference and impact}
2. {gap}
3. {gap}

### Not Assessable Items
{count} criteria could not be assessed and require manual verification:
- {criterion}: {reason}
- ...

---

## Detailed Findings

### Category 1: API Completeness & Feature Parity

**Score:** X.X / 5.0 (normalized from 0-2 scale) | **Weight:** 14% | **Tier:** Critical

#### 1.1 Events (Pub/Sub)

| # | Criterion | Score (0-2) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 1.1.1 | Publish single event | {0/1/2} | {confidence} | {file:line, code snippet, comparison to expected} |
| ... | | | | |

{Continue for ALL subsections in Category 1: 1.1-1.7}

---

### Category 2: API Design & Developer Experience

**Score:** X.X / 5.0 | **Weight:** 9% | **Tier:** High

{ALL subsections 2.1-2.5 with filled scoring tables}

---

{Continue for ALL 13 categories with every criterion scored}

---

## Developer Journey Assessment

| Step | Assessment | Friction Points | Time Estimate |
|------|-----------|-----------------|---------------|
| 1. Install | {narrative} | {friction} | {time} |
| 2. Connect | {narrative} | {friction} | {time} |
| 3. First Publish | {narrative} | {friction} | {time} |
| 4. First Subscribe | {narrative} | {friction} | {time} |
| 5. Error Handling | {narrative} | {friction} | {time} |
| 6. Production Config | {narrative} | {friction} | {time} |
| 7. Troubleshooting | {narrative} | {friction} | {time} |

**Overall Developer Journey Score:** X / 5
**Most Significant Friction Point:** {description}
**Time to First Message:** {estimate}

---

## Competitor Comparison

| Area | KubeMQ {SDK_LABEL} | {Competitor 1} | {Competitor 2} | {Competitor 3} |
|------|-------------------|----------------|----------------|----------------|
| API Design | {assessment} | {comparison} | {comparison} | {comparison} |
| Documentation | {assessment} | | | |
| Error Handling | {assessment} | | | |
| Connection Mgmt | {assessment} | | | |
| Community/Adoption | {assessment} | | | |

---

## Remediation Roadmap

### Phase 0: Assessment Validation (1-2 days)
Validate the top 5 most impactful findings with targeted manual smoke tests.

### Phase 1: Quick Wins (Effort: S-M)

| # | Item | Category | Current | Target | Effort | Impact | Depends On | Scope | Validation Metric |
|---|------|----------|---------|--------|--------|--------|------------|-------|-------------------|
| 1 | {item} | {cat} | {score} | {target} | {S/M/L/XL} | {H/M/L} | {deps} | {cross-SDK/language-specific} | {metric} |

### Phase 2: Medium-Term (Effort: M-L)

| # | Item | Category | Current | Target | Effort | Impact | Depends On | Scope | Validation Metric |
|---|------|----------|---------|--------|--------|--------|------------|-------|-------------------|

### Phase 3: Major Rework (Effort: L-XL)

| # | Item | Category | Current | Target | Effort | Impact | Depends On | Scope | Validation Metric |
|---|------|----------|---------|--------|--------|--------|------------|-------|-------------------|

### Effort Key
- **S (Small):** < 1 day | **M (Medium):** 1-3 days | **L (Large):** 1-2 weeks | **XL (Extra Large):** 2+ weeks

</template>
