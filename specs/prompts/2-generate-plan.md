# Prompt: Generate Implementation Plan

**Role**: Principal .NET Software Engineer and Systems Architect  
**Goal**: Transform an approved feature specification into a rigorous, actionable technical implementation plan and task checklist.

---

## Instructions for AI Assistant

You are tasked with generating a comprehensive implementation plan based on the provided feature specification.

### Governing Rules & Context
1. **Constitution Compliance**: You must strictly respect all architectural invariants in [specs/constitution.md](../constitution.md).
2. **Code Standards**: All proposed designs, class patterns, and namespaces must comply with [.github/copilot-instructions.md](../../.github/copilot-instructions.md).
3. **Idiomatic C#/.NET**: Use modern C# features (file-scoped namespaces, records, pattern matching, async conventions).
4. **Output Structure**: Your output must strictly follow the format of [specs/templates/plan-template.md](../templates/plan-template.md).

---

## Process

1. **Analyze Requirements**:
   - Read the feature specification completely.
   - Cross-check acceptance criteria and edge cases.
2. **Design Architecture**:
   - Select appropriate layers (Domain, Application, Infrastructure, API, or Library components).
   - Identify existing code that can be reused vs. new types to be created.
   - Plan for dependency injection, testability, and immutability.
3. **Draft File Changes**:
   - List explicit paths for all new, modified, or deleted files.
4. **Build Task Checklist**:
   - Break implementation into small, atomic, sequential tasks.
   - Sequence tasks logically: domain models/contracts first, business logic with unit tests second, integration/boundary wiring third, error handling fourth.
   - Each task must be independently testable.

---

## Deliverable

Generate a complete document formatted identically to `specs/templates/plan-template.md` so the user can save it directly as `specs/features/<feature-name>/plan.md`.
