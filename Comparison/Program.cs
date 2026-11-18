// Comparison: ".NET 10 way (C# 14)" vs ".NET 11 way (C# 15)"
// Both halves compile and run on net11.0 — the difference is the code pattern/API.

using System.IO.Compression;
using System.Text;
using System.Text.Json;

const string payload = "The quick brown fox jumps over the lazy dog. ";

Console.WriteLine("=== .NET 10 (C# 14) way  vs  .NET 11 (C# 15) way ===\n");

// ------------------------------------------------------------
// 1. Result type
//    Old: base class + 'is' checks  -> switch needs a default arm
//    New: union type                -> exhaustive switch
// ------------------------------------------------------------
Console.WriteLine("-- 1. Result types --");
Console.WriteLine($"  Old: {DescribeOld(LookupOld(42))}");
Console.WriteLine($"  New: {DescribeNew(LookupNew(42))}");
Console.WriteLine($"  Old: {DescribeOld(LookupOld(-1))}");
Console.WriteLine($"  New: {DescribeNew(LookupNew(-1))}");

static ProductLookup LookupNew(int id) =>
    id >= 0 ? new Product(id, $"Product #{id}") : new ProductNotFound(id);

static object LookupOld(int id) =>                 // .NET 10: 'object' or base class carries the result
    id >= 0 ? new ProductOld(id, $"Product #{id}") : new ProductNotFoundOld(id);

static string DescribeOld(object result) => result switch
{
    ProductOld p      => $"Found: {p.Name} (id {p.Id})",
    ProductNotFoundOld m => $"No product with ID {m.Id}",
    _ => "Unknown result",                          // required default arm — compiler can't help
};

static string DescribeNew(ProductLookup result) => result switch
{
    Product p        => $"Found: {p.Name} (id {p.Id})",
    ProductNotFound m => $"No product with ID {m.Id}",
    // no default arm — union makes the switch exhaustive
};

// ------------------------------------------------------------
// 2. HashSet initialization
//    Old: constructor + initializer   New: collection expression arguments
// ------------------------------------------------------------
Console.WriteLine("\n-- 2. HashSet with comparer --");

var oldSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Alice", "ALICE", "Bob" };
HashSet<string> newSet = [with(StringComparer.OrdinalIgnoreCase), "Alice", "ALICE", "Bob"];

Console.WriteLine($"  Old: count = {oldSet.Count}");
Console.WriteLine($"  New: count = {newSet.Count}");
Console.WriteLine($"  Same contents? {oldSet.SetEquals(newSet)}");

// ------------------------------------------------------------
// 3. Nested-loop exit
//    Old: bool flag checked in both loops   New: labeled break
// ------------------------------------------------------------
Console.WriteLine("\n-- 3. Exiting nested loops --");
Console.WriteLine($"  Old (flag):        {FindPairWithFlag()}");
Console.WriteLine($"  New (labeled break): {FindPairWithLabel()}");

static (int, int)? FindPairWithFlag()
{
    (int, int)? found = null;                       // .NET 10: flag / result variable
    for (int i = 1; i <= 5 && found is null; i++)
        for (int j = 1; j <= 5; j++)
            if (i * j > 6) { found = (i, j); break; }
    return found;
}

static (int, int)? FindPairWithLabel()
{
    (int, int)? found = null;
    outer: for (int i = 1; i <= 5; i++)             // C# 15: labeled break
        for (int j = 1; j <= 5; j++)
            if (i * j > 6) { found = (i, j); break outer; }
    return found;
}

// ------------------------------------------------------------
// 4. Compression
//    Old: GZipStream   New: ZstandardStream (.NET 11)
// ------------------------------------------------------------
Console.WriteLine("\n-- 4. Compression (same 8 KB payload) --");

byte[] data = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(payload, 170)));

byte[] gzipped = Compress(data, useZstd: false);
byte[] zstd = Compress(data, useZstd: true);

Console.WriteLine($"  Old (GZip):      {data.Length} -> {gzipped.Length} bytes");
Console.WriteLine($"  New (Zstandard): {data.Length} -> {zstd.Length} bytes");

static byte[] Compress(byte[] input, bool useZstd)
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

// ------------------------------------------------------------
// 5. JSON of a result
//    Old: serialize each case manually   New: union serializes directly
// ------------------------------------------------------------
Console.WriteLine("\n-- 5. JSON --");
Console.WriteLine($"  New: {JsonSerializer.Serialize<ProductLookup>(LookupNew(42))}");

// ------------------------------------------------------------
// Type declarations
// ------------------------------------------------------------
public sealed record Product(int Id, string Name);
public sealed record ProductNotFound(int Id);
public union ProductLookup(Product, ProductNotFound);

// .NET 10 style: unrelated types + object, or a shared base class
public sealed record ProductOld(int Id, string Name);
public sealed record ProductNotFoundOld(int Id);
