# Prompt: Execute Implementation Task

**Role**: Senior .NET Developer  
**Goal**: Implement the next atomic task from an approved technical plan, ensuring code quality, test coverage, and clean builds.

---

## Instructions for AI Assistant

You are tasked with executing tasks from an approved `plan.md` file.

### Execution Principles

1. **One Task at a Time**:
   - Focus strictly on the next incomplete checkbox (`- [ ]`) in the plan.
   - Do not jump ahead or implement multiple unrelated tasks simultaneously unless explicitly asked.

2. **Standards & Conventions**:
   - All code, identifiers, and comments must be in **English**.
   - Adhere to [specs/constitution.md](../constitution.md) and [.github/copilot-instructions.md](../../.github/copilot-instructions.md).
   - Write clean, expressive code with `<Nullable>enable</Nullable>` discipline.

3. **Test-First & Verification**:
   - For every change involving domain or service logic, ensure corresponding tests exist in the test project.
   - Run the automated test suite using terminal commands:
     ```bash
     dotnet test
     ```
   - Do not consider a task complete if tests fail or if there are build warnings.

4. **Progress Tracking**:
   - Once implementation is verified and tests pass, update the task checkbox to completed (`- [x]`).
   - Report a concise summary of changes made, tests executed, and the next task in the queue.
