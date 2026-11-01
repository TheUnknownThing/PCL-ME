using System;
using System.IO;
using System.Text;

namespace PCL.Core.Utils;

public class StringStream : Stream
{
    private readonly MemoryStream _innerStream;

    /// <summary>
    /// 使用指定编码初始化 StringStream。
    /// </summary>
    public StringStream(string source, Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        encoding ??= Encoding.UTF8;

        var buffer = encoding.GetBytes(source);
        _innerStream = new MemoryStream(buffer);
    }

    public override bool CanRead  => _innerStream.CanRead;
    public override bool CanSeek  => _innerStream.CanSeek;
    public override bool CanWrite => false;
    public override long Length   => _innerStream.Length;

    public override long Position
    {
        get => _innerStream.Position;
        set => _innerStream.Position = value;
    }

    public override void Flush() { /* No-op for a read-only stream. */ }

    public override int Read(byte[] buffer, int offset, int count) => _innerStream.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);

    public override void SetLength(long value) => throw new NotSupportedException("StringStream is read-only and does not support SetLength.");

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("StringStream is read-only and does not support Write.");

    protected override void Dispose(bool disposing)
    {
        if (disposing) _innerStream.Dispose();
        base.Dispose(disposing);
    }
}
