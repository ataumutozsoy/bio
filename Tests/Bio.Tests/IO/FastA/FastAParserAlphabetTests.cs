using System.IO;
using System.Linq;
using System.Text;
using Bio.Extensions;
using Bio.IO.FastA;
using NUnit.Framework;

namespace Bio.Tests.IO.FastA
{
    /// <summary>
    /// Tests alphabet detection and validation across the lines of multi-line FASTA records.
    /// </summary>
    [TestFixture]
    public class FastAParserAlphabetTests
    {
        [Test]
        public void DetectsAmbiguousDnaWhenAmbiguitySymbolAppearsOnLaterLine()
        {
            ISequence sequence = ParseSingle(">s1\nACGTACGT\nACGTNNNN\n");

            Assert.AreSame(Alphabets.AmbiguousDNA, sequence.Alphabet);
            Assert.AreEqual("ACGTACGTACGTNNNN", sequence.ConvertToString());
        }

        [Test]
        public void ParsesMultiLineRna()
        {
            ISequence sequence = ParseSingle(">r1\nACGUACGU\nUUGGCCAA\n");

            Assert.AreSame(Alphabets.RNA, sequence.Alphabet);
            Assert.AreEqual("ACGUACGUUUGGCCAA", sequence.ConvertToString());
        }

        [Test]
        public void ParsesMultiLineProtein()
        {
            ISequence sequence = ParseSingle(">p1\nMKVLAAGI\nWYHHKQE*\n");

            Assert.AreSame(Alphabets.Protein, sequence.Alphabet);
            Assert.AreEqual("MKVLAAGIWYHHKQE*", sequence.ConvertToString());
        }

        [Test]
        public void RejectsInvalidSymbolOnLaterRnaLine()
        {
            Assert.Catch(() => ParseSingle(">r1\nACGUACGU\nACGU1CGU\n"));
        }

        [Test]
        public void RejectsInvalidSymbolOnLaterProteinLine()
        {
            Assert.Catch(() => ParseSingle(">p1\nMKVLAAGI\nWYHH1QE*\n"));
        }

        [Test]
        public void RejectsInvalidSymbolOnLaterLineWithExplicitProteinAlphabet()
        {
            var parser = new FastAParser { Alphabet = Alphabets.Protein };

            Assert.Catch(() => Parse(parser, ">p1\nMKVLAAGI\nWYHH1QE*\n").ToList());
        }

        private static ISequence ParseSingle(string fasta)
        {
            return Parse(new FastAParser(), fasta).Single();
        }

        private static System.Collections.Generic.IEnumerable<ISequence> Parse(FastAParser parser, string fasta)
        {
            using (var stream = new MemoryStream(Encoding.ASCII.GetBytes(fasta)))
            {
                foreach (ISequence sequence in parser.Parse(stream))
                {
                    yield return sequence;
                }
            }
        }
    }
}
