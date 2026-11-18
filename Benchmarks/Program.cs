using System.IO.Compression;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

// Run:  dotnet run -c Release --project Benchmarks -- --filter *
// Quick smoke run (fewer iterations):
//        dotnet run -c Release --project Benchmarks -- --filter * --job short

if (args.Length == 0)
{
    Console.WriteLine("Pass --filter * to run all benchmarks, e.g.:");
    Console.WriteLine("  dotnet run -c Release -- --filter *");
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

// ============================================================
// Old pattern (.NET 10): constructor + collection initializer
// New pattern (.NET 11): collection expression with with(...) args
// ============================================================
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0)]
[SimpleJob(RuntimeMoniker.Net11_0)]
public class HashSetInitBenchmarks
{
    private readonly string[] _items = Enumerable.Range(0, 1_000).Select(i => $"item-{i}").ToArray();

    [Benchmark(Baseline = true)]
    public HashSet<string> Old_ConstructorAndLoop()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in _items) set.Add(item);
        return set;
    }

    [Benchmark]
    public HashSet<string> New_CollectionExpression()
        => [with(StringComparer.OrdinalIgnoreCase), .. _items];
}

// ============================================================
// Compression — same code, two runtimes: is GZip faster on .NET 11?
// ============================================================
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0)]
[SimpleJob(RuntimeMoniker.Net11_0)]
public class CompressionBenchmarks
{
    private byte[] _payload = null!;

    [GlobalSetup]
    public void Setup() => _payload = Encoding.UTF8.GetBytes(
        string.Concat(Enumerable.Repeat("The quick brown fox jumps over the lazy dog. ", 300)));

    [Benchmark(Baseline = true)]
    public byte[] GZip()
    {
        using var output = new MemoryStream();
        using (var gz = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
            gz.Write(_payload, 0, _payload.Length);
        return output.ToArray();
    }
}

// ============================================================
// Old API (.NET 10): GZipStream
// New API (.NET 11): ZstandardStream — net11.0 only, the type doesn't exist on .NET 10
// ============================================================
#if NET11_0_OR_GREATER
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net11_0)]
public class ZstandardVsGzipBenchmarks
{
    private byte[] _payload = null!;

    [GlobalSetup]
    public void Setup() => _payload = Encoding.UTF8.GetBytes(
        string.Concat(Enumerable.Repeat("The quick brown fox jumps over the lazy dog. ", 300)));

    [Benchmark(Baseline = true)]
    public byte[] Old_GZip()
    {
        using var output = new MemoryStream();
        using (var gz = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
            gz.Write(_payload, 0, _payload.Length);
        return output.ToArray();
    }

    [Benchmark]
    public byte[] New_Zstandard()
    {
        using var output = new MemoryStream();
        using (var zs = new ZstandardStream(output, CompressionMode.Compress, leaveOpen: true))
            zs.Write(_payload, 0, _payload.Length);
        return output.ToArray();
    }
}
#endif

// ============================================================
// Old pattern (.NET 10): flag / result variable to leave nested loops
// New pattern (C# 15): labeled break
// ============================================================
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0)]
[SimpleJob(RuntimeMoniker.Net11_0)]
public class NestedLoopExitBenchmarks
{
    [Benchmark(Baseline = true)]
    public (int, int)? Old_Flag()
    {
        (int, int)? found = null;
        for (int i = 1; i <= 100 && found is null; i++)
            for (int j = 1; j <= 100; j++)
                if (i * j > 500) { found = (i, j); break; }
        return found;
    }

    [Benchmark]
    public (int, int)? New_LabeledBreak()
    {
        (int, int)? found = null;
        outer: for (int i = 1; i <= 100; i++)
            for (int j = 1; j <= 100; j++)
                if (i * j > 500) { found = (i, j); break outer; }
        return found;
    }
}

