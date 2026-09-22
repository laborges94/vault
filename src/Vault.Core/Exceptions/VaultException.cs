namespace Vault.Core.Exceptions;

/// <summary>
/// Base exception for all Vault domain exceptions.
/// </summary>
public class VaultException : Exception
{
    public VaultException(string message) : base(message)
    {
    }

    public VaultException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when vault inputs or formatting fail validation constraints.
/// </summary>
public class VaultValidationException : VaultException
{
    public VaultValidationException(string message) : base(message)
    {
    }

    public VaultValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when cryptographic authentication or decryption fails due to invalid key or tampered data.
/// </summary>
public class VaultAuthenticationException : VaultException
{
    public VaultAuthenticationException(string message) : base(message)
    {
    }

    public VaultAuthenticationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Alias for VaultAuthenticationException, thrown when master passphrase verification fails.
/// </summary>
public class AuthenticationFailedException : VaultAuthenticationException
{
    public AuthenticationFailedException(string message) : base(message)
    {
    }

    public AuthenticationFailedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when a vault container file is corrupted, truncated, or has an invalid format.
/// </summary>
public class VaultCorruptedException : VaultException
{
    public VaultCorruptedException(string message) : base(message)
    {
    }

    public VaultCorruptedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when a requested vault file cannot be found.
/// </summary>
public class VaultNotFoundException : VaultException
{
    public VaultNotFoundException(string message) : base(message)
    {
    }

    public VaultNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when a requested secret key does not exist within the specified environment.
/// </summary>
public class SecretNotFoundException : VaultException
{
    public SecretNotFoundException(string message) : base(message)
    {
    }

    public SecretNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when the vault file is locked by another process and cannot be accessed within the timeout window.
/// </summary>
public class VaultFileLockedException : VaultException
{
    public VaultFileLockedException(string message) : base(message)
    {
    }

    public VaultFileLockedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when an ephemeral secret sharing envelope has passed its time-to-live (TTL).
/// </summary>
public class VaultExpiredException : VaultException
{
    public VaultExpiredException(string message) : base(message)
    {
    }

    public VaultExpiredException(string message, Exception innerException) : base(message, innerException)
    {
    }
}


