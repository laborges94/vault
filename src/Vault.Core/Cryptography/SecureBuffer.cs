using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Vault.Core.Cryptography;

/// <summary>
/// A disposable wrapper over sensitive byte arrays that zeroes underlying memory on disposal.
/// </summary>
public sealed class SecureBuffer : IDisposable
{
    private byte[]? _buffer;
    private bool _disposed;

    public SecureBuffer(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        _buffer = new byte[length];
    }

    public SecureBuffer(byte[] buffer, bool copy = false)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (copy)
        {
            _buffer = new byte[buffer.Length];
            buffer.CopyTo(_buffer, 0);
        }
        else
        {
            _buffer = buffer;
        }
    }

    public int Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _buffer!.Length;
        }
    }

    public Span<byte> Span
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _buffer.AsSpan();
        }
    }

    public ReadOnlySpan<byte> ReadOnlySpan
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _buffer.AsSpan();
        }
    }

    public byte[] DangerousGetUnderlyingArray()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _buffer!;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_buffer != null)
        {
            CryptographicOperations.ZeroMemory(_buffer);
            _buffer = null;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// A disposable wrapper over sensitive character arrays that zeroes underlying memory on disposal.
/// </summary>
public sealed class SecureCharBuffer : IDisposable
{
    private char[]? _buffer;
    private bool _disposed;

    public SecureCharBuffer(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        _buffer = new char[length];
    }

    public SecureCharBuffer(char[] buffer, bool copy = false)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (copy)
        {
            _buffer = new char[buffer.Length];
            buffer.CopyTo(_buffer, 0);
        }
        else
        {
            _buffer = buffer;
        }
    }

    public int Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _buffer!.Length;
        }
    }

    public Span<char> Span
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _buffer.AsSpan();
        }
    }

    public ReadOnlySpan<char> ReadOnlySpan
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _buffer.AsSpan();
        }
    }

    public char[] DangerousGetUnderlyingArray()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _buffer!;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_buffer != null)
        {
            var byteSpan = MemoryMarshal.AsBytes(_buffer.AsSpan());
            CryptographicOperations.ZeroMemory(byteSpan);
            _buffer = null;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
