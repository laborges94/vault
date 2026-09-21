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
