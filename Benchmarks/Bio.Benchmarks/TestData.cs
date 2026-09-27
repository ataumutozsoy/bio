using System.Text;

namespace Bio.Benchmarks;

/// <summary>
/// Generates deterministic synthetic FASTA and FASTQ files in memory, so benchmark
/// results do not depend on test data checked into the repository.
/// </summary>
public static class TestData
{
    private const string Nucleotides = "ACGT";

    public static byte[] FastA(int sequenceCount, int sequenceLength, int lineWidth, int seed = 42)
    {
        var random = new Random(seed);
        var builder = new StringBuilder();
        for (int s = 0; s < sequenceCount; s++)
        {
            builder.Append(">seq").Append(s).Append(" synthetic sequence for benchmarking\n");
            for (int i = 0; i < sequenceLength; i++)
            {
                builder.Append(Nucleotides[random.Next(Nucleotides.Length)]);
                if ((i + 1) % lineWidth == 0 || i == sequenceLength - 1)
                {
                    builder.Append('\n');
                }
            }
        }

        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    public static byte[] FastQ(int readCount, int readLength, int seed = 42)
    {
        var random = new Random(seed);
        var builder = new StringBuilder();
        for (int r = 0; r < readCount; r++)
        {
            builder.Append("@read").Append(r).Append(" 1:N:0:ACGTACGT\n");
            for (int i = 0; i < readLength; i++)
            {
                builder.Append(Nucleotides[random.Next(Nucleotides.Length)]);
            }

            builder.Append("\n+\n");
            for (int i = 0; i < readLength; i++)
            {
                // Illumina 1.8+ (Sanger offset) quality characters, Phred 2..41.
                builder.Append((char)('#' + random.Next(40)));
            }

            builder.Append('\n');
        }

        return Encoding.ASCII.GetBytes(builder.ToString());
    }
}
