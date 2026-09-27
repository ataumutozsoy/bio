using System;
using System.Text;
using NUnit.Framework;

namespace Bio.Tests
{
    /// <summary>
    /// Tests IAlphabet.ValidateSequence on a sub-range of a buffer, which is how the
    /// parsers validate each line they read.
    /// </summary>
    [TestFixture]
    public class AlphabetValidateSequenceTests
    {
        // '1' is not a symbol of any of these alphabets.
        private const char InvalidSymbol = '1';

        private static readonly object[] AlphabetsWithValidSymbols =
        {
            new object[] { Alphabets.DNA, "ACGTacgt-" },
            new object[] { Alphabets.RNA, "ACGUacgu-" },
            new object[] { Alphabets.Protein, "ACDEFGHIKLMNOPQRSTUVWYacdefghiklmnopqrstuvwy-*" },
            new object[] { Alphabets.AmbiguousDNA, "ACGTNRYacgtnry-" },
            new object[] { Alphabets.AmbiguousRNA, "ACGUNRYacgunry-" },
            new object[] { Alphabets.AmbiguousProtein, "ACDEFGHIKLMNOPQRSTUVWYXBZJacdxbzj-*" },
        };

        [TestCaseSource(nameof(AlphabetsWithValidSymbols))]
        public void ValidSymbolsInRangeAreAccepted(IAlphabet alphabet, string validSymbols)
        {
            byte[] data = Encoding.ASCII.GetBytes(InvalidSymbol + validSymbols + InvalidSymbol);

            Assert.IsTrue(alphabet.ValidateSequence(data, 1, validSymbols.Length));
        }

        [TestCaseSource(nameof(AlphabetsWithValidSymbols))]
        public void InvalidSymbolInRangeIsRejected(IAlphabet alphabet, string validSymbols)
        {
            // The invalid symbol sits beyond index 'length', so an implementation that compares
            // the index against length instead of offset + length would not see it.
            byte[] data = Encoding.ASCII.GetBytes(validSymbols + validSymbols + InvalidSymbol);

            Assert.IsFalse(alphabet.ValidateSequence(data, validSymbols.Length, validSymbols.Length + 1));
        }

        [TestCaseSource(nameof(AlphabetsWithValidSymbols))]
        public void EmptyRangeIsAccepted(IAlphabet alphabet, string validSymbols)
        {
            byte[] data = Encoding.ASCII.GetBytes(validSymbols + InvalidSymbol);

            Assert.IsTrue(alphabet.ValidateSequence(data, validSymbols.Length, 0));
        }

        [TestCaseSource(nameof(AlphabetsWithValidSymbols))]
        public void RangePastEndOfArrayThrows(IAlphabet alphabet, string validSymbols)
        {
            byte[] data = Encoding.ASCII.GetBytes(validSymbols);

            Assert.Throws<ArgumentOutOfRangeException>(() => alphabet.ValidateSequence(data, 1, validSymbols.Length));
        }
    }
}
