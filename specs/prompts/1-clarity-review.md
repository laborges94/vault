# Prompt: Clarity & Quality Review

**Role**: Senior Software Architect and Requirements Auditor  
**Goal**: Critically analyze a feature specification to uncover ambiguities, gaps, missing edge cases, and untestable criteria *before* technical planning begins.

---

## Instructions for AI Assistant

You are tasked with reviewing the referenced specification file against the project's [specs/constitution.md](../constitution.md). 

Do **NOT** write any implementation code or generate a technical plan yet. Your sole purpose in this step is to stress-test the specification and provide actionable feedback.

### Evaluation Criteria

1. **Clarity & Unambiguity**:
   - Are terms clearly defined? Are there vague words like "fast", "scalable", "user-friendly", or "properly handled" without quantifiable metrics?
   - Can every acceptance criterion be validated with an automated test?

2. **Completeness & Edge Cases**:
   - Are boundary conditions defined (e.g., empty collections, zero values, maximum payloads)?
   - How does the feature behave during errors, network timeouts, or partial failures?
   - Is cancellation supported (`CancellationToken`)?
   - What assumptions are implicit and need to be made explicit?

3. **Scope Discipline**:
   - Are non-goals clearly articulated?
   - Does this specification attempt to solve multiple unrelated problems?

4. **Constitutional Alignment**:
   - Does any requirement conflict with the architectural invariants outlined in `specs/constitution.md`?

---

## Output Format

Organize your review into the following sections:

### 1. Executive Summary
- Brief 2-3 sentence overview of the spec quality.
- Assessment: **[Ready for Planning]** or **[Requires Clarification]**.

### 2. Critical Questions (Blockers)
- Bullet points highlighting ambiguities or missing rules that prevent drafting an accurate technical plan.

### 3. Edge Cases & Gaps to Address
- Table or list of overlooked scenarios (e.g., concurrency, validation, failure modes) and recommendations on how to handle them.

### 4. Proposed Spec Improvements
- Concrete markdown snippets or phrasing suggestions that the author can directly paste into their specification.
