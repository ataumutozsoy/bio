using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Bio.IO;
using NUnit.Framework;

namespace Bio.Tests.IO
{
    /// <summary>
    /// Tests that ByteLineReader returns the same lines as StreamReader with byte order mark detection,
    /// which the FASTA and FASTQ parsers used before.
    /// </summary>
    [TestFixture]
    public class ByteLineReaderTests
    {
        private static readonly int[] BufferSizes = { 4, 5, 7, 16, 1024 };

        [TestCase("")]
        [TestCase("a")]
        [TestCase("a\n")]
        [TestCase("a\r\n")]
        [TestCase("a\r")]
        [TestCase("a\n\n")]
        [TestCase("\r\n\r\n")]
        [TestCase("\n\r")]
        [TestCase("a\rb\nc\r\nd")]
        [TestCase("a\r\r\nb")]
        [TestCase(">id\nACGT\r\n>id2\r\nAC\n")]
        public void MatchesStreamReaderForAsciiInput(string text)
        {
            AssertSameAsStreamReader(Encoding.ASCII.GetBytes(text));
        }

        [Test]
        public void MatchesStreamReaderForRandomBytes()
        {
            byte[] symbols = { (byte)'A', (byte)'C', (byte)'>', (byte)' ', (byte)'?', (byte)'\r', (byte)'\n', 0x80, 0xC3, 0xA9, 0xFF };
            var random = new Random(12345);
            for (int i = 0; i < 2000; i++)
            {
                byte[] data = Enumerable.Range(0, random.Next(0, 200))
                    .Select(_ => symbols[random.Next(symbols.Length)])
                    .ToArray();

                if (random.Next(4) == 0)
                {
                    // UTF-8 byte order mark.
                    data = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(data).ToArray();
                }

                AssertSameAsStreamReader(data);
            }
        }

        [Test]
        public void MatchesStreamReaderForUnicodeEncodings()
        {
            Encoding[] encodings =
            {
                new UTF8Encoding(true),
                new UnicodeEncoding(false, true),
                new UnicodeEncoding(true, true),
                new UTF32Encoding(false, true),
                new UTF32Encoding(true, true),
            };
            string[] symbols = { "A", "C", ">", " ", "\r", "\n", "é", "中", "😀" };
            var random = new Random(54321);
            foreach (Encoding encoding in encodings)
            {
                for (int i = 0; i < 300; i++)
                {
                    string text = string.Concat(Enumerable.Range(0, random.Next(0, 100)).Select(_ => symbols[random.Next(symbols.Length)]));
                    byte[] data = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();

                    AssertSameAsStreamReader(data);
                }
            }
        }

        [Test]
        public void ReadsLineLongerThanBuffer()
        {
            string longLine = new string('A', 10000);
            byte[] data = Encoding.ASCII.GetBytes(">id\r\n" + longLine + "\r\nC");

            var reader = new ByteLineReader(new MemoryStream(data), 16);

            Assert.AreEqual(new[] { ">id", longLine, "C" }, ReadAllLines(reader));
        }

        [Test]
        public void DoesNotCloseStream()
        {
            var stream = new MemoryStream(Encoding.ASCII.GetBytes("a\nb"));

            ReadAllLines(new ByteLineReader(stream, 16));

            Assert.IsTrue(stream.CanRead);
        }

        private static void AssertSameAsStreamReader(byte[] data)
        {
            foreach (int bufferSize in BufferSizes)
            {
                var expected = new StreamReader(new MemoryStream(data), Encoding.ASCII, true);
                var actual = new ByteLineReader(new MemoryStream(data), bufferSize);
                string context = string.Format("buffer {0}, input {1}", bufferSize, BitConverter.ToString(data));

                while (true)
                {
                    Assert.AreEqual(expected.EndOfStream, actual.EndOfStream, context);
                    int expectedPeek = expected.Peek();
                    int actualPeek = actual.Peek();
                    Assert.AreEqual(expectedPeek == -1, actualPeek == -1, context);
                    Assert.AreEqual(expectedPeek == '>', actualPeek == '>', context);

                    string expectedLine = expected.ReadLine();
                    int start, length;
                    bool hasLine = actual.ReadLine(out start, out length);
                    Assert.AreEqual(expectedLine != null, hasLine, context);
                    if (expectedLine == null)
                    {
                        break;
                    }

                    Assert.AreEqual(expectedLine, actual.GetString(start, length), context);
                    Assert.AreEqual(Encoding.ASCII.GetBytes(expectedLine), actual.ToAsciiBytes(start, length), context);
                }
            }
        }

        private static List<string> ReadAllLines(ByteLineReader reader)
        {
            var lines = new List<string>();
            int start, length;
            while (reader.ReadLine(out start, out length))
            {
                lines.Add(reader.GetString(start, length));
            }

            return lines;
        }
    }
}
