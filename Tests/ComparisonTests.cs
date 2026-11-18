using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Comparison.Tests;

/// <summary>
/// Correctness baseline for the benchmarks: the ".NET 10 way" and the
/// ".NET 11 way" must produce identical observable results.
/// </summary>
public class EquivalenceTests
{
    // --------------------------------------------------------
    // 1. Result types: union switch == old base-class switch
    // --------------------------------------------------------
    [Theory]
    [InlineData(42, "Found: Product #42 (id 42)")]
    [InlineData(-1, "No product with ID -1")]
    public void Union_switch_matches_old_result_switch(int id, string expected)
    {
        Assert.Equal(expected, DescribeOld(LookupOld(id)));
        Assert.Equal(expected, DescribeNew(LookupNew(id)));
    }

    private static object LookupOld(int id) =>
        id >= 0 ? new ProductOld(id, $"Product #{id}") : new ProductNotFoundOld(id);

    private static string DescribeOld(object result) => result switch
    {
        ProductOld p         => $"Found: {p.Name} (id {p.Id})",
        ProductNotFoundOld m => $"No product with ID {m.Id}",
        _                    => "Unknown result",
    };

    private static ProductLookup LookupNew(int id) =>
        id >= 0 ? new Product(id, $"Product #{id}") : new ProductNotFound(id);

    private static string DescribeNew(ProductLookup result) => result switch
    {
        Product p         => $"Found: {p.Name} (id {p.Id})",
        ProductNotFound m => $"No product with ID {m.Id}",
    };

    // --------------------------------------------------------
    // 2. HashSet init: constructor loop == collection expression
    // --------------------------------------------------------
    [Fact]
    public void HashSet_initialization_styles_are_equivalent()
    {
        string[] items = ["Alice", "ALICE", "Bob", "bob", "Carol"];

        var oldWay = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items) oldWay.Add(item);

        HashSet<string> newWay = [with(StringComparer.OrdinalIgnoreCase), .. items];

        Assert.Equal(oldWay.Count, newWay.Count);
        Assert.Equal(oldWay, newWay);
    }

    // --------------------------------------------------------
    // 3. Nested-loop exit: flag == labeled break
    // --------------------------------------------------------
    [Theory]
    [InlineData(7, 1, 8)]     // first pair with i*j > 7 in a 1..100 grid
    [InlineData(500, 6, 84)]  // 5*100 = 500 is not > 500, so i=6 first, j=84
    public void Labeled_break_finds_same_pair_as_flag(int threshold, int expI, int expJ)
    {
        Assert.Equal(FindWithFlag(threshold), FindWithLabel(threshold));
        Assert.Equal((expI, expJ), FindWithLabel(threshold));
    }

    private static (int, int)? FindWithFlag(int threshold)
    {
        (int, int)? found = null;
        for (int i = 1; i <= 100 && found is null; i++)
            for (int j = 1; j <= 100; j++)
                if (i * j > threshold) { found = (i, j); break; }
        return found;
    }

    private static (int, int)? FindWithLabel(int threshold)
    {
        (int, int)? found = null;
        outer: for (int i = 1; i <= 100; i++)
            for (int j = 1; j <= 100; j++)
                if (i * j > threshold) { found = (i, j); break outer; }
        return found;
    }

    // --------------------------------------------------------
    // 4. Compression: GZip (.NET 10) and Zstandard (.NET 11) both roundtrip
    // --------------------------------------------------------
    [Theory]
    [InlineData(false)] // GZip
    [InlineData(true)]  // Zstandard
    public void Compression_roundtrips_identically(bool useZstd)
    {
        byte[] data = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("payload ", 1_000)));

        byte[] compressed = Compress(data, useZstd);
        byte[] restored = Decompress(compressed, useZstd);

        Assert.Equal(data, restored);
        Assert.True(compressed.Length < data.Length, "repetitive payload should compress");
    }

    [Fact]
    public void Zstandard_compresses_at_least_as_small_as_gzip_on_repetitive_data()
    {
        byte[] data = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("The quick brown fox. ", 500)));
        Assert.True(Compress(data, useZstd: true).Length <= Compress(data, useZstd: false).Length);
    }

    private static byte[] Compress(byte[] input, bool useZstd)
    {
        using var output = new MemoryStream();
        if (useZstd)
        {
            using var z = new ZstandardStream(output, CompressionMode.Compress, leaveOpen: true);
            z.Write(input, 0, input.Length);
        }
        else
        {
            using var g = new GZipStream(output, CompressionMode.Compress, leaveOpen: true);
            g.Write(input, 0, input.Length);
        }
        return output.ToArray();
    }

    private static byte[] Decompress(byte[] input, bool useZstd)
    {
        using var source = new MemoryStream(input);
        using var output = new MemoryStream();
        if (useZstd)
        {
            using var z = new ZstandardStream(source, CompressionMode.Decompress);
            z.CopyTo(output);
        }
        else
        {
            using var g = new GZipStream(source, CompressionMode.Decompress);
            g.CopyTo(output);
        }
        return output.ToArray();
    }

    // --------------------------------------------------------
    // 5. JSON: union serializes to the case type directly
    // --------------------------------------------------------
    [Fact]
    public void Union_serializes_without_wrapper()
    {
        ProductLookup lookup = new Product(42, "Mechanical keyboard");
        string json = JsonSerializer.Serialize(lookup);

        Assert.Equal("""{"Id":42,"Name":"Mechanical keyboard"}""", json);
    }
}

// Local copies of the ".NET 10 way" types (unrelated records + object)
public sealed record ProductOld(int Id, string Name);
public sealed record ProductNotFoundOld(int Id);
