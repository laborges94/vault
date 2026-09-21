# Contributing to This Project

First off, thank you for considering contributing to this project! It's people like you that make the open source community such an amazing place to learn, inspire, and create.

## Code of Conduct

Please be respectful and professional in all interactions within this project.

## How Can I Contribute?

### Reporting Bugs
- Search existing issues to see if the bug has already been reported.
- If not, create a new issue detailing:
  - What you expected to happen.
  - What actually happened.
  - Steps to reproduce the behavior.
  - Any error logs or stack traces.

### Suggesting Enhancements
- Open an issue explaining your suggestion and why it would be beneficial to the project.

### Pull Requests
1. Fork the repository.
2. Create a branch (`git checkout -b feature/issue-id-short-description`).
3. Make your changes.
4. Ensure your code follows the coding style defined in `.editorconfig`.
5. Write all code, comments, documentation, PR details, and commit messages in **English**.
6. Run the tests (`dotnet test`) and make sure they pass.
7. Commit your changes using [Conventional Commits](https://www.conventionalcommits.org/) messages (e.g., `feat(auth): add google login provider`).
8. Push to your fork and submit a pull request to the `main` branch using the [pull request template](pull_request_template.md).

## Commit Message Style

We use [Conventional Commits](https://www.conventionalcommits.org/):
- `feat(scope): ...` for new features.
- `fix(scope): ...` for bug fixes.
- `docs(scope): ...` for documentation.
- `style(scope): ...` for code formatting changes.
- `refactor(scope): ...` for cleanups.
- `test(scope): ...` for adding/fixing tests.
