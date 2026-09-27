using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;


namespace Bio.IO.FastA
{
    /// <summary>
    /// A FastaParser reads from a source of text that is formatted according to the FASTA flat
    /// file specification and converts the data to in-memory ISequence objects.  For advanced
    /// users, the ability to select an encoding for the internal memory representation is
    /// provided. There is also a default encoding for each alphabet that may be encountered.
    /// Documentation for the latest FastA file format can be found at
    /// http://www.ncbi.nlm.nih.gov/blast/fasta.shtml .
    /// </summary>
    public class FastAParser : ISequenceParser
    {
        /// <summary>
        /// Size of the StreamReader buffer, in characters.
        /// </summary>
        private const int ReaderBufferSize = 64 * 1024;

        /// <summary>
        /// Maximum length of a byte array (Array.MaxLength, which is not available on netstandard2.0).
        /// </summary>
        private const int MaxArrayLength = 0x7FFFFFC7;

        private IAlphabet baseAlphabet;

        /// <summary>
        /// Gets the type of parser.
        /// This is intended to give developers name of the parser.
        /// </summary>
        public string Name => Properties.Resource.FastAName;

        /// <summary>
        /// Gets the description of the parser.
        /// This is intended to give developers some information 
        /// of the parser class. This property returns a simple description of what this
        ///  class achieves.
        /// </summary>
        public string Description => Properties.Resource.FASTAPARSER_DESCRIPTION;

        /// <summary>
        /// Gets a comma separated values of the possible FastA
        /// file extensions.
        /// </summary>
        public string SupportedFileTypes => Properties.Resource.FASTA_FILEEXTENSION;

        /// <summary>
        /// Gets or sets the alphabet to use for parsed ISequence objects.  If this is not set, the alphabet will
        /// be determined based on the file being parsed.
        /// </summary>
        public IAlphabet Alphabet { get; set; }

        /// <summary>
        /// Returns an IEnumerable of sequences in the stream being parsed.
        /// </summary>
        /// <param name="stream">Stream to parse.</param>
        /// <returns>Returns ISequence arrays.</returns>
        public IEnumerable<ISequence> Parse(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            return this.ParseAll(stream);
        }

        /// <summary>
        /// Returns an IEnumerable of sequences in the stream being parsed.
        /// </summary>
        /// <param name="stream">Stream to parse.</param>
        /// <returns>Returns a Sequence.</returns>
        public ISequence ParseOne(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            byte[] buffer = new byte[PlatformManager.Services.DefaultBufferSize];
            return this.ParseOne(new ByteLineReader(stream, ReaderBufferSize), ref buffer);
        }

        /// <summary>
        /// Parses all sequences in the stream.
        /// </summary>
        /// <param name="stream">Stream to parse.</param>
        /// <returns>The sequences.</returns>
        private IEnumerable<ISequence> ParseAll(Stream stream)
        {
            // The buffer is shared by all sequences, so it only grows to the longest one once.
            byte[] buffer = new byte[PlatformManager.Services.DefaultBufferSize];
            var reader = new ByteLineReader(stream, ReaderBufferSize);
            while (!reader.EndOfStream)
            {
                var seq = this.ParseOne(reader, ref buffer);
                if (seq != null)
                    yield return seq;
            }
        }

        /// <summary>
        /// Returns an IEnumerable of sequences in the stream being parsed.
        /// </summary>
        /// <param name="reader">Reader of the stream to parse.</param>
        /// <param name="buffer">Buffer to use; replaced by a larger one if needed.</param>
        /// <returns>Returns a Sequence.</returns>
        ISequence ParseOne(ByteLineReader reader, ref byte[] buffer)
        {
            if (reader.Peek() == -1)
                return null;

            string message;
            int lineStart, lineLength;
            bool hasLine = ReadNonEmptyLine(reader, out lineStart, out lineLength);

            if (!hasLine || reader.Buffer[lineStart] != (byte)'>')
            {
                message = string.Format(
                        CultureInfo.InvariantCulture,
                        Properties.Resource.INVALID_INPUT_FILE,
                        Properties.Resource.FASTA_NAME);

                throw new Exception(message);
            }

            string name = reader.GetString(lineStart + 1, lineLength - 1);
            int bufferPosition = 0;

            // Read next line, skipping blank lines.
            hasLine = ReadNonEmptyLine(reader, out lineStart, out lineLength);

            if (!hasLine)
            {
                message = string.Format(
                    CultureInfo.InvariantCulture,
                    Properties.Resource.InvalidSymbolInString,
                    string.Empty);
                throw new Exception(message);
            }

            IAlphabet alphabet = Alphabet;
            bool tryAutoDetectAlphabet = alphabet == null;

            do
            {
                // Lines with non-ASCII bytes are decoded first, so that each character becomes
                // one byte ('?' if it is not ASCII) exactly as when parsing through a StreamReader.
                string decodedLine = reader.IsAscii(lineStart, lineLength) ? null : reader.GetString(lineStart, lineLength);
                int symbolCount = decodedLine == null ? lineLength : decodedLine.Length;

                // Files > 2G are not supported in this release.
                if ((((long)bufferPosition + symbolCount) >= PlatformManager.Services.MaxSequenceSize))
                {
                    throw new ArgumentOutOfRangeException(
                        string.Format(CultureInfo.CurrentUICulture, Properties.Resource.SequenceDataGreaterthan2GB, name));
                }
                int neededSize = bufferPosition + symbolCount;
                if (neededSize >= buffer.Length)
                {
                    // Grow geometrically so that long sequences are copied O(log n) times rather than O(n),
                    // without exceeding the largest array the runtime can allocate.
                    long suggestedSize = Math.Min(Math.Min(2L * buffer.Length, MaxArrayLength), PlatformManager.Services.MaxSequenceSize);
                    int newSize = (int)Math.Max(neededSize + 1L, suggestedSize);
                    Array.Resize(ref buffer, newSize);
                }

                if (decodedLine == null)
                {
                    System.Buffer.BlockCopy(reader.Buffer, lineStart, buffer, bufferPosition, lineLength);
                }
                else
                {
                    Encoding.ASCII.GetBytes(decodedLine, 0, decodedLine.Length, buffer, bufferPosition);
                }

                // Auto detect alphabet if alphabet is set to null, else validate with already set alphabet
                if (tryAutoDetectAlphabet)
                {
                    // If we have a base alphabet we detected earlier,
                    // then try that first.
                    if (this.baseAlphabet != null &&
                        this.baseAlphabet.ValidateSequence(buffer, bufferPosition, symbolCount))
                    {
                        alphabet = this.baseAlphabet;
                    }
                    // Otherwise attempt to identify alphabet
                    else
                    {
                        // Different alphabet - try to auto detect.
                        this.baseAlphabet = null;
                        alphabet = Alphabets.AutoDetectAlphabet(buffer, bufferPosition, symbolCount, alphabet);
                        if (alphabet == null)
                        {
                            throw new Exception(string.Format(CultureInfo.InvariantCulture,
                                            Properties.Resource.InvalidSymbolInString, decodedLine ?? reader.GetString(lineStart, lineLength)));
                        }
                    }

                    // Determine the base alphabet used.
                    if (this.baseAlphabet == null)
                    {
                        this.baseAlphabet = alphabet;
                    }
                    else
                    {
                        // If they are not the same, then this might be an error.
                        if (this.baseAlphabet != alphabet)
                        {
                            // If the new alphabet includes all the base alphabet then use it instead.
                            // This happens when we hit an ambiguous form of the alphabet later in the file.
                            if (!this.baseAlphabet.HasAmbiguity && Alphabets.GetAmbiguousAlphabet(this.baseAlphabet) == alphabet)
                            {
                                this.baseAlphabet = alphabet;
                            }
                            else if (alphabet.HasAmbiguity || Alphabets.GetAmbiguousAlphabet(alphabet) != this.baseAlphabet)
                            {
                                throw new Exception(Properties.Resource.FastAContainsMorethanOnebaseAlphabet);
                            }
                        }
                    }
                }
                else
                {
                    // Validate against supplied alphabet.
                    if (!alphabet.ValidateSequence(buffer, bufferPosition, symbolCount))
                    {
                        throw new Exception(string.Format(CultureInfo.InvariantCulture, Properties.Resource.InvalidSymbolInString, decodedLine ?? reader.GetString(lineStart, lineLength)));
                    }
                }

                bufferPosition += symbolCount;

                if (reader.Peek() == (byte)'>')
                {
                    break;
                }

                // Read next line.
                hasLine = reader.ReadLine(out lineStart, out lineLength);

                // Continue reading if blank line found.
                while (hasLine && lineLength == 0 && reader.Peek() != (byte)'>')
                {
                    hasLine = reader.ReadLine(out lineStart, out lineLength);
                }
            }
            while (hasLine);

            // Truncate buffer to remove trailing 0's
            byte[] tmpBuffer = new byte[bufferPosition];
            Array.Copy(buffer, tmpBuffer, bufferPosition);

            if (tryAutoDetectAlphabet)
            {
                alphabet = this.baseAlphabet;
            }

            // In memory sequence; tmpBuffer is not used elsewhere, so it is not copied again.
            Sequence sequence = Sequence.CreateWithoutCopy(alphabet, tmpBuffer);
            sequence.ID = name;
            return sequence;
        }

        /// <summary>
        /// Reads the next line that is not empty.
        /// </summary>
        /// <param name="reader">Reader to read from.</param>
        /// <param name="lineStart">Offset of the line in the reader's buffer.</param>
        /// <param name="lineLength">Length of the line.</param>
        /// <returns>False if the end of the stream was reached.</returns>
        private static bool ReadNonEmptyLine(ByteLineReader reader, out int lineStart, out int lineLength)
        {
            bool hasLine = reader.ReadLine(out lineStart, out lineLength);
            while (hasLine && lineLength == 0)
            {
                hasLine = reader.ReadLine(out lineStart, out lineLength);
            }

            return hasLine;
        }
    }
}
