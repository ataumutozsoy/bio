using System;
using System.Collections.Generic;
#if NET8_0_OR_GREATER
using System.Buffers;
#endif

namespace Bio
{
    /// <summary>
    /// The set of byte values accepted by an alphabet, optimized for validating sequence data,
    /// which every parser does for every symbol it reads.
    /// </summary>
    internal sealed class AlphabetSymbolSet
    {
        private readonly bool[] contains = new bool[256];

#if NET8_0_OR_GREATER
        // Vectorized search over the same symbols; rebuilt lazily after symbols are added.
        private SearchValues<byte> searchValues;
#endif

        /// <summary>
        /// Adds a symbol to the set.
        /// </summary>
        /// <param name="symbol">Symbol to add.</param>
        public void Add(byte symbol)
        {
            this.contains[symbol] = true;
#if NET8_0_OR_GREATER
            this.searchValues = null;
#endif
        }

        /// <summary>
        /// Checks whether every symbol in the given range is in the set.
        /// The caller is responsible for validating the range.
        /// </summary>
        /// <param name="symbols">Symbols to check.</param>
        /// <param name="offset">Offset of the first symbol to check.</param>
        /// <param name="length">Number of symbols to check.</param>
        /// <returns>True if all symbols in the range are in the set.</returns>
        public bool ContainsAll(byte[] symbols, long offset, long length)
        {
#if NET8_0_OR_GREATER
            SearchValues<byte> values = this.searchValues ??= SearchValues.Create(this.ToArray());
            return symbols.AsSpan(checked((int)offset), checked((int)length)).IndexOfAnyExcept(values) < 0;
#else
            bool[] table = this.contains;
            for (long i = offset; i < offset + length; i++)
            {
                if (!table[symbols[i]])
                {
                    return false;
                }
            }

            return true;
#endif
        }

        private byte[] ToArray()
        {
            var result = new List<byte>();
            for (int i = 0; i < this.contains.Length; i++)
            {
                if (this.contains[i])
                {
                    result.Add((byte)i);
                }
            }

            return result.ToArray();
        }
    }
}
