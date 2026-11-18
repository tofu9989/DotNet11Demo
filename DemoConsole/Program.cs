using System.Text.Json;

Console.WriteLine($"Target framework: net11.0 (C# 15, .NET {Environment.Version})\n");

// ============================================================
// 1. Union types — a value is exactly ONE of the declared cases
// ============================================================
Console.WriteLine("== 1. Union types ==");

ProductLookup lookup1 = new Product(42, "Mechanical keyboard"); // implicit conversion
ProductLookup lookup2 = new ProductNotFound(99);

Print(lookup1);
Print(lookup2);
// The switch below is exhaustive: add a case type to the union and
// the compiler flags every switch that doesn't handle it.

static void Print(ProductLookup result) => Console.WriteLine("  " + result switch
{
    Product p        => $"Found: {p.Name} (id {p.Id})",
    ProductNotFound m => $"No product with ID {m.Id}",
});

// ============================================================
// 2. Closed hierarchies — exhaustive switch, no default arm
// ============================================================
Console.WriteLine("\n== 2. Closed hierarchies ==");

foreach (JobStatus status in new JobStatus[]
{
    new Queued(),
    new Running(65),
    new Completed(TimeSpan.FromSeconds(3.2)),
})
{
    Console.WriteLine("  " + Describe(status));
}

static string Describe(JobStatus status) => status switch
{
    Queued            => "Job is queued",
    Running r         => $"Job is running ({r.PercentComplete}%)",
    Completed c       => $"Job finished in {c.Elapsed.TotalSeconds:F1}s",
    // No default arm needed — the compiler knows every descendant.
};

// ============================================================
// 3. Collection expression arguments — `with(...)` ctor args
// ============================================================
Console.WriteLine("\n== 3. Collection expression arguments ==");

HashSet<string> names = [with(StringComparer.OrdinalIgnoreCase), "Alice", "ALICE", "Bob"];
Console.WriteLine($"  Case-insensitive set count: {names.Count} (expects 2)");

List<int> buffer = [with(capacity: 1024), .. Enumerable.Range(0, 100)];
Console.WriteLine($"  List created via collection expression, count: {buffer.Count}");

// ============================================================
// 4. Extension indexers — index into types you don't own
// ============================================================
Console.WriteLine("\n== 4. Extension indexers ==");

IEnumerable<int> numbers = Enumerable.Range(1, 10);
Console.WriteLine($"  numbers[2] via extension indexer: {numbers[2]} (expects 3)");
Console.WriteLine($"  numbers[^1] via extension indexer: {numbers[^1]} (expects 10)");

// ============================================================
// 5. Labeled break / continue — exit nested loops directly
// ============================================================
Console.WriteLine("\n== 5. Labeled break / continue ==");

outer: for (int i = 1; i <= 5; i++)
{
    for (int j = 1; j <= 5; j++)
    {
        if (i * j > 6)
        {
            Console.WriteLine($"  First pair with i*j > 6: ({i},{j}) — breaking the OUTER loop");
            break outer;
        }
    }
}

// ============================================================
// 6. Bonus: System.Text.Json serializes unions directly
// ============================================================
Console.WriteLine("\n== 6. JSON + union types ==");

string json = JsonSerializer.Serialize<ProductLookup>(lookup1);
Console.WriteLine($"  Serialized union (no wrapper/discriminator): {json}");

// ------------------------------------------------------------
// Type declarations
// ------------------------------------------------------------

// --- Union type (1) ---
public sealed record Product(int Id, string Name);
public sealed record ProductNotFound(int Id);
public union ProductLookup(Product, ProductNotFound);

// --- Closed hierarchy (2) ---
public closed record class JobStatus;
public record class Queued : JobStatus;
public record class Running(int PercentComplete) : JobStatus;
public record class Completed(TimeSpan Elapsed) : JobStatus;

// --- Extension indexer (4) ---
public static class SequenceExtensions
{
    extension(IEnumerable<int> source)
    {
        public int this[Index index] => source.ElementAt(index);
        public int this[int index] => source.ElementAt(index);
    }
}
