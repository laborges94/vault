# Technical Plan: Core Secret Vault & Developer Sharing

**Spec Reference**: `specs/features/001-secret-vault-core/spec.md`  
**Status**: Draft  
**Author(s)**: Lucas Borges  
**Date**: 2026-09-21  

---

## 1. Technical Overview & Architectural Approach

This technical plan details the architecture, component design, and incremental task breakdown for implementing **Vault**: a lightweight, zero-knowledge, open-source C#/.NET developer secret vault and ephemeral sharing tool.

The implementation strictly honors the foundational principles in [specs/constitution.md](../../constitution.md) and coding conventions in [.github/copilot-instructions.md](../../.github/copilot-instructions.md):
- **Language**: Strictly English for all code, identifiers, comments, tests, and documentation.
- **Target Framework**: Modern .NET 10.0 (`net10.0`) with C# 14 / C# 10+ language features, file-scoped namespaces, record types, pattern matching, and nullable reference types enabled (`<Nullable>enable</Nullable>`).
- **Cryptographic Security**: Audited cryptographic primitives: Argon2id for memory-hard key derivation and AES-256-GCM for authenticated encryption with associated data (AEAD). Custom cryptography is strictly forbidden.
- **Defensive Memory Hygiene**: Plaintext secrets, master passphrases, and derived keys are handled in pinned or clearable buffers (`byte[]`, `char[]`, `Span<byte>`) and sanitized via `CryptographicOperations.ZeroMemory` immediately after use.
- **Zero Plaintext Persistence**: Plaintext secrets are never persisted to disk, written to unencrypted temporary files, logged, or included in exception messages.

### 1.1 Architecture & Design Decisions

1. **Layered Decoupled Architecture**:
   - `Vault.Core`: A portable class library encapsulating domain models, cryptographic engines, storage persistence, `.env` file parsing, process execution injection, and ephemeral envelope sharing. All I/O operations are abstracted via interfaces (`IVaultStorage`, `IProcessRunner`, `IConsoleReader`).
   - `Vault.Cli`: A console application and .NET tool hosting the CLI commands (`init`, `set`, `get`, `list`, `delete`, `env push`, `env pull`, `run`, `share`, `open`). Uses `System.CommandLine` for type-safe argument parsing, help generation, and subcommands.
   - `tests/Vault.Core.Tests`: Unit tests for cryptography, storage serialization, `.env` parsing, memory sanitization, and envelope TTL logic.
   - `tests/Vault.Cli.Tests`: Integration tests for CLI command parsing, exit codes, stdin piping, and non-interactive workflows.

2. **Cryptographic Container Layout (`.vault.enc`)**:
   - Header format:
     - Magic Bytes: `0x56 0x41 0x55 0x4C 0x54` (`VAULT` - 5 bytes)
     - Format Version: `ushort` (2 bytes, little-endian, initial value `1`)
     - Argon2id Salt: 16 bytes
     - Argon2id Iterations: `uint` (4 bytes)
     - Argon2id Memory in KiB: `uint` (4 bytes, default 65536 KiB = 64 MiB)
     - Argon2id Parallelism: `uint` (4 bytes, default 4 lanes)
     - AES-GCM Nonce: 12 bytes
     - AES-GCM Tag: 16 bytes
     - Ciphertext: Variable length payload
   - Additional Authenticated Data (AAD): All header bytes preceding the ciphertext (bytes 0 to 58) are supplied as AAD to AES-256-GCM during encryption and decryption to detect any header tampering.

3. **Atomic File Persistence & Concurrency**:
   - Write operations write full contents to a temporary sibling file (`<target>.tmp.<guid>`), acquire exclusive file locks (`FileShare.None`) with a 3-second timeout, and perform atomic replacement (`File.Move(..., overwrite: true)`).
   - On unhandled failure or cancellation, temporary staging files are deleted.

4. **In-Memory Model & Serialization**:
   - Vault payload inside ciphertext is serialized as UTF-8 JSON representing a `VaultDocument`:
     - Schema version (`int`)
     - Metadata (Created, Modified)
     - Environments: `Dictionary<string, Dictionary<string, SecretEntry>>` where `SecretEntry` contains `Value`, `Description`, `Tags`, `UpdatedAt`, `ExpiresAt`.
   - Default environment is `default` when `--env` is omitted.
   - Memory buffers are converted to UTF-8 byte spans during encryption/decryption and zeroed immediately.

5. **Process Injection (`vault run`)**:
   - `IProcessRunner` decrypts secrets in memory, constructs `ProcessStartInfo` with injected `EnvironmentVariables`, spawns the child process, streams standard I/O asynchronously, monitors `CancellationToken`, cleans up buffers, and forwards the child's exact exit code.

6. **Ephemeral Secret Sharing**:
   - `vault share`: Generates a random 256-bit AES-GCM symmetric key and random 96-bit nonce. Encrypts payload containing secret value, created timestamp, and TTL duration.
   - Outputs a shareable envelope: Base64URL-encoded payload containing `[Version:1B][CreatedAt:8B][TtlSeconds:4B][Nonce:12B][Tag:16B][Ciphertext]`.
   - The key is displayed separately or formatted with fragment anchor (`#<hex-or-base64-key>`) so ciphertext alone is unreadable.
   - `vault open`: Parses the envelope, validates client-side TTL (`CreatedAt + TTL > UtcNow`), checks integrity, and decrypts with the supplied key.

### 1.2 Dependencies & External Libraries

- `Konscious.Security.Cryptography.Argon2` (NuGet, version `1.3.1` or latest): Audited managed Argon2id implementation for .NET.
- `System.CommandLine` (NuGet, version `2.0.0-beta4.*` or stable equivalent): Standardized command-line parsing and shell completion.
- Standard Library:
  - `System.Security.Cryptography.AesGcm` (native .NET authenticated encryption).
  - `System.Security.Cryptography.RandomNumberGenerator` (cryptographically secure random byte generation).
  - `System.Security.Cryptography.CryptographicOperations` (`ZeroMemory`).
  - `System.Text.Json` (high-performance JSON serialization for decrypted payload).

---

## 2. File & Component Changes

Identify the files to be created, modified, or deleted across project layers.

| Action | Path | Description |
| :--- | :--- | :--- |
| `[NEW]` | `Vault.sln` | Root Visual Studio / .NET solution file |
| `[NEW]` | `src/Vault.Core/Vault.Core.csproj` | Core class library project targeting `net10.0` |
| `[NEW]` | `src/Vault.Core/Model/SecretEntry.cs` | Immutable record representing a secret entry with metadata |
| `[NEW]` | `src/Vault.Core/Model/VaultDocument.cs` | In-memory root document representing environments and secret entries |
| `[NEW]` | `src/Vault.Core/Model/SecretKeyValidator.cs` | Validation logic for secret key naming constraints (`^[a-zA-Z0-9_:\.\/-]{1,128}$`) |
| `[NEW]` | `src/Vault.Core/Cryptography/Argon2Parameters.cs` | Configuration record and defaults for Argon2id key derivation |
| `[NEW]` | `src/Vault.Core/Cryptography/IKeyDerivationService.cs` | Interface for deriving encryption keys from master passphrases |
| `[NEW]` | `src/Vault.Core/Cryptography/Argon2KeyDerivationService.cs` | Argon2id key derivation implementation with zeroing support |
| `[NEW]` | `src/Vault.Core/Cryptography/IAeadEncryptionService.cs` | Interface for AES-256-GCM encryption/decryption with AAD |
| `[NEW]` | `src/Vault.Core/Cryptography/AesGcmEncryptionService.cs` | AES-256-GCM authenticated cipher implementation |
| `[NEW]` | `src/Vault.Core/Cryptography/SecureBuffer.cs` | Wrapper managing sensitive memory byte/char buffers and zeroing upon disposal |
| `[NEW]` | `src/Vault.Core/Storage/VaultHeader.cs` | Binary header parser, serializer, and AAD generator for `.vault.enc` |
| `[NEW]` | `src/Vault.Core/Storage/IVaultStorage.cs` | Interface for loading and persisting encrypted vault files |
| `[NEW]` | `src/Vault.Core/Storage/FileVaultStorage.cs` | Atomic file storage implementation with staging files and file locking |
| `[NEW]` | `src/Vault.Core/Services/IVaultService.cs` | High-level application service interface for vault CRUD operations |
| `[NEW]` | `src/Vault.Core/Services/VaultService.cs` | Implementation of vault operations (init, set, get, list, delete) |
| `[NEW]` | `src/Vault.Core/Env/DotEnvParser.cs` | Parser and serializer for `.env` files with comment and quote handling |
| `[NEW]` | `src/Vault.Core/Execution/IProcessRunner.cs` | Interface for child process execution with injected environment variables |
| `[NEW]` | `src/Vault.Core/Execution/ProcessRunner.cs` | Process runner implementation with stream forwarding and signal handling |
| `[NEW]` | `src/Vault.Core/Sharing/SharingEnvelope.cs` | Data structure and serialization for ephemeral sharing envelopes |
| `[NEW]` | `src/Vault.Core/Sharing/IEphemeralShareService.cs` | Interface for generating and opening ephemeral secret envelopes |
| `[NEW]` | `src/Vault.Core/Sharing/EphemeralShareService.cs` | AES-GCM envelope encryption/decryption and client TTL enforcement |
| `[NEW]` | `src/Vault.Core/Exceptions/VaultException.cs` | Base domain exception classes (AuthenticationFailed, CorruptedVault, etc.) |
| `[NEW]` | `src/Vault.Cli/Vault.Cli.csproj` | Console executable and .NET tool project targeting `net10.0` |
| `[NEW]` | `src/Vault.Cli/Program.cs` | CLI entry point, command dispatching, and global exception mapping |
| `[NEW]` | `src/Vault.Cli/Authentication/IPassphraseProvider.cs` | Interface for obtaining passphrase from env, stdin, file, or TTY |
| `[NEW]` | `src/Vault.Cli/Authentication/PassphraseProvider.cs` | Implementation handling non-interactive and interactive masked terminal input |
| `[NEW]` | `src/Vault.Cli/Commands/InitCommand.cs` | Command handler for `vault init` |
| `[NEW]` | `src/Vault.Cli/Commands/SetCommand.cs` | Command handler for `vault set` |
| `[NEW]` | `src/Vault.Cli/Commands/GetCommand.cs` | Command handler for `vault get` |
| `[NEW]` | `src/Vault.Cli/Commands/ListCommand.cs` | Command handler for `vault list` |
| `[NEW]` | `src/Vault.Cli/Commands/DeleteCommand.cs` | Command handler for `vault delete` |
| `[NEW]` | `src/Vault.Cli/Commands/EnvPushCommand.cs` | Command handler for `vault env push` |
| `[NEW]` | `src/Vault.Cli/Commands/EnvPullCommand.cs` | Command handler for `vault env pull` |
| `[NEW]` | `src/Vault.Cli/Commands/RunCommand.cs` | Command handler for `vault run` |
| `[NEW]` | `src/Vault.Cli/Commands/ShareCommand.cs` | Command handler for `vault share` |
| `[NEW]` | `src/Vault.Cli/Commands/OpenCommand.cs` | Command handler for `vault open` |
| `[NEW]` | `src/Vault.Cli/Output/IConsoleFormatter.cs` | Formatter for table output, warnings, errors, and raw output |
| `[NEW]` | `tests/Vault.Core.Tests/Vault.Core.Tests.csproj` | Unit test project targeting `net10.0` |
| `[NEW]` | `tests/Vault.Core.Tests/Cryptography/Argon2KeyDerivationTests.cs` | Unit tests for Argon2id key derivation |
| `[NEW]` | `tests/Vault.Core.Tests/Cryptography/AesGcmEncryptionTests.cs` | Unit tests for AES-256-GCM encryption, decryption, and tag validation |
| `[NEW]` | `tests/Vault.Core.Tests/Storage/VaultHeaderTests.cs` | Unit tests for binary header parsing and AAD consistency |
| `[NEW]` | `tests/Vault.Core.Tests/Storage/FileVaultStorageTests.cs` | Unit tests for atomic file saving, corrupt file handling, and lock contention |
| `[NEW]` | `tests/Vault.Core.Tests/Services/VaultServiceTests.cs` | Unit tests for vault CRUD and environment namespacing |
| `[NEW]` | `tests/Vault.Core.Tests/Env/DotEnvParserTests.cs` | Unit tests for `.env` parsing, comments, quotes, and duplicate warning |
| `[NEW]` | `tests/Vault.Core.Tests/Sharing/EphemeralShareTests.cs` | Unit tests for envelope encryption, decryption, invalid keys, and TTL expiration |
| `[NEW]` | `tests/Vault.Cli.Tests/Vault.Cli.Tests.csproj` | CLI test project targeting `net10.0` |
| `[NEW]` | `tests/Vault.Cli.Tests/CliCommandTests.cs` | Functional tests for CLI invocation, argument parsing, and exit codes |
| `[MODIFY]` | `README.md` | Keep documentation aligned with implemented CLI commands, syntax, and build commands |

---

## 3. Verification & Testing Strategy

- **Unit Tests**:
  - **Cryptography**: Verify roundtrip encryption/decryption, assert that flipping any ciphertext bit or AAD bit throws `AuthenticationTagMismatchException`, verify Argon2id parameter handling, and assert memory zeroing on disposable secure buffers.
  - **Storage & Header**: Verify binary header layout (magic `VAULT`, version `1`, salt, parameters, nonce, tag), verify atomic staging file replace, and verify lock acquisition failure on simulated locks.
  - **Domain & Services**: Verify CRUD operations across default and custom environments, key pattern validation (`SecretKeyValidator`), payload size limits, and non-existent key retrieval.
  - **Environment (`.env`)**: Verify multi-line comments, quotes, whitespace, variable substitution / escaping, duplicate keys last-write-wins warning.
  - **Ephemeral Sharing**: Verify envelope serialization/deserialization, successful open within TTL, expiration failure when TTL elapsed, and authentication failure on wrong key.
- **Integration Tests**:
  - **Process Injection (`vault run`)**: Execute child process (`pwsh -c` or `dotnet --version`), assert that secret environment variables are available in the child environment, verify exit code propagation, and ensure no unencrypted secrets are dumped to disk.
  - **CLI Non-Interactive Authentication**: Test passphrase acquisition via environment variable `VAULT_PASSPHRASE` and piped stdin (`--passphrase-stdin`).
- **Commands**:
  ```bash
  # Build solution
  dotnet build

  # Run all automated tests
  dotnet test

  # Run Core tests only
  dotnet test tests/Vault.Core.Tests

  # Run CLI tests only
  dotnet test tests/Vault.Cli.Tests
  ```

---

## 4. Implementation Tasks

Ordered, atomic task checklist adhering to Test-Driven Development (TDD) principles. Each task is independently verifiable.

- [x] **Task 1: Solution & Project Scaffolding**
  - [x] Initialize `Vault.sln` with `src/Vault.Core`, `src/Vault.Cli`, `tests/Vault.Core.Tests`, and `tests/Vault.Cli.Tests`.
  - [x] Configure target framework `net10.0`, `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, and package references (`Konscious.Security.Cryptography.Argon2`, `System.CommandLine`).
  - [x] Verify clean build with `dotnet build`.

- [x] **Task 2: Core Domain Models & Validation**
  - [x] Create `SecretEntry` record with metadata (value, description, tags, timestamps).
  - [x] Create `VaultDocument` aggregate with environment dictionary structure.
  - [x] Implement `SecretKeyValidator` enforcing `^[a-zA-Z0-9_:\.\/-]{1,128}$` and 1 MiB value limit.
  - [x] Write unit tests verifying model immutability and key/value validation constraints.

- [x] **Task 3: Cryptographic Primitives & Memory Hygiene**
  - [x] Implement `SecureBuffer` implementing `IDisposable` with `CryptographicOperations.ZeroMemory` for `byte[]` and `char[]`.
  - [x] Create `IKeyDerivationService` and `Argon2KeyDerivationService` with configurable parameters (64MB memory, 3 iterations, 4 lanes).
  - [x] Create `IAeadEncryptionService` and `AesGcmEncryptionService` supporting AES-256-GCM with Additional Authenticated Data (AAD).
  - [x] Write unit tests for Argon2id key derivation, AES-GCM encryption/decryption, tamper detection on bit flips, and buffer memory zeroing.

- [ ] **Task 4: Binary Vault Container Header & Atomic Storage**
  - [ ] Implement `VaultHeader` reading and writing the binary specification (`VAULT`, version, salt, Argon2 params, nonce, tag, AAD generation).
  - [ ] Implement `IVaultStorage` and `FileVaultStorage` supporting atomic writes (`<file>.tmp.<guid>` -> `File.Move`), file locking with 3s timeout, and corruption detection.
  - [ ] Write unit tests for header serialization/deserialization, corrupted file rejection, atomic swap, and lock contention handling.

- [ ] **Task 5: Vault Application Service (CRUD & Namespaces)**
  - [ ] Implement `IVaultService` and `VaultService` handling `InitAsync`, `SetSecretAsync`, `GetSecretAsync`, `ListSecretsAsync`, and `DeleteSecretAsync`.
  - [ ] Implement environment namespacing (`--env <name>`) and custom exceptions (`VaultNotFoundException`, `SecretNotFoundException`, `AuthenticationFailedException`).
  - [ ] Write unit tests asserting full secret lifecycle, multiple environments isolation, and error handling.

- [ ] **Task 6: `.env` File Parsing & Synchronization (`push` & `pull`)**
  - [ ] Implement `DotEnvParser` supporting comments (`#`), blank lines, quoted values, and duplicate key warnings with last-write-wins resolution.
  - [ ] Integrate `.env` push and pull into `VaultService`.
  - [ ] Write unit tests asserting proper parsing of various `.env` formats, roundtripping, and duplicate key handling.

- [ ] **Task 7: Process Injection (`vault run`)**
  - [ ] Implement `IProcessRunner` and `ProcessRunner` launching child processes with decrypted secrets injected into environment variables.
  - [ ] Handle stdio forwarding, exit code forwarding, `CancellationToken` cancellation, and memory buffer sanitization.
  - [ ] Write tests verifying environment variable inheritance and process exit code propagation without writing secrets to disk.

- [ ] **Task 8: Ephemeral Secret Sharing & Envelope Consumption**
  - [ ] Implement `SharingEnvelope` data layout, Base64URL encoding, and key separation.
  - [ ] Implement `IEphemeralShareService` and `EphemeralShareService` with client-side TTL check (`CreatedAt + TTL > UtcNow`).
  - [ ] Write unit tests asserting envelope creation, opening before TTL expiration, rejection of expired envelopes, and rejection of invalid keys.

- [ ] **Task 9: CLI Application Infrastructure & Passphrase Provider**
  - [ ] Implement `IPassphraseProvider` and `PassphraseProvider` supporting non-interactive resolution (`VAULT_PASSPHRASE`, `--passphrase-stdin`, `--passphrase-file`) and interactive masked terminal input.
  - [ ] Wire up command-line parsing using `System.CommandLine` in `Vault.Cli`.
  - [ ] Implement `IConsoleFormatter` for structured output, tables, and raw output mode (`--raw`).

- [ ] **Task 10: CLI Command Handlers**
  - [ ] Implement commands: `init`, `set`, `get`, `list`, `delete`.
  - [ ] Implement environment commands: `env push`, `env pull`.
  - [ ] Implement execution command: `run`.
  - [ ] Implement sharing commands: `share`, `open`.
  - [ ] Write functional tests verifying command parsing, non-interactive piping, raw output, and exit codes.

- [ ] **Task 11: End-to-End Verification & Documentation Alignment**
  - [ ] Run full automated test suite across all projects (`dotnet test`).
  - [ ] Verify all scenarios from `specs/features/001-secret-vault-core/spec.md` (Scenario 1 through 6).
  - [ ] Check and update [README.md](../../README.md) to ensure synchronization with implemented CLI commands, prerequisites, and project structure.
