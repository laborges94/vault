# Feature Specification: Core Secret Vault & Developer Sharing

**Status**: Approved  
**Author(s)**: Lucas Borges  
**Created**: 2026-09-21  
**Last Updated**: 2026-09-21  

---

## 1. Overview & Problem Statement

Software development teams frequently need to store, synchronize, and exchange sensitive credentials—such as database connection strings, third-party API keys, OAuth client secrets, and environment configurations (`.env`).

Currently, teams face a difficult trade-off:
1. **Insecure Channels**: Sharing secrets via chat apps (Slack, Microsoft Teams, Discord), unencrypted email, or committing them accidentally to version control.
2. **Prohibitive Cost & Complexity**: Adopting heavyweight enterprise vaults (e.g., HashiCorp Vault, AWS Secrets Manager, 1Password Teams) which impose substantial cloud infrastructure complexity, maintenance overhead, or steep per-user licensing fees.

**Vault** is a lightweight, zero-knowledge, open-source C#/.NET developer vault. It provides local encrypted vault file storage, a command-line interface (CLI), seamless `.env` file synchronization, process secret injection, and ephemeral peer-to-peer secret sharing envelopes.

### 1.1 Goals
- **Zero-Knowledge Local Storage**: Store secrets in a local, password-protected encrypted file format (`.vault.enc`) using industry-standard cryptography (Argon2id for key derivation, AES-256-GCM for authenticated encryption).
- **Developer-Centric CLI**: Provide an intuitive CLI tool (`vault init`, `vault set`, `vault get`, `vault list`, `vault delete`, `vault share`, `vault open`) for managing secrets across development workflows.
- **Seamless `.env` Integration**:
  - `vault env push`: Encrypt and store local `.env` variables into the vault.
  - `vault env pull`: Decrypt secrets from the vault into a local `.env` file.
  - `vault run -- <command>`: Inject decrypted secrets directly into the target child process's in-memory environment without ever writing plaintext to disk.
- **Ephemeral Secret Sharing**: Provide a secure peer-to-peer sharing mechanism (`vault share <key>` and `vault open <envelope>`) producing a self-contained, encrypted envelope with client-validated time-to-live (TTL).
- **Strict Memory Hygiene**: Ensure plaintext secrets and derived encryption keys are zeroed out in memory immediately after cryptographic operations.

### 1.2 Non-Goals (Out of Scope)
- **Centralized Cloud Server & Remote Relay (MVP)**: This initial specification focuses on the standalone CLI tool, core library, and local encrypted vault storage. Multi-tenant web hosting, cloud daemon synchronization, and true single-use server-side burn-after-reading relays are deferred to subsequent specifications (e.g., Feature 002: Cloud Relay & Synchronization). Ephemeral sharing in this MVP is offline and token-based with client-enforced TTL.
- **Dynamic Secret Generation**: Automatically rotating cloud credentials or generating short-lived AWS/GCP IAM tokens is deferred to future plugin extensions.
- **Hardware Security Module (HSM) / PKCS#11 Integration**: Hardware-bound keys (YubiKey/TPM/HSM) will be considered in future releases.

---

## 2. User Stories & Personas

- **Persona 1: Alex (Software Engineer)**
  - *As a developer*, I want to encrypt and share a temporary database password with a teammate using an encrypted envelope with an expiration TTL, so that we do not leak plaintext credentials in chat history.
- **Persona 2: Beatriz (Tech Lead / Architect)**
  - *As a tech lead*, I want to keep project secrets securely encrypted in a `.vault.enc` file that can be safely version-controlled or backed up, so that new developers can onboard and decrypt the project secrets using a single team master passphrase.
- **Persona 3: Carlos (DevOps / Backend Developer)**
  - *As a backend engineer*, I want to run my application locally with `vault run -- dotnet run`, so that the application receives all necessary environment variables in memory without keeping unencrypted `.env` files lying on my disk.

---

## 3. Functional Requirements

### 3.1 Vault Initialization & Cryptographic Management
- **FR-01 (Vault Initialization)**: The system MUST allow users to initialize a new vault via `vault init [path]`, prompting for a master passphrase with confirmation and optional minimum entropy validation.
- **FR-01a (Non-Interactive Authentication)**: The CLI MUST support non-interactive passphrase provisioning via:
  1. Environment variable: `VAULT_PASSPHRASE`
  2. Stdin pipe: `--passphrase-stdin`
  3. File path: `--passphrase-file <path>`
  When none of these options are provided in an interactive session, the CLI MUST prompt securely via terminal standard input with echo suppressed.
- **FR-02 (Key Derivation)**: The system MUST derive encryption keys from the master passphrase using **Argon2id** with cryptographically secure random salt (minimum 16 bytes) and standardized parameters (e.g., minimum 64MB memory cost, 3 iterations, 4 lanes).
- **FR-03 (Authenticated Encryption)**: The system MUST encrypt and decrypt vault payloads using **AES-256-GCM** (authenticated encryption with associated data - AEAD), generating a unique 96-bit initialization vector (nonce) for every encryption operation and verifying the 128-bit authentication tag upon decryption.
- **FR-03a (Vault File Header & AAD Binding)**: The `.vault.enc` container format MUST define a deterministic binary layout:
  - Magic Bytes: `0x56 0x41 0x55 0x4C 0x54` (`VAULT`)
  - Format Version: 16-bit unsigned integer (initial version: `1`)
  - Argon2id Parameters: Salt (16 bytes), Iterations (`uint32`), Memory in KiB (`uint32`), Parallelism (`uint32`)
  - AES-GCM Initialization Vector (Nonce): 12 bytes
  - AES-GCM Authentication Tag: 16 bytes
  - Encrypted Payload: Variable length ciphertext
  All header fields prior to ciphertext MUST be passed as Additional Authenticated Data (AAD) during AES-GCM encryption and decryption to prevent tampering with format versions or key derivation parameters.
- **FR-03b (Atomic File Persistence)**: All write operations modifying `.vault.enc` MUST write the complete payload to a staging file (`<target>.tmp.<guid>`) in the same directory and execute an atomic replace (`File.Move(..., overwrite: true)`) to prevent vault corruption in the event of power loss, process crash, or write interruption.
- **FR-04 (Zero Plaintext Persistence)**: The system MUST NEVER persist plaintext secrets, unencrypted master passphrases, or intermediate keys to disk, log outputs, or error reports.
- **FR-05 (Memory Sanitization)**: The system MUST treat sensitive data defensively in memory. Passphrases, derived keys, and decrypted secret payloads MUST be represented as byte arrays (`byte[]`), character arrays (`char[]`), or memory spans (`Span<byte>`), and cleared using `CryptographicOperations.ZeroMemory` immediately upon completion or disposal. Immutable managed strings (`System.String`) MUST NOT be used for intermediate cryptographic keys or raw secret buffers.

### 3.2 Secret Lifecycle (CRUD)
- **FR-06 (Create/Update Secret)**: The system MUST provide `vault set <key> <value>` (or interactive prompt / stdin piping) to add or update secrets with optional metadata (description, tags, expiration timestamp) and environment namespace support (`--env <name>`).
- **FR-06a (Secret Key & Value Constraints)**:
  - Secret keys MUST be case-sensitive alphanumeric strings including underscores, hyphens, periods, and colons/slashes for hierarchical grouping (regex: `^[a-zA-Z0-9_:\.\/-]{1,128}$`).
  - Secret values MUST support arbitrary UTF-8 text payloads up to a default maximum limit of 1 MiB per secret entry.
  - Environment namespacing via `--env <name>` MUST be supported uniformly across `set`, `get`, `list`, and `delete`.
- **FR-07 (Retrieve Secret)**: The system MUST provide `vault get <key> [--env <name>]` to decrypt and display a secret value, supporting raw output (`--raw`) for piping and clipboard copying (`--copy`).
- **FR-08 (List Secrets)**: The system MUST provide `vault list [--env <name>]` to list all stored secret keys, tags, last-updated timestamps, and metadata without exposing secret values.
- **FR-09 (Delete Secret)**: The system MUST provide `vault delete <key> [--env <name>]` to remove a secret from the vault with confirmation (or `--force` flag).

### 3.3 Seamless Environment (`.env`) Integration
- **FR-10 (`.env` Push)**: The system MUST provide `vault env push [file] [--env <name>]` to parse a standard `.env` file, validate key-value pairs, ignore comments (`#`) and empty lines, handle quoted values, and batch upsert them into the encrypted vault under an optional environment namespace. Duplicate keys within the same `.env` file MUST emit a warning, applying last-write-wins resolution.
- **FR-11 (`.env` Pull)**: The system MUST provide `vault env pull [file] [--env <name>]` to export secrets from the vault to a `.env` file, prompting for confirmation before overwriting any existing file unless forced (`--force`).
- **FR-12 (Process Injection `vault run`)**: The system MUST provide `vault run [--env <name>] -- <command> [args...]` to decrypt secrets, inject them into the child process's execution environment variables, launch the child process, and exit with the child's return code. Plaintext secrets MUST NOT be written to disk during this execution.

### 3.4 Ephemeral Secret Sharing
- **FR-13 (Encrypted Envelope Generation)**: The system MUST provide `vault share <key> [--env <name>]` (or `vault share --value <val>`) to generate an encrypted, self-contained sharing envelope (encoded as base64/bech32).
- **FR-14 (Ephemeral Cryptographic Model)**: Ephemeral envelopes MUST be encrypted using AES-256-GCM with a newly generated 256-bit symmetric key. The resulting shareable bundle contains the ciphertext, nonce, salt, and metadata (creation timestamp, TTL). The decryption key is output separately to the sender (e.g., via hash fragment `#<key>` or separate display) so holding only the envelope ciphertext does not permit decryption.
- **FR-15 (Envelope Consumption)**: The system MUST provide `vault open <envelope> [--key <key>]` to decrypt and output or copy the ephemeral secret.
- **FR-16 (TTL & Offline Enforcement)**: TTL (`--ttl <duration>`, e.g., `1h`, `24h`) is validated client-side against the recipient's system clock upon execution of `vault open`. If the duration since creation exceeds the TTL, decryption fails with an expiration error. True server-side burn-after-reading is deferred to a future relay feature.

---

## 4. Acceptance Criteria

### Scenario 1: Initializing a New Encrypted Vault
- **Given**: A directory without an existing vault file.
- **When**: The user executes `vault init` and provides a valid passphrase.
- **Then**: 
  - A `.vault.enc` file is created containing the magic header `VAULT` (`0x56 0x41 0x55 0x4C 0x54`), format version `1`, Argon2id salt/parameters, nonce, encrypted payload, and authentication tag.
  - All header fields are bound as AAD to the AES-GCM cipher.
  - The vault file contains zero plaintext characters corresponding to the passphrase or payload.
  - Temporary staging files (`.vault.enc.tmp.*`) are cleanly removed upon completion.
  - Subsequent access with the correct passphrase succeeds.

### Scenario 2: Storing and Retrieving Secrets (with Namespaces and Validation)
- **Given**: An initialized vault at `.vault.enc`.
- **When**: 
  - The user executes `vault set DB_PASSWORD "s3cur3P@ssw0rd!" --env staging`.
  - The user executes `vault set "INVALID KEY NAME" "value"` (containing invalid spaces).
- **Then**: 
  - The invalid key command fails immediately with a validation error citing the allowed key pattern (`^[a-zA-Z0-9_:\.\/-]{1,128}$`).
  - Running `vault get DB_PASSWORD --env staging` decrypts and displays `"s3cur3P@ssw0rd!"`.
  - Running `vault list --env staging` displays `DB_PASSWORD` without exposing the secret value.
  - Inspecting the raw file `.vault.enc` reveals no instance of `"s3cur3P@ssw0rd!"`.

### Scenario 3: Tamper Detection & Integrity Verification
- **Given**: An initialized `.vault.enc` file containing stored secrets.
- **When**: An attacker or corrupted disk flips a single bit in the header metadata (AAD) or the ciphertext of `.vault.enc`.
- **Then**: 
  - The decryption operation MUST fail immediately due to AES-GCM authentication tag mismatch.
  - The system MUST output an integrity validation error without leaking partial contents or crashing with an unhandled exception.

### Scenario 4: Process Secret Injection with `vault run`
- **Given**: A vault containing `API_KEY=test-token-xyz` under `--env production`.
- **When**: The user executes `vault run --env production -- pwsh -c "Get-ChildItem env:API_KEY"`.
- **Then**: 
  - The child process receives `API_KEY: test-token-xyz` in memory and prints it.
  - No temporary `.env` or plaintext file is written to the filesystem.
  - The parent process cleanly zeroes decrypted buffers and exits with the child process's exact return code.

### Scenario 5: Ephemeral Secret Sharing & Envelope Consumption
- **Given**: A secret `PROD_SSH_KEY` stored in the vault.
- **When**: The user runs `vault share PROD_SSH_KEY --ttl 1h`.
- **Then**: 
  - An encrypted shareable envelope string is generated, and a separate decryption key is displayed or formatted as a URL/fragment.
  - Running `vault open <envelope> --key <key>` before expiration decrypts and outputs `PROD_SSH_KEY`.
  - Running `vault open <envelope> --key <invalid-key>` fails with an authentication error.
  - Running `vault open <envelope> --key <key>` after 1 hour fails with an expiration error citing TTL expiration.

### Scenario 6: Non-Interactive Passphrase Provisioning
- **Given**: An initialized vault and `VAULT_PASSPHRASE` configured in the environment.
- **When**: The user executes `vault get DB_PASSWORD` without a TTY prompt, or runs `echo "mypass" | vault get DB_PASSWORD --passphrase-stdin`.
- **Then**: 
  - The CLI consumes the passphrase non-interactively without blocking or requesting interactive input.
  - Decryption succeeds normally.

---

## 5. Edge Cases & Error Handling

- **EC-01 (Incorrect Master Passphrase)**: 
  - *Behavior*: If the user inputs the wrong passphrase, AES-GCM tag verification fails. The CLI must output `Error: Authentication failed. Invalid passphrase or corrupted vault.` The error response time should remain resilient against timing discrepancies.
- **EC-02 (Corrupted or Truncated File)**:
  - *Behavior*: If `.vault.enc` is incomplete, missing magic bytes (`VAULT`), or has an unsupported format version, the system must abort with a descriptive validation error before attempting cryptographic processing.
- **EC-03 (Concurrent Modifications & File Locking)**:
  - *Behavior*: When reading or writing `.vault.enc`, the application must acquire a file lock (`FileShare.None` during writes). If the lock cannot be acquired within 3 seconds, the CLI must abort gracefully with `Error: Vault file is locked by another process.`
- **EC-04 (Environment File Edge Cases & Duplicates)**:
  - *Behavior*: When executing `vault env push`, blank lines and comments (`#`) must be ignored. Quoted values (single and double) must be unescaped properly. Duplicate keys within the `.env` file must trigger a warning to stderr and resolve using last-write-wins.
- **EC-05 (Memory Dump Protection & String Hygiene)**:
  - *Behavior*: Passphrases, keys, and raw secrets must avoid long-lived immutable `string` allocations where feasible, using `byte[]`, `char[]`, or pinned memory spans cleared via `CryptographicOperations.ZeroMemory` in `finally` blocks or `IDisposable.Dispose()`.
- **EC-06 (Asynchronous I/O & Cancellation)**:
  - *Behavior*: All storage, file access, and child process executions must accept a `CancellationToken`. User cancellation (e.g., `Ctrl+C`) must terminate child processes cleanly and delete any temporary staging files (`.vault.enc.tmp.*`).

---

## 6. Open Questions & Assumptions

- **Assumptions**:
  - Developers have .NET 10.0 runtime (or will install the tool via `dotnet tool install -g Vault.Cli` or a native self-contained binary).
  - The default vault location is `.vault.enc` in the current working directory, with fallback or configuration to global `~/.vault/vault.enc` via environment variable `VAULT_FILE`.
  - Argon2id will be supported via standard .NET / audited managed cryptographic package (such as `Konscious.Security.Cryptography.Argon2` or modern .NET primitives).
- **Resolution of Architectural Questions**:
  - *Q1 (Ephemeral Sharing Scope)*: Resolved for MVP (Feature 001). Ephemeral sharing will produce self-contained, offline encrypted envelopes with client-side TTL checks and separated decryption keys. True single-use server-side burn-after-reading relays are deferred to **Feature 002: Cloud Relay & Synchronization**.
  - *Q2 (Public-Key / Asymmetric Encryption)*: Resolved for MVP. Public-key encryption (e.g., age/GPG recipients) is deferred to future plugin extensions.
