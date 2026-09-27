using System;
using System.IO;
using System.Text;

namespace Bio.IO
{
    /// <summary>
    /// Reads lines from a stream as raw bytes instead of decoding them into strings.
    /// Line breaks ("\n", "\r" and "\r\n") and byte order marks are handled the same way as
    /// <see cref="StreamReader"/> with byte order mark detection handles them, so parsers get the
    /// same lines they would get from <see cref="StreamReader.ReadLine"/>.
    /// </summary>
    /// <remarks>
    /// A line is returned as a range of <see cref="Buffer"/>. The range is only valid until the
    /// next call to <see cref="ReadLine"/>, <see cref="Peek"/> or <see cref="EndOfStream"/>.
    /// Input without a byte order mark is treated as ASCII, and input with a UTF-8 byte order mark
    /// as UTF-8. UTF-16 and UTF-32 input is converted to UTF-8 while it is read.
    /// </remarks>
    internal sealed class ByteLineReader
    {
        /// <summary>
        /// Maximum length of a byte array (Array.MaxLength, which is not available on netstandard2.0).
        /// </summary>
        private const int MaxArrayLength = 0x7FFFFFC7;

        private readonly Stream stream;
        private byte[] buffer;
        private int start;
        private int end;
        private bool endOfStream;

        // The last line ended with '\r'; a '\n' that follows it belongs to the same line break.
        private bool skipLineFeed;

        /// <summary>
        /// Initializes a new instance of the ByteLineReader class.
        /// </summary>
        /// <param name="stream">Stream to read. It is not closed by the reader.</param>
        /// <param name="bufferSize">Initial buffer size in bytes. The buffer grows to hold longer lines.</param>
        public ByteLineReader(Stream stream, int bufferSize)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            this.stream = stream;
            this.buffer = new byte[Math.Max(bufferSize, 4)];

            // Look for a byte order mark, as StreamReader does.
            while (this.end < 4 && this.ReadMore())
            {
            }

            if (this.StartsWith(0xEF, 0xBB, 0xBF))
            {
                this.start = 3;
                this.IsUtf8 = true;
            }
            else if (this.StartsWith(0xFE, 0xFF) || this.StartsWith(0xFF, 0xFE) || this.StartsWith(0x00, 0x00, 0xFE, 0xFF))
            {
                // UTF-16 or UTF-32: let StreamReader decode the text, including the bytes already read,
                // and read the result as UTF-8.
                byte[] prefix = new byte[this.end];
                System.Buffer.BlockCopy(this.buffer, 0, prefix, 0, this.end);
                var textReader = new StreamReader(new PrefixedStream(prefix, stream), Encoding.ASCII, true, this.buffer.Length, true);
                this.stream = new Utf8TranscodingStream(textReader);
                this.start = this.end = 0;
                this.endOfStream = false;
                this.IsUtf8 = true;
            }
        }

        /// <summary>
        /// Gets the buffer that holds the line returned by the last call to <see cref="ReadLine"/>.
        /// </summary>
        public byte[] Buffer
        {
            get { return this.buffer; }
        }

        /// <summary>
        /// Gets a value indicating whether the bytes are UTF-8. Otherwise they are decoded as ASCII.
        /// </summary>
        public bool IsUtf8 { get; private set; }

        /// <summary>
        /// Gets a value indicating whether all data has been read.
        /// </summary>
        public bool EndOfStream
        {
            get { return !this.EnsureData(); }
        }

        /// <summary>
        /// Returns the next byte without consuming it, or -1 at the end of the stream.
        /// </summary>
        /// <returns>The next byte, or -1.</returns>
        public int Peek()
        {
            return this.EnsureData() ? this.buffer[this.start] : -1;
        }

        /// <summary>
        /// Reads the next line. The line break is not included.
        /// </summary>
        /// <param name="lineStart">Offset of the line in <see cref="Buffer"/>.</param>
        /// <param name="lineLength">Length of the line in bytes.</param>
        /// <returns>False if the end of the stream was reached before reading any data.</returns>
        public bool ReadLine(out int lineStart, out int lineLength)
        {
            if (!this.EnsureData())
            {
                lineStart = 0;
                lineLength = 0;
                return false;
            }

            int scanned = 0;
            while (true)
            {
                int index = IndexOfLineBreak(this.buffer, this.start + scanned, this.end - this.start - scanned);
                if (index >= 0)
                {
                    lineStart = this.start;
                    lineLength = index - this.start;
                    this.start = index + 1;
                    this.skipLineFeed = this.buffer[index] == (byte)'\r';
                    return true;
                }

                scanned = this.end - this.start;
                if (!this.ReadMore())
                {
                    // The last line has no line break.
                    lineStart = this.start;
                    lineLength = this.end - this.start;
                    this.start = this.end;
                    return true;
                }
            }
        }

        /// <summary>
        /// Checks whether a range of <see cref="Buffer"/> contains only ASCII bytes.
        /// </summary>
        /// <param name="offset">Offset of the range.</param>
        /// <param name="count">Length of the range.</param>
        /// <returns>True if every byte in the range is below 0x80.</returns>
        public bool IsAscii(int offset, int count)
        {
#if NET8_0_OR_GREATER
            return Ascii.IsValid(this.buffer.AsSpan(offset, count));
#else
            for (int i = offset; i < offset + count; i++)
            {
                if (this.buffer[i] >= 0x80)
                {
                    return false;
                }
            }

            return true;
#endif
        }

        /// <summary>
        /// Decodes a range of <see cref="Buffer"/> the way StreamReader would have decoded it.
        /// </summary>
        /// <param name="offset">Offset of the range.</param>
        /// <param name="count">Length of the range.</param>
        /// <returns>The decoded text.</returns>
        public string GetString(int offset, int count)
        {
            return (this.IsUtf8 ? Encoding.UTF8 : Encoding.ASCII).GetString(this.buffer, offset, count);
        }

        /// <summary>
        /// Returns a range of <see cref="Buffer"/> as a new array of ASCII bytes, the same as
        /// Encoding.ASCII.GetBytes on the line returned by StreamReader: characters that are not
        /// ASCII become '?'.
        /// </summary>
        /// <param name="offset">Offset of the range.</param>
        /// <param name="count">Length of the range.</param>
        /// <returns>A new array.</returns>
        public byte[] ToAsciiBytes(int offset, int count)
        {
            if (!this.IsAscii(offset, count))
            {
                return Encoding.ASCII.GetBytes(this.GetString(offset, count));
            }

            byte[] result = new byte[count];
            System.Buffer.BlockCopy(this.buffer, offset, result, 0, count);
            return result;
        }

        private static int IndexOfLineBreak(byte[] data, int offset, int count)
        {
#if NET8_0_OR_GREATER
            int index = data.AsSpan(offset, count).IndexOfAny((byte)'\n', (byte)'\r');
            return index < 0 ? -1 : offset + index;
#else
            for (int i = offset; i < offset + count; i++)
            {
                byte value = data[i];
                if (value == (byte)'\n' || value == (byte)'\r')
                {
                    return i;
                }
            }

            return -1;
#endif
        }

        private bool StartsWith(params byte[] prefix)
        {
            if (this.end < prefix.Length)
            {
                return false;
            }

            for (int i = 0; i < prefix.Length; i++)
            {
                if (this.buffer[i] != prefix[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Makes sure there is unread data, skipping the second half of a "\r\n" line break.
        /// </summary>
        /// <returns>False at the end of the stream.</returns>
        private bool EnsureData()
        {
            while (true)
            {
                if (this.start == this.end && !this.ReadMore())
                {
                    return false;
                }

                if (this.skipLineFeed)
                {
                    this.skipLineFeed = false;
                    if (this.buffer[this.start] == (byte)'\n')
                    {
                        this.start++;
                        continue;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Reads more data after the unread data, moving the unread data to the start of the buffer
        /// or growing the buffer when it is full.
        /// </summary>
        /// <returns>False if there is no more data in the stream.</returns>
        private bool ReadMore()
        {
            if (this.endOfStream)
            {
                return false;
            }

            if (this.start > 0)
            {
                int unread = this.end - this.start;
                System.Buffer.BlockCopy(this.buffer, this.start, this.buffer, 0, unread);
                this.start = 0;
                this.end = unread;
            }
            else if (this.end == this.buffer.Length)
            {
                if (this.buffer.Length >= MaxArrayLength)
                {
                    throw new InvalidDataException("A line is longer than the maximum supported length.");
                }

                Array.Resize(ref this.buffer, (int)Math.Min(2L * this.buffer.Length, MaxArrayLength));
            }

            int read = this.stream.Read(this.buffer, this.end, this.buffer.Length - this.end);
            if (read == 0)
            {
                this.endOfStream = true;
                return false;
            }

            this.end += read;
            return true;
        }

        /// <summary>
        /// A read-only stream that returns the given bytes and then the rest of another stream.
        /// </summary>
        private sealed class PrefixedStream : Stream
        {
            private readonly byte[] prefix;
            private readonly Stream inner;
            private int prefixPosition;

            public PrefixedStream(byte[] prefix, Stream inner)
            {
                this.prefix = prefix;
                this.inner = inner;
            }

            public override bool CanRead
            {
                get { return true; }
            }

            public override bool CanSeek
            {
                get { return false; }
            }

            public override bool CanWrite
            {
                get { return false; }
            }

            public override long Length
            {
                get { throw new NotSupportedException(); }
            }

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            public override int Read(byte[] destination, int offset, int count)
            {
                if (this.prefixPosition < this.prefix.Length)
                {
                    int copied = Math.Min(count, this.prefix.Length - this.prefixPosition);
                    System.Buffer.BlockCopy(this.prefix, this.prefixPosition, destination, offset, copied);
                    this.prefixPosition += copied;
                    return copied;
                }

                return this.inner.Read(destination, offset, count);
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] source, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// A read-only stream that returns the text of a TextReader encoded as UTF-8.
        /// </summary>
        private sealed class Utf8TranscodingStream : Stream
        {
            private const int CharBufferSize = 4096;

            private readonly TextReader reader;
            private readonly Encoder encoder = new UTF8Encoding(false).GetEncoder();
            private readonly char[] chars = new char[CharBufferSize];
            private readonly byte[] bytes = new byte[Encoding.UTF8.GetMaxByteCount(CharBufferSize)];
            private int bytesStart;
            private int bytesEnd;
            private bool finished;

            public Utf8TranscodingStream(TextReader reader)
            {
                this.reader = reader;
            }

            public override bool CanRead
            {
                get { return true; }
            }

            public override bool CanSeek
            {
                get { return false; }
            }

            public override bool CanWrite
            {
                get { return false; }
            }

            public override long Length
            {
                get { throw new NotSupportedException(); }
            }

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            public override int Read(byte[] destination, int offset, int count)
            {
                while (this.bytesStart == this.bytesEnd)
                {
                    if (this.finished)
                    {
                        return 0;
                    }

                    int charCount = this.reader.Read(this.chars, 0, this.chars.Length);
                    this.finished = charCount == 0;
                    this.bytesStart = 0;
                    this.bytesEnd = this.encoder.GetBytes(this.chars, 0, charCount, this.bytes, 0, this.finished);
                }

                int copied = Math.Min(count, this.bytesEnd - this.bytesStart);
                System.Buffer.BlockCopy(this.bytes, this.bytesStart, destination, offset, copied);
                this.bytesStart += copied;
                return copied;
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] source, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }
}
