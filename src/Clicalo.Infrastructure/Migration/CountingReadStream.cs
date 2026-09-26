namespace Clicalo.Infrastructure.Migration;

/// <summary>
/// A read-only view of a seekable stream that counts the bytes read through it, so <see cref="SafeZipReader"/> knows
/// how much compressed data an entry really consumed instead of trusting its header (MIG-009).
/// </summary>
/// <param name="inner">The archive bytes.</param>
internal sealed class CountingReadStream(Stream inner) : Stream
{
    /// <summary>Bytes read since the last <see cref="ResetCount"/>.</summary>
    public long BytesRead { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => true;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => inner.Length;

    /// <inheritdoc />
    public override long Position
    {
        get => inner.Position;
        set => inner.Position = value;
    }

    /// <summary>Starts counting again from zero.</summary>
    public void ResetCount() => BytesRead = 0;

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) =>
        Count(inner.Read(buffer, offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));

    /// <inheritdoc />
    public override int ReadByte()
    {
        var value = inner.ReadByte();
        if (value >= 0)
        {
            BytesRead++;
        }

        return value;
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

    /// <inheritdoc />
    public override void Flush() { }

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    private int Count(int read)
    {
        BytesRead += read;
        return read;
    }
}
