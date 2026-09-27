using System;
using System.IO;
using System.Text;

namespace Bio.Extensions
{
    /// <summary>
    /// Extensions for the Stream
    /// </summary>
    public static class StreamExtensions
    {
        /// <summary>
        /// Opens the given stream for reading with a StreamReader.
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="encoding">Encoding, defaults to UTF8</param>
        /// <param name="detectEncodingFromByteOrderMarks"></param>
        /// <param name="bufferSize">Buffer size, defaults to 1k</param>
        /// <param name="leaveOpen">True to keep underlying stream open on disposal.</param>
        /// <returns>StreamReader</returns>
        public static StreamReader OpenRead(this Stream stream, Encoding encoding = null, bool detectEncodingFromByteOrderMarks = true, int bufferSize = 1024, bool leaveOpen=true)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (encoding == null)
                encoding = Encoding.ASCII;

            return new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks, bufferSize, leaveOpen);
        }

        /// <summary>
        /// Opens the given stream for writing with a StreamWriter.
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="encoding">Encoding, defaults to UTF8</param>
        /// <param name="bufferSize">Buffer size, defaults to 1k</param>
        /// <param name="leaveOpen">True to keep underlying stream open on disposal.</param>
        /// <returns>StreamWriter</returns>
        public static StreamWriter OpenWrite(this Stream stream, Encoding encoding = null, int bufferSize = 1024, bool leaveOpen = true)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (encoding == null)
                encoding = Encoding.ASCII;

            return new StreamWriter(stream, encoding, bufferSize, leaveOpen);
        }

#if NETSTANDARD2_0
        /// <summary>
        /// Reads exactly count bytes from the stream, looping over partial reads.
        /// Polyfill for Stream.ReadExactly, which is available from .NET 7 onwards.
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="buffer">Buffer to fill.</param>
        /// <param name="offset">Offset in the buffer at which to begin storing data.</param>
        /// <param name="count">Number of bytes to read.</param>
        /// <exception cref="EndOfStreamException">The end of the stream is reached before count bytes are read.</exception>
        internal static void ReadExactly(this Stream stream, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                int read = stream.Read(buffer, offset, count);
                if (read == 0)
                    throw new EndOfStreamException();
                offset += read;
                count -= read;
            }
        }
#endif
    }
}
