# Project Audit

## Overview

Perform a comprehensive project audit to identify refactoring and performance opportunities. Produce a Markdown document detailing findings and recommended remediation steps.

## Steps

1. **Explore the codebase**
   - Search for refactoring opportunities: bloat, mixed concerns, duplicated logic, oversized classes, domain logic coupled to Unity.
   - Search for performance opportunities: LINQ in hot paths, repeated `GetComponent`, per-frame allocations, uncached lookups, heavy operations in `Update`/`FixedUpdate`.

2. **Apply project rules**
   - Use `.cursor/rules/voxel-architecture.mdc` and `voxel-csharp-unity.mdc` as audit criteria.
   - Check alignment with: Core/Features structure, business logic separation, Unity 6.3 best practices.

3. **Produce audit report**
   - Create `docs/audit-report.md` (or `docs/audit/audit-report-YYYY-MM-DD.md` if dated).
   - Structure: Executive summary, Refactoring opportunities, Performance opportunities, Prioritized action plan.
   - For each issue: location, description, impact, recommended fix with concrete steps.

## Report Template

```markdown
# Project Audit Report

## Executive Summary
Brief overview of findings and priority areas.

## Refactoring Opportunities
| Location | Issue | Impact | Recommendation |
|----------|-------|--------|----------------|
| ... | ... | ... | ... |

## Performance Opportunities
| Location | Issue | Impact | Recommendation |
|----------|-------|--------|----------------|

## Prioritized Action Plan
1. [High] ...
2. [Medium] ...
3. [Low] ...
```

## Output

Write the audit report to the project root or `docs/` directory. Do not modify code unless explicitly asked—this command produces analysis only.
