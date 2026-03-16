# Golden Standard — Known Internal Inconsistencies

**Purpose:** Pre-documented conflicts between GS specs. Gap research agents MUST use the listed resolutions instead of re-discovering these conflicts independently.

**Last Updated:** 2026-03-10
**Source:** Discovered during Java SDK and Python SDK gap research runs (R1/R2 reviews).

---

## How to Use

- **Gap research agents:** When you encounter one of these conflicts, use the **Resolution** column. Do NOT flag it as a newly discovered inconsistency — it is already known.
- **Reviewers:** Verify the gap research uses the correct resolution from this file. Flag as Critical if the report handles a known inconsistency incorrectly.
- **New discoveries:** If you find a conflict NOT listed here, flag it prominently in the report with "NEW GS INCONSISTENCY" and add it to the Open Questions section.

---

## Known Conflicts

| # | Spec A | Spec B | Conflict | Resolution | Discovered |
|---|--------|--------|----------|------------|------------|
| 1 | 02 REQ-CONN-1: "Connection drops detected within keepalive timeout (default 20s)" | 02 REQ-CONN-3: keepalive_time=10s, keepalive_timeout=5s → computed 15s | Keepalive detection timeout: 20s stated vs 15s computed | **Use 15s** (computed from REQ-CONN-3 values). Treat "20s" in REQ-CONN-1 as a rounding/typo. Note the discrepancy in the report but do not flag as a gap. | Java R1 C-2 |
| 2 | 02 REQ-CONN-4: "Optional timeout for maximum drain duration (default: 5s)" | 10 REQ-CONC-5: "configurable timeout (default 30 seconds)" | Shutdown timeout: 5s drain vs 30s callback completion | **These are separate timeouts.** Drain timeout (REQ-CONN-4) = 5s for flushing in-flight messages. Callback completion timeout (REQ-CONC-5) = 30s for waiting on active callbacks to finish. Implement both with distinct configuration keys. | Java R1 C-4 |
| 3 | 01 REQ-ERR-4: "Default timeout: 5 seconds" | Various SDK implementations using 30s default | Send timeout default value | **Use 5s** per GS. If SDK currently uses 30s, flag as breaking change requiring migration path (e.g., `legacy_timeout_mode` flag). | Python R1 M-4 |

---

## Conflict Resolution Principles

When encountering a NEW inconsistency not listed above:

1. **More specific spec wins.** If one spec defines a general principle and another defines a specific value for the same concept, use the specific value.
2. **Tier 1 wins over Tier 2.** If a Tier 1 spec and Tier 2 spec conflict on the same topic, the Tier 1 spec takes precedence (it was finalized first).
3. **Dedicated requirement wins.** If REQ-X is specifically about topic T, and REQ-Y mentions topic T as a secondary concern, REQ-X is authoritative for T.
4. **Flag, don't choose silently.** Even when applying these principles, always note the conflict in the report under "GS Inconsistencies Found."
