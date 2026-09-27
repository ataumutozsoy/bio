using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using Bio.Core.Extensions;
using Bio.Properties;

namespace Bio.IO.FastQ
{
    /// <summary>
    ///     A FastQParser reads from a source of text that is formatted according to the FASTQ
    ///     file specification and converts the data to in-memory QualitativeSequence objects.
    /// </summary>
    public class FastQParser : IParserWithAlphabet<IQualitativeSequence>
    {
        /// <summary>
        ///     Size of the StreamReader buffer, in characters.
        /// </summary>
        private const int ReaderBufferSize = 64 * 1024;

        /// <summary>
        ///     Initializes a new instance of the FastQParser class.
        /// </summary>
        public FastQParser()
        {
            this.FormatType = FastQFormatType.Illumina_v1_8;
        }

        /// <summary>
        ///     Gets or sets the format type to be used.
        ///     The FastQFormatType to be used for parsed QualitativeSequence objects.
        ///     Default value is Illumina_v1_8
        /// </summary>
        public FastQFormatType FormatType { get; set; }

        /// <summary>
        ///     Gets the type of parser.
        ///     This is intended to give developers name of the parser.
        /// </summary>
        public virtual string Name
        {
            get
            {
                return Resource.FastQName;
            }
        }

        /// <summary>
        ///     Gets the description of the parser.
        ///     This is intended to give developers some information
        ///     of the parser class. This property returns a simple description of what this
        ///     class achieves.
        /// </summary>
        public virtual string Description
        {
            get
            {
                return Resource.FASTQPARSER_DESCRIPTION;
            }
        }

        /// <summary>
        ///     Gets a comma separated values of the possible FastQ
        ///     file extensions.
        /// </summary>
        public virtual string SupportedFileTypes
        {
            get
            {
                return Resource.FASTQ_FILEEXTENSION;
            }
        }

        /// <summary>
        ///     Gets or sets the alphabet to use for parsed ISequence objects.  If this is not set, the alphabet will
        ///     be determined based on the file being parsed.
        /// </summary>
        public IAlphabet Alphabet { get; set; }

        /// <summary>
        ///     Gets the IEnumerable of QualitativeSequences from the steam being parsed.
        /// </summary>
        /// <param name="stream">Stream to be parsed.</param>
        /// <returns>Returns the QualitativeSequences.</returns>
        public IEnumerable<IQualitativeSequence> Parse(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            return this.ParseAll(stream, this.FormatType);
        }

        /// <summary>
        ///     Returns a single QualitativeSequence from the FASTQ data.
        /// </summary>
        /// <param name="stream">Reader to be parsed.</param>
        /// <returns>Returns a QualitativeSequence.</returns>
        public IQualitativeSequence ParseOne(Stream stream)
        {
            return ParseOne(stream, this.FormatType);
        }

        /// <summary>
        ///     Returns a single QualitativeSequence from the FASTQ data.
        /// </summary>
        /// <param name="stream">Reader to be parsed.</param>
        /// <param name="formatType">FASTQ format type.</param>
        /// <returns>Returns a QualitativeSequence.</returns>
        public IQualitativeSequence ParseOne(Stream stream, FastQFormatType formatType)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            return ParseOne(new ByteLineReader(stream, ReaderBufferSize), formatType);
        }

        /// <summary>
        ///     Parses all QualitativeSequences in the stream.
        /// </summary>
        /// <param name="stream">Stream to be parsed.</param>
        /// <param name="formatType">FASTQ format type.</param>
        /// <returns>Returns the QualitativeSequences.</returns>
        private IEnumerable<IQualitativeSequence> ParseAll(Stream stream, FastQFormatType formatType)
        {
            var reader = new ByteLineReader(stream, ReaderBufferSize);
            while (reader.Peek() != -1)
            {
                IQualitativeSequence seq = ParseOne(reader, formatType);
                if (seq != null)
                {
                    yield return seq;
                }
            }
        }

        /// <summary>
        ///     Returns a single QualitativeSequence from the FASTQ data.
        /// </summary>
        /// <param name="reader">Reader to be parsed.</param>
        /// <param name="formatType">FASTQ format type.</param>
        /// <returns>Returns a QualitativeSequence.</returns>
        private IQualitativeSequence ParseOne(ByteLineReader reader, FastQFormatType formatType)
        {
            if (reader.EndOfStream)
            {
                return null;
            }

            int lineStart, lineLength;
            bool hasLine = ReadNextLine(reader, out lineStart, out lineLength);
            if (!hasLine || reader.Buffer[lineStart] != (byte)'@')
            {
                string message = string.Format(CultureInfo.CurrentCulture, Resource.INVALID_INPUT_FILE, this.Name);
                throw new Exception(message);
            }

            // Process header line.
            string id = reader.GetString(lineStart + 1, lineLength - 1).Trim();

            hasLine = ReadNextLine(reader, out lineStart, out lineLength);
            if (!hasLine)
            {
                string details = string.Format(CultureInfo.CurrentCulture, Resource.FastQ_InvalidSequenceLine, id);
                string message = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.IOFormatErrorMessage,
                    this.Name,
                    details);
                throw new Exception(message);
            }

            // Get sequence from second line.
            byte[] sequenceData = reader.ToAsciiBytes(lineStart, lineLength);

            // Goto third line.
            hasLine = ReadNextLine(reader, out lineStart, out lineLength);

            // Check for '+' symbol in the third line.
            if (!hasLine || reader.Buffer[lineStart] != (byte)'+')
            {
                string details = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.FastQ_InvalidQualityScoreHeaderLine,
                    id);
                string message = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.IOFormatErrorMessage,
                    this.Name,
                    details);
                throw new Exception(message);
            }

            string qualScoreId = reader.GetString(lineStart + 1, lineLength - 1).Trim();

            if (!string.IsNullOrEmpty(qualScoreId) && !id.Equals(qualScoreId))
            {
                string details = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.FastQ_InvalidQualityScoreHeaderData,
                    id);
                string message = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.IOFormatErrorMessage,
                    this.Name,
                    details);
                throw new Exception(message);
            }

            // Goto fourth line.
            hasLine = ReadNextLine(reader, out lineStart, out lineLength);

            if (!hasLine)
            {
                string details = string.Format(CultureInfo.CurrentCulture, Resource.FastQ_EmptyQualityScoreLine, id);
                string message = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.IOFormatErrorMessage,
                    this.Name,
                    details);
                throw new Exception(message);
            }

            // Get the quality scores from the fourth line.
            byte[] qualScores = reader.ToAsciiBytes(lineStart, lineLength);

            // Check for sequence length and quality score length.
            if (sequenceData.GetLongLength() != qualScores.GetLongLength())
            {
                string details = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.FastQ_InvalidQualityScoresLength,
                    id);
                string message = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.IOFormatErrorMessage,
                    this.Name,
                    details);
                throw new Exception(message);
            }

            // Auto detect alphabet if alphabet is set to null, else validate with already set alphabet
            IAlphabet alphabet = this.Alphabet;
            if (alphabet == null)
            {
                alphabet = Alphabets.AutoDetectAlphabet(sequenceData, 0, sequenceData.GetLongLength(), alphabet);
                if (alphabet == null)
                {
                    throw new Exception(Resource.CouldNotIdentifyAlphabetType);
                }
            }
            else
            {
                if (!alphabet.ValidateSequence(sequenceData, 0, sequenceData.GetLongLength()))
                {
                    throw new Exception(Resource.InvalidAlphabetType);
                }
            }

            // sequenceData and qualScores were created for this record only, so they are not copied again.
            QualitativeSequence sequence = QualitativeSequence.CreateWithoutCopy(alphabet, formatType, sequenceData, qualScores);
            sequence.ID = id;
            return sequence;
        }

        /// <summary>
        ///     Reads the next line that is not blank.
        /// </summary>
        /// <param name="reader">Reader to read from.</param>
        /// <param name="lineStart">Offset of the line in the reader's buffer.</param>
        /// <param name="lineLength">Length of the line.</param>
        /// <returns>False if the end of the stream was reached.</returns>
        private static bool ReadNextLine(ByteLineReader reader, out int lineStart, out int lineLength)
        {
            // Continue reading if blank line found.
            bool hasLine = reader.ReadLine(out lineStart, out lineLength);
            while (hasLine && lineLength == 0)
            {
                hasLine = reader.ReadLine(out lineStart, out lineLength);
            }

            return hasLine;
        }
    }
}
