# Spec-Driven Development (SDD) Workflow

This directory contains a lightweight, AI-assisted **Spec-Driven Development** framework. It provides a structured lifecycle for defining, refining, planning, and implementing features with AI coding assistants (such as Antigravity, Cursor, GitHub Copilot, and Claude Code).

---

## 🧭 The SDD Lifecycle

```text
  ┌─────────────────┐       ┌─────────────────┐       ┌─────────────────┐       ┌─────────────────┐
  │   1. Specify    │ ───►  │   2. Review     │ ───►  │    3. Plan      │ ───►  │   4. Execute    │
  │ (spec-template) │       │(clarity-review) │       │ (generate-plan) │       │ (execute-task)  │
  └─────────────────┘       └─────────────────┘       └─────────────────┘       └─────────────────┘
```

1. **Specify**: Define the problem and business requirements using `templates/spec-template.md`.
2. **Review**: Use `prompts/1-clarity-review.md` to prompt the AI to find gaps, edge cases, and ambiguities.
3. **Plan**: Use `prompts/2-generate-plan.md` to have the AI produce a concrete architectural design and task checklist using `templates/plan-template.md`.
4. **Execute**: Use `prompts/3-execute-task.md` to guide the AI to implement tasks incrementally with automated test verification.

---

## 📁 Directory Structure

```text
specs/
├── constitution.md           # Project invariants and non-negotiable architectural rules
├── README.md                 # This guide
├── templates/
│   ├── spec-template.md      # Functional specification template (Requirements, Edge Cases)
│   └── plan-template.md      # Technical plan template (Architecture, Files, Tasks checklist)
└── prompts/
    ├── 1-clarity-review.md   # Prompt: Audit specification for clarity and completeness
    ├── 2-generate-plan.md    # Prompt: Translate spec into technical implementation plan
    └── 3-execute-task.md     # Prompt: Implement tasks incrementally with tests
```

---

## 🚀 How to Use

### Step 1: Create a Feature Specification
1. Create a feature folder under `specs/features/<feature-name>/` (or create a file `specs/features/<feature-name>.md`).
2. Copy [templates/spec-template.md](templates/spec-template.md) into your feature file.
3. Fill in the user stories, acceptance criteria, and business constraints.

### Step 2: Request an AI Clarity Review
In your AI chat assistant, run or paste the instructions from [prompts/1-clarity-review.md](prompts/1-clarity-review.md), referencing your feature spec:
> *"Review `@specs/features/<feature-name>/spec.md` using the rules in `@specs/prompts/1-clarity-review.md`."*

Refine the specification based on the AI's feedback and unanswered edge cases.

### Step 3: Generate the Technical Plan
Once the spec is solid, ask the AI to design the plan:
> *"Generate an implementation plan for `@specs/features/<feature-name>/spec.md` following `@specs/prompts/2-generate-plan.md`."*

Save the output to `specs/features/<feature-name>/plan.md`.

### Step 4: Execute Incrementally
Guide the AI to execute the tasks one at a time:
> *"Execute the next incomplete task in `@specs/features/<feature-name>/plan.md` following `@specs/prompts/3-execute-task.md`."*

The AI will write the code, run `dotnet test`, and update the checkbox upon verification.
