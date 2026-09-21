# Project Constitution

This document defines the non-negotiable architectural invariants, core quality standards, and development principles for this project. All technical plans, specifications, and code contributions must conform to this constitution.

---

## 1. Foundational Principles

- **Simplicity Over Cleverness**: Prefer clear, readable, and idiomatic C# code over complex, speculative abstractions.
- **Specification-Driven**: No non-trivial code should be written without a clear specification and an approved technical plan.
- **Zero Broken Windows**: Warnings should be treated as errors. The codebase must compile cleanly with `<Nullable>enable</Nullable>`.

---

## 2. Engineering & Code Standards

All implementations must strictly comply with the repository's coding conventions:

- **AI & Coding Instructions**: Follow all guidelines in [.github/copilot-instructions.md](../.github/copilot-instructions.md) and [AGENTS.md](../AGENTS.md).
- **Language**: Strictly **English** for all code, identifiers, comments, documentation, and commits.
- **C# & .NET**: Target modern .NET (C# 10+), utilizing file-scoped namespaces, pattern matching, records, and primary constructors where appropriate.
- **Formatting**: Adhere to [.editorconfig](../.editorconfig).
- **Git Commits**: Use [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`, etc.).

---

## 3. Architecture & Design Rules

1. **Dependency Inversion**: High-level modules must not depend on low-level details. Rely on interfaces and constructor injection.
2. **Immutability by Default**: Favor immutable data structures (`record`, `readonly struct`, `IReadOnlyList<T>`, init-only properties) for domain models and data transfer objects.
3. **Asynchronous Discipline**: Use `async`/`await` for I/O operations. Append `Async` to method names. Propagate `CancellationToken` through asynchronous call chains.
4. **Resilience & Defensive Design**: Validate inputs at boundary entry points (API endpoints, public library methods). Handle exceptions gracefully without swallowing them silently.

---

## 4. Testing & Verification Requirements

- **Test Coverage**: Business logic, edge cases, and bug fixes must have automated tests before merging.
- **Test Integrity**: Tests must be deterministic, isolated, and execute quickly (`dotnet test`).
- **TDD Encouraged**: When implementing from a plan, write tests to assert behavior before completing the implementation whenever practical.

---

## 5. Amendments

This constitution serves as a long-term contract. It should only be updated when making fundamental architectural shifts that affect the entire project lifecycle.
