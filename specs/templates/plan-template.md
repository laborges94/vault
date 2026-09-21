# Technical Plan: [Feature Name]

**Spec Reference**: `[Path to Spec file, e.g., specs/features/feature-name/spec.md]`  
**Status**: [Draft | Approved | In Progress | Completed]  
**Author(s)**: [Author Name]  
**Date**: [YYYY-MM-DD]  

---

## 1. Technical Overview & Architectural Approach

Provide a concise summary of the proposed solution. Explain how it integrates into the existing codebase, honoring the invariants in `specs/constitution.md` and coding styles in `.github/copilot-instructions.md`.

### 1.1 Architecture & Design Decisions
- **Decision 1**: [Description and rationale]
- **Decision 2**: [Description and rationale]

### 1.2 Dependencies & External Libraries
- [List any new NuGet packages, or state "None (standard library only)"]

---

## 2. File & Component Changes

Identify the files to be created, modified, or deleted across project layers.

| Action | Path | Description |
| :--- | :--- | :--- |
| `[NEW]` | `src/ProjectName/Folder/NewFile.cs` | Implements [component/class] |
| `[MODIFY]` | `src/ProjectName/Folder/ExistingFile.cs` | Extends [method/behavior] |
| `[NEW]` | `tests/ProjectName.Tests/Folder/NewFileTests.cs` | Unit tests for [component] |

---

## 3. Verification & Testing Strategy

- **Unit Tests**: [Detail which logic will be unit tested and expected scenarios]
- **Integration Tests**: [Detail boundary or database/API tests, if applicable]
- **Commands**:
  ```bash
  dotnet test --filter "FullyQualifiedName~FeatureName"
  ```

---

## 4. Implementation Tasks

Ordered, atomic task checklist. Each task should be independently verifiable, preferably following TDD (write/update test, implement, verify).

- [ ] **Task 1: Setup & Data Contracts**
  - [ ] Create domain entities or request/response records in `src/...`.
  - [ ] Ensure immutability and nullability annotations are applied.
- [ ] **Task 2: Core Domain / Service Logic**
  - [ ] Write unit tests for business logic in `tests/...`.
  - [ ] Implement core service/handler in `src/...`.
  - [ ] Run tests and ensure all pass (`dotnet test`).
- [ ] **Task 3: Integration / Boundary Layer**
  - [ ] Wire up dependency injection in composition root / `Program.cs` or service extensions.
  - [ ] Expose endpoints or public interfaces.
- [ ] **Task 4: Edge Cases & Error Handling**
  - [ ] Add tests for invalid inputs, cancellations, and boundary conditions.
  - [ ] Implement validation and structured error responses.
- [ ] **Task 5: Final Verification & Documentation**
  - [ ] Run full test suite (`dotnet test`).
  - [ ] Verify README or XML documentation if public APIs changed.
