using Vault.Core.Cryptography;

namespace Vault.Core.Tests.Cryptography;

public class SecureBufferTests
{
    [Fact]
    public void SecureBuffer_Dispose_ZeroesUnderlyingArray()
    {
        // Arrange
        var rawArray = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var secure = new SecureBuffer(rawArray, copy: false);

        // Act
        secure.Dispose();

        // Assert: rawArray was zeroed out in-place
        Assert.All(rawArray, b => Assert.Equal(0, b));
        Assert.Throws<ObjectDisposedException>(() => secure.Length);
        Assert.Throws<ObjectDisposedException>(() => { _ = secure.Span.Length; });
    }

    [Fact]
    public void SecureBuffer_WithCopyTrue_PreservesSourceAndZeroesInternalArray()
    {
        // Arrange
        var sourceArray = new byte[] { 42, 43, 44 };
        var secure = new SecureBuffer(sourceArray, copy: true);
        var internalArray = secure.DangerousGetUnderlyingArray();

        // Act
        secure.Dispose();

        // Assert: original source array untouched, internal array zeroed
        Assert.Equal(new byte[] { 42, 43, 44 }, sourceArray);
        Assert.All(internalArray, b => Assert.Equal(0, b));
    }

    [Fact]
    public void SecureCharBuffer_Dispose_ZeroesUnderlyingMemory()
    {
        // Arrange
        var rawChars = new char[] { 'S', 'e', 'c', 'r', 'e', 't', '!' };
        var secure = new SecureCharBuffer(rawChars, copy: false);

        // Act
        secure.Dispose();

        // Assert
        Assert.All(rawChars, c => Assert.Equal('\0', c));
        Assert.Throws<ObjectDisposedException>(() => secure.Length);
        Assert.Throws<ObjectDisposedException>(() => { _ = secure.Span.Length; });
    }

    [Fact]
    public void SecureBuffer_MultipleDisposes_DoesNotThrow()
    {
        var secure = new SecureBuffer(16);
        secure.Dispose();
        secure.Dispose(); // idempotent
    }
}
