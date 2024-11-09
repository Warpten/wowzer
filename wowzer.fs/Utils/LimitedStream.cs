using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualBasic;

namespace wowzer.fs.Utils
{
    /// <summary>
    /// A stream that allows for reading from another stream up to a given number of bytes.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="LimitedStream"/> class.
    /// </remarks>
    /// <param name="underlyingStream">The stream to read from.</param>
    /// <param name="length">The number of bytes to read from the parent stream.</param>
    public class LimitedStream(Stream underlyingStream, long length) : Stream
    {
        /// <summary>
        /// The stream to read from.
        /// </summary>
        private readonly Stream _underlyingStream = underlyingStream;

        /// <summary>
        /// The total length of the stream.
        /// </summary>
        private readonly long _length = length;

        /// <summary>
        /// The remaining bytes allowed to be read.
        /// </summary>
        private long _remainingBytes = length;

        /// <inheritdoc />
        public bool IsDisposed { get; private set; }

        /// <inheritdoc />
        public override bool CanRead => !IsDisposed;

        /// <inheritdoc />
        public override bool CanSeek => !IsDisposed && _underlyingStream.CanSeek;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length
        {
            get
            {
                return _underlyingStream.CanSeek ?
                    _length : throw new NotSupportedException();
            }
        }

        /// <inheritdoc />
        public override long Position
        {
            get => _length - _remainingBytes;
            set => Seek(value, SeekOrigin.Begin);
        }

        /// <inheritdoc />
        public override void Flush() => ThrowDisposedOr(new NotSupportedException());

        /// <inheritdoc />
        public override Task FlushAsync(CancellationToken cancellationToken) => throw ThrowDisposedOr(new NotSupportedException());

        /// <inheritdoc />
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (offset + count > buffer.Length)
                throw new ArgumentException();

            count = (int)Math.Min(count, this._remainingBytes);
            if (count <= 0)
                return 0;

            int bytesRead = await _underlyingStream.ReadAsync(buffer, offset, count, cancellationToken);
            _remainingBytes -= bytesRead;
            return bytesRead;
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || count < 0)
                throw new ArgumentOutOfRangeException();

            if (offset + count > buffer.Length)
                throw new ArgumentException();

            count = (int)Math.Min(count, _remainingBytes);
            if (count <= 0)
                return 0;

            int bytesRead = _underlyingStream.Read(buffer, offset, count);
            _remainingBytes -= bytesRead;
            return bytesRead;
        }

        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // If we're beyond the end of the stream (as the result of a Seek operation), return 0 bytes.
            if (_remainingBytes < 0)
                return 0;

            buffer = buffer.Slice(0, (int)Math.Min(buffer.Length, _remainingBytes));
            if (buffer.IsEmpty)
                return 0;

            int bytesRead = await _underlyingStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            _remainingBytes -= bytesRead;
            return bytesRead;
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            if (!CanSeek)
                throw new NotSupportedException();

            // Recalculate offset relative to the current position
            long newOffset = origin switch
            {
                SeekOrigin.Current => offset,
                SeekOrigin.End => _length + offset - Position,
                SeekOrigin.Begin => offset - Position,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };

            // Determine whether the requested position is within the bounds of the stream
            if (Position + newOffset < 0)
            {
                throw new IOException();
            }

            long currentPosition = _underlyingStream.Position;
            long newPosition = _underlyingStream.Seek(newOffset, SeekOrigin.Current);
            _remainingBytes -= newPosition - currentPosition;
            return Position;
        }

        /// <inheritdoc />
        public override void SetLength(long value) => throw ThrowDisposedOr(new NotSupportedException());

        /// <inheritdoc />
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            this.IsDisposed = true;
            base.Dispose(disposing);
        }

        private Exception ThrowDisposedOr(Exception ex)
        {
            throw ex;
        }
    }
}
