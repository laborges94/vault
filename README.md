# Vault

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
![.NET Version](https://img.shields.io/badge/.NET-10.0-blue.svg)

**Vault** is a lightweight, secure, and open-source zero-knowledge secret vault and ephemeral sharing tool designed for software developers and engineering teams.

---

## 🔒 Key Features

- **Zero-Knowledge Local Storage**: Store secrets in local, encrypted vault files (`.vault.enc`) using industry-standard authenticated encryption (**AES-256-GCM**) and memory-hard key derivation (**Argon2id**).
- **Developer-Friendly CLI**: Effortlessly manage secrets with an intuitive CLI (`vault init`, `vault set`, `vault get`, `vault list`, `vault delete`, `vault share`, `vault open`).
- **Seamless `.env` Integration**:
  - `vault env push`: Safely ingest `.env` files into the encrypted vault.
  - `vault env pull`: Decrypt vault secrets into a local `.env` file.
  - `vault run -- <command>`: Inject secrets directly into a child process's memory without leaving unencrypted files on disk.
- **Ephemeral Secret Sharing**: Share sensitive credentials peer-to-peer using time-to-live (TTL) encrypted envelopes (`vault share`, `vault open`).
- **Memory Hygiene**: Defensive memory clearing (`CryptographicOperations.ZeroMemory`) for sensitive keys and buffers.

---

## 🚀 Getting Started

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
- C# 10+ compatible development environment

### Installation

Clone the repository:

```bash
git clone https://github.com/laborges94/vault.git
cd vault
```

### Build the Project

```bash
dotnet build
```

### Run Tests

```bash
dotnet test
```

---

## 📂 Project Structure

```
├── .agents/                    # AI Agents & Skills
├── .github/
│   ├── workflows/              # GitHub Actions CI/CD pipelines
│   └── copilot-instructions.md # Coding styles and prompt configurations
├── src/                        # Source code directory
│   ├── Vault.Core/             # Core domain, cryptography, and storage abstractions
│   └── Vault.Cli/              # Command-line interface
├── tests/                      # Unit & Integration tests
│   ├── Vault.Core.Tests/
│   └── Vault.Cli.Tests/
├── docs/                       # Wiki & Documentation
├── specs/                      # Spec-Driven Development (SDD)
│   ├── constitution.md         # Invariants & Security Principles
│   ├── features/               # Feature specifications
│   │   └── 001-secret-vault-core/
│   │       └── spec.md         # Core Vault & Developer Sharing specification
│   ├── templates/              # Spec and plan templates
│   └── prompts/                # AI-assisted SDD workflow prompts
├── .editorconfig               # Formatting and style rules
├── .gitattributes              # Line endings configuration
├── .gitignore                  # Git ignore patterns
├── AGENTS.md                   # Instructions for AI coding assistants
├── LICENSE                     # Open source license (MIT)
└── README.md                   # Project documentation
```

---

## 📐 Spec-Driven Development (SDD)

This repository follows a strict **Spec-Driven Development** workflow:

1. **[specs/constitution.md](specs/constitution.md)**: Foundational project invariants, engineering rules, and cryptographic security standards.
2. **[specs/features/001-secret-vault-core/spec.md](specs/features/001-secret-vault-core/spec.md)**: Approved foundational specification for the core vault and sharing system.
3. **[specs/templates/](specs/templates/)**: Functional and technical specification templates.
4. **[specs/prompts/](specs/prompts/)**: Structured prompts guiding AI assistants through review, planning, and task execution.

See [specs/README.md](specs/README.md) for the complete lifecycle guide.

---

## 🤝 Contributing

Contributions, issues, and feature requests are welcome! Feel free to check the [Contributing Guidelines](.github/CONTRIBUTING.md).

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

