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

## 💻 CLI Usage

### Initializing a Vault

```bash
# Initialize a new encrypted container (default: .vault.enc)
vault init

# Specify a custom vault path
vault init ./secrets/app.vault.enc
```

### Managing Secrets (CRUD)

```bash
# Store or update a secret
vault set DB_PASSWORD "s3cur3P@ssw0rd!"

# Store secret in a specific environment namespace
vault set API_KEY "prod-secret-token" --env production --description "Production API Key" --tags "api,auth"

# Retrieve secret (standard formatted)
vault get DB_PASSWORD

# Retrieve raw secret value (useful for piping / scripting)
vault get DB_PASSWORD --raw

# List secrets in default environment
vault list

# List secrets across all environments
vault list --all-envs

# Delete a secret
vault delete DB_PASSWORD --force
```

### Environment File (`.env`) Synchronization

```bash
# Import key-value pairs from .env into vault
vault env push .env

# Export vault secrets to a .env file
vault env pull .env --force
```

### Process Injection (`vault run`)

Inject decrypted secrets directly into child process memory without creating unencrypted files on disk:

```bash
vault run --env production -- dotnet run
```

### Ephemeral Secret Sharing

Encrypt sensitive credentials into self-contained, TTL-bounded envelopes for secure sharing via chat or email:

```bash
# Create an ephemeral share (default TTL: 15m)
vault share DB_PASSWORD --ttl 1h

# Or share a raw value directly
vault share --value "one-time-token" --ttl 30m

# Recipient consumes the envelope using the provided key
vault open <envelope> --key <decryption-key>

# Or with combined token fragment
vault open <envelope>#<decryption-key> --raw
```

### Non-Interactive Authentication (CI/CD & Scripts)

Passphrases can be supplied non-interactively via environment variable, stdin, or file:

```bash
# Via environment variable
export VAULT_PASSPHRASE="your-master-passphrase"
vault get DB_PASSWORD --raw

# Via standard input
echo "your-master-passphrase" | vault get DB_PASSWORD --passphrase-stdin

# Via passphrase file
vault get DB_PASSWORD --passphrase-file /path/to/keyfile
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

