using BenchmarkDotNet.Attributes;

using Bio.IO.FastA;
using Bio.IO.FastQ;

namespace Bio.Benchmarks;

/// <summary>
/// Measures end-to-end parsing of FASTA and FASTQ data from memory, so disk I/O is excluded.
/// </summary>
[MemoryDiagnoser]
public class ParserBenchmarks
{
    private byte[] _fastALargeGenome = [];
    private byte[] _fastAManyShort = [];
    private byte[] _fastQReads = [];

    [GlobalSetup]
    public void Setup()
    {
        _fastALargeGenome = TestData.FastA(sequenceCount: 1, sequenceLength: 10_000_000, lineWidth: 80);
        _fastAManyShort = TestData.FastA(sequenceCount: 20_000, sequenceLength: 1_000, lineWidth: 60);
        _fastQReads = TestData.FastQ(readCount: 100_000, readLength: 150);
    }

    [Benchmark]
    public long FastA_LargeGenome() => ParseFastA(_fastALargeGenome);

    [Benchmark]
    public long FastA_ManyShortSequences() => ParseFastA(_fastAManyShort);

    [Benchmark]
    public long FastQ_Reads()
    {
        using var stream = new MemoryStream(_fastQReads, writable: false);
        long total = 0;
        foreach (var sequence in new FastQParser().Parse(stream))
        {
            total += sequence.Count;
        }

        return total;
    }

    private static long ParseFastA(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        long total = 0;
        foreach (var sequence in new FastAParser().Parse(stream))
        {
            total += sequence.Count;
        }

        return total;
    }
}
