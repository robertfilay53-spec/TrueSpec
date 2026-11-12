namespace TrueSpec.Core.Benchmarks;

using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;

public sealed record BenchmarkResult(
    string Name,
    bool Success,
    string Summary,
    string Detail,
    string Source,
    double? Value,
    string Unit)
{
    public static BenchmarkResult Failed(string name, string reason, string source) => new(
        name,
        false,
        reason,
        "No number is shown because the measurement did not complete.",
        source,
        null,
        string.Empty);
}

public interface IBenchmarkService
{
    Task<BenchmarkResult> RunCpuSingleThreadAsync(CancellationToken cancellationToken = default);

    Task<BenchmarkResult> RunDiskSequentialReadAsync(CancellationToken cancellationToken = default);

    /// <summary>U10: same xorshift workload as the single-thread test, on all logical cores.</summary>
    Task<BenchmarkResult> RunCpuMultiThreadAsync(CancellationToken cancellationToken = default);

    /// <summary>U10: random 4 KB read/write IOPS, the number that decides how a machine feels.</summary>
    Task<BenchmarkResult> RunDiskRandom4KAsync(CancellationToken cancellationToken = default);

    /// <summary>U10: memory copy bandwidth (Buffer.BlockCopy, 64 MB buffers).</summary>
    Task<BenchmarkResult> RunMemoryCopyAsync(CancellationToken cancellationToken = default);

    /// <summary>U10: disk latency percentiles (p50/p90/p99) of 4 KB random reads under continuous load.</summary>
    Task<BenchmarkResult> RunDiskLatencyAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Real, local measurements. Nothing here is a sample result: the CPU tests run
/// a fixed workload, the disk tests read and write a real temporary file, and
/// the memory test copies real buffers. Results are reported only after the
/// measurement finishes, and every result names its exact workload (threads,
/// block size, queue depth, duration) so two runs are comparable.
///
/// Concurrent load: before each timed workload the service samples total CPU
/// load across all processes (see <see cref="SampleSystemCpuLoadPercent"/>).
/// When the machine was already busy (at or above
/// <see cref="ConcurrentLoadThresholdPercent"/>) the result carries the
/// concurrent-load note, so a run made while another program was working is
/// never silently compared with an idle run. When no load sample could be
/// taken, no note is attached and none is claimed. The load test never gets
/// this note: its own burn workers are the load, so a note there would blame
/// another program for the test itself.
/// </summary>
public sealed class BenchmarkService : IBenchmarkService
{
    private const int CpuIterations = 20_000_000;
    private const int DiskFileSizeBytes = 64 * 1024 * 1024;
    private const int DiskBufferSize = 1024 * 1024;

    // U10: random 4 KB I/O. One synchronous operation at a time (queue depth
    // 1), a fixed seed so the offset sequence is reproducible, and a wall-clock
    // cap per phase so a slow spinning disk cannot stretch the test: the phase
    // reports how many operations actually completed in the time it had.
    private const int RandomBlockBytes = 4 * 1024;
    private const int RandomFileSizeBytes = 64 * 1024 * 1024;
    private const int RandomPhaseSeconds = 2;
    private const int RandomMaxOperations = 100_000;
    private const int RandomSeed = 20261112;

    // U10: memory copy. 64 MB buffers, 8 copies per pass, best of 3 passes.
    private const int MemoryBufferBytes = 64 * 1024 * 1024;
    private const int MemoryCopiesPerPass = 8;
    private const int MemoryPasses = 3;

    private const int CpuMultiThreadRounds = 3;

    /// <summary>
    /// The load above which a run is labelled as made under concurrent load.
    /// A single busy core on a 4-core machine is 25% of total CPU; below this
    /// the machine counts as idle for labelling purposes. This is a labelling
    /// threshold, not a measurement: the sampled load itself is reported in
    /// the note.
    /// </summary>
    public const double ConcurrentLoadThresholdPercent = 25d;

    private readonly Func<double?>? _sampleCpuLoadPercent;

    /// <summary>
    /// <paramref name="sampleCpuLoadPercent"/> supplies the total-CPU-load
    /// sample taken just before each run (percent, 0-100), or null when the
    /// load could not be sampled. Tests pass a fake; the default samples the
    /// real system (all processes, see <see cref="SampleSystemCpuLoadPercent"/>).
    /// </summary>
    public BenchmarkService(Func<double?>? sampleCpuLoadPercent = null)
    {
        _sampleCpuLoadPercent = sampleCpuLoadPercent;
    }

    public Task<BenchmarkResult> RunCpuSingleThreadAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => MeasureCpu(cancellationToken), cancellationToken);

    public Task<BenchmarkResult> RunDiskSequentialReadAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            // The shared static disk measurement takes the note as a
            // parameter so the load test (which calls the same method while
            // its own burn workers saturate the CPU) never gets one.
            var loadNote = SampleLoadNote();
            return MeasureDisk(DiskFileSizeBytes, cancellationToken, loadNote);
        }, cancellationToken);

    public Task<BenchmarkResult> RunCpuMultiThreadAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => MeasureCpuMultiThread(cancellationToken), cancellationToken);

    public Task<BenchmarkResult> RunDiskRandom4KAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => MeasureDiskRandom4K(cancellationToken), cancellationToken);

    public Task<BenchmarkResult> RunMemoryCopyAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => MeasureMemoryCopy(cancellationToken), cancellationToken);

    public Task<BenchmarkResult> RunDiskLatencyAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => MeasureDiskLatency(cancellationToken), cancellationToken);

    /// <summary>
    /// Sequential read of a real temporary file of the given size. Shared by
    /// the disk benchmark (64 MB) and the load test (128 MB) so both measure
    /// the same way. The load test passes no load note: its burn workers are
    /// the load.
    /// </summary>
    public static BenchmarkResult MeasureDiskSequentialRead(int sizeBytes, CancellationToken cancellationToken) =>
        MeasureDisk(sizeBytes, cancellationToken, loadNote: null);

    /// <summary>
    /// The concurrent-load note for a sampled total CPU load, or null when
    /// the machine was idle (below the threshold) or the load was not
    /// sampled. Pure and unit-testable with fake loads; never invents a
    /// load value.
    /// </summary>
    public static string? ConcurrentLoadNote(double? loadPercent) =>
        loadPercent is { } load && double.IsFinite(load) && load >= ConcurrentLoadThresholdPercent
            ? FormattableString.Invariant(
                $"Note: CPU load was {load:0}% just before this run, so another program was using the CPU. This result may be lower than on an idle machine; compare only with runs made on an idle machine.")
            : null;

    /// <summary>
    /// Latency percentiles of a set of per-operation latencies in
    /// milliseconds, by the nearest-rank method. Returns null when there is
    /// no usable latency. Pure and unit-testable with fake latencies.
    /// </summary>
    public static (double P50, double P90, double P99)? LatencyPercentiles(IReadOnlyList<double> latenciesMs)
    {
        if (latenciesMs is null)
        {
            return null;
        }

        var sorted = latenciesMs.Where(v => double.IsFinite(v) && v >= 0d).OrderBy(v => v).ToArray();
        if (sorted.Length == 0)
        {
            return null;
        }

        double Percentile(double p) =>
            sorted[Math.Max(0, (int)Math.Ceiling(p / 100d * sorted.Length) - 1)];

        return (Percentile(50d), Percentile(90d), Percentile(99d));
    }

    /// <summary>
    /// Total CPU load across all processes, sampled over a short window by
    /// differencing every process's TotalProcessorTime. This is the same
    /// quantity Task Manager shows as total CPU: it reflects other programs,
    /// not this process, because this process is idle (sleeping) during the
    /// sample window. Returns null when the load cannot be sampled (process
    /// enumeration denied, no usable processes). No network, no sensor
    /// library, no new dependency; works on Windows, Linux and macOS.
    /// </summary>
    internal static double? SampleSystemCpuLoadPercent()
    {
        try
        {
            var before = SnapshotProcessTimes();
            if (before.Count == 0)
            {
                return null;
            }

            var stopwatch = Stopwatch.StartNew();
            Thread.Sleep(250);
            var after = SnapshotProcessTimes();
            stopwatch.Stop();

            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            if (elapsedMs <= 0d)
            {
                return null;
            }

            var totalCpuMs = 0d;
            foreach (var (id, beforeTime) in before)
            {
                if (after.TryGetValue(id, out var afterTime))
                {
                    totalCpuMs += Math.Max(0d, (afterTime - beforeTime).TotalMilliseconds);
                }
            }

            var percent = totalCpuMs / (elapsedMs * Math.Max(1, Environment.ProcessorCount)) * 100d;
            return double.IsFinite(percent) && percent >= 0d ? percent : null;
        }
        catch
        {
            // A load sample must never stop a benchmark from running.
            return null;
        }
    }

    private static Dictionary<int, TimeSpan> SnapshotProcessTimes()
    {
        var snapshot = new Dictionary<int, TimeSpan>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                snapshot[process.Id] = process.TotalProcessorTime;
            }
            catch
            {
                // A process that exits mid-snapshot, or one this user may
                // not query, is skipped; the sample uses the rest.
            }
            finally
            {
                process.Dispose();
            }
        }

        return snapshot;
    }

    /// <summary>Load note for this run, from the injected sampler or the real system sample.</summary>
    private string? SampleLoadNote()
    {
        try
        {
            var load = _sampleCpuLoadPercent is not null
                ? _sampleCpuLoadPercent()
                : SampleSystemCpuLoadPercent();
            return ConcurrentLoadNote(load);
        }
        catch
        {
            return null;
        }
    }

    private static string WithLoadNote(string detail, string? loadNote) =>
        loadNote is null ? detail : $"{detail} {loadNote}";

    private BenchmarkResult MeasureCpu(CancellationToken cancellationToken)
    {
        var loadNote = SampleLoadNote();
        try
        {
            RunCpuWorkload(1_000_000, cancellationToken);

            var best = double.MaxValue;
            for (var round = 0; round < 3; round++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var stopwatch = Stopwatch.StartNew();
                var checksum = RunCpuWorkload(CpuIterations, cancellationToken);
                stopwatch.Stop();

                if (checksum == long.MinValue)
                {
                    return BenchmarkResult.Failed(
                        "CPU single-thread",
                        "The workload could not be verified.",
                        "Measured on this CPU");
                }

                best = Math.Min(best, stopwatch.Elapsed.TotalSeconds);
            }

            var operationsPerSecond = CpuIterations / best;
            return new BenchmarkResult(
                "CPU single-thread",
                true,
                FormattableString.Invariant($"{operationsPerSecond / 1_000_000d:0.0} million operations per second"),
                WithLoadNote(
                    FormattableString.Invariant(
                        $"Workload: 1 thread, {CpuIterations:N0} xorshift iterations, best of 3 runs, {best * 1000d:0} ms."),
                    loadNote),
                "Measured on this CPU with Stopwatch",
                Math.Round(operationsPerSecond, 0),
                "ops/s");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return BenchmarkResult.Failed("CPU single-thread", $"Measurement failed: {exception.Message}", "Measured on this CPU");
        }
    }

    /// <summary>
    /// U10: the same xorshift workload as the single-thread test, run on all
    /// logical cores at once. The per-thread iteration count is identical, so
    /// the two results are directly comparable: a perfect scale-up would
    /// report N times the single-thread rate for N threads.
    /// </summary>
    private BenchmarkResult MeasureCpuMultiThread(CancellationToken cancellationToken)
    {
        var loadNote = SampleLoadNote();
        var threads = Math.Max(1, Environment.ProcessorCount);
        try
        {
            // Warm-up, discarded: one short run per thread, so JIT cost is
            // paid before the timed rounds.
            RunCpuWorkloadParallel(threads, 1_000_000, cancellationToken);

            var best = double.MaxValue;
            for (var round = 0; round < CpuMultiThreadRounds; round++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var stopwatch = Stopwatch.StartNew();
                var checksums = RunCpuWorkloadParallel(threads, CpuIterations, cancellationToken);
                stopwatch.Stop();

                if (checksums.Any(checksum => checksum == long.MinValue))
                {
                    return BenchmarkResult.Failed(
                        "CPU multi-thread",
                        "The workload could not be verified.",
                        "Measured on this CPU");
                }

                best = Math.Min(best, stopwatch.Elapsed.TotalSeconds);
            }

            var operationsPerSecond = threads * (double)CpuIterations / best;
            return new BenchmarkResult(
                "CPU multi-thread",
                true,
                FormattableString.Invariant($"{operationsPerSecond / 1_000_000d:0.0} million operations per second"),
                WithLoadNote(
                    FormattableString.Invariant(
                        $"Workload: {threads} threads (all logical cores), {CpuIterations:N0} xorshift iterations per thread, best of {CpuMultiThreadRounds} runs, {best * 1000d:0} ms. Same workload as the single-thread test."),
                    loadNote),
                "Measured on this CPU with Stopwatch",
                Math.Round(operationsPerSecond, 0),
                "ops/s");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return BenchmarkResult.Failed("CPU multi-thread", $"Measurement failed: {exception.Message}", "Measured on this CPU");
        }
    }

    private static long[] RunCpuWorkloadParallel(int threads, int iterations, CancellationToken cancellationToken)
    {
        var checksums = new long[threads];
        Parallel.For(
            0,
            threads,
            new ParallelOptions { CancellationToken = cancellationToken },
            i => checksums[i] = RunCpuWorkload(iterations, cancellationToken));
        return checksums;
    }

    /// <summary>
    /// The fixed xorshift workload. Public so the load test can burn the
    /// same verified workload on all cores instead of duplicating it: the
    /// checksum proves the work ran and keeps the JIT from eliminating it.
    /// </summary>
    public static long RunCpuWorkload(int iterations, CancellationToken cancellationToken)
    {
        ulong state = 0x9E3779B97F4A7C15UL;
        var sum = 0UL;
        for (var i = 0; i < iterations; i++)
        {
            if ((i & 0xFFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            state ^= state << 13;
            state ^= state >> 7;
            state ^= state << 17;
            sum += state ^ (ulong)i;
        }

        // The checksum only exists so the JIT cannot eliminate the loop.
        // It must wrap (unchecked): the sum of 20M full-width xorshift
        // values exceeds long.MaxValue after the first iteration, so a
        // checked cast throws OverflowException and the benchmark could
        // never succeed. A wrapped checksum still proves the work ran.
        return unchecked((long)sum);
    }

    /// <summary>
    /// U10: memory copy bandwidth. Two real 64 MB buffers, Buffer.BlockCopy,
    /// 8 copies per pass, best of 3 passes. A sampled byte is verified after
    /// each pass so the copy cannot be eliminated; the reported number is
    /// bytes copied divided by the best pass time.
    /// </summary>
    private BenchmarkResult MeasureMemoryCopy(CancellationToken cancellationToken)
    {
        var loadNote = SampleLoadNote();
        try
        {
            var source = new byte[MemoryBufferBytes];
            var destination = new byte[MemoryBufferBytes];
            RandomNumberGenerator.Fill(source);

            // Warm-up, discarded, and the verification probe: the same index
            // is checked after every timed pass.
            const int probeIndex = 12345;
            Buffer.BlockCopy(source, 0, destination, 0, MemoryBufferBytes);

            var best = double.MaxValue;
            for (var pass = 0; pass < MemoryPasses; pass++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var stopwatch = Stopwatch.StartNew();
                for (var copy = 0; copy < MemoryCopiesPerPass; copy++)
                {
                    Buffer.BlockCopy(source, 0, destination, 0, MemoryBufferBytes);
                }

                stopwatch.Stop();

                if (destination[probeIndex] != source[probeIndex])
                {
                    return BenchmarkResult.Failed(
                        "Memory copy",
                        "The copied data could not be verified.",
                        "Measured with Buffer.BlockCopy");
                }

                best = Math.Min(best, stopwatch.Elapsed.TotalSeconds);
            }

            var bytesPerSecond = MemoryCopiesPerPass * (double)MemoryBufferBytes / best;
            var gigabytesPerSecond = bytesPerSecond / 1_000_000_000d;
            return new BenchmarkResult(
                "Memory copy",
                true,
                FormattableString.Invariant($"{gigabytesPerSecond:0.0} GB/s"),
                WithLoadNote(
                    FormattableString.Invariant(
                        $"Workload: {MemoryBufferBytes / 1024 / 1024} MB buffers, Buffer.BlockCopy, {MemoryCopiesPerPass} copies per pass, best of {MemoryPasses} passes, {best * 1000d:0} ms."),
                    loadNote),
                "Measured with Buffer.BlockCopy and Stopwatch",
                Math.Round(gigabytesPerSecond, 2),
                "GB/s");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return BenchmarkResult.Failed("Memory copy", $"Measurement failed: {exception.Message}", "Measured with Buffer.BlockCopy");
        }
    }

    /// <summary>
    /// U10: random 4 KB disk read/write IOPS. A real 64 MB temporary file,
    /// random offsets from a fixed seed, queue depth 1, up to 2 seconds per
    /// phase. The result names how many operations actually completed in the
    /// time available, so a slow disk reports fewer operations instead of a
    /// stretched-out test.
    /// </summary>
    private BenchmarkResult MeasureDiskRandom4K(CancellationToken cancellationToken)
    {
        const string name = "Disk random 4K";
        const string source = "Measured with a local temporary file";
        var loadNote = SampleLoadNote();
        var path = Path.Combine(Path.GetTempPath(), $"truespec-bench-{Guid.NewGuid():N}.bin");
        try
        {
            var freeSpaceError = CheckTempDriveFreeSpace(RandomFileSizeBytes);
            if (freeSpaceError is not null)
            {
                return BenchmarkResult.Failed(name, freeSpaceError, source);
            }

            WriteRandomTempFile(path, RandomFileSizeBytes, cancellationToken);

            var (readCount, readSeconds) = RunRandomPhase(path, FileAccess.Read, recordLatencies: null, cancellationToken);
            var (writeCount, writeSeconds) = RunRandomPhase(path, FileAccess.Write, recordLatencies: null, cancellationToken);

            if (readCount == 0 || writeCount == 0 || readSeconds <= 0d || writeSeconds <= 0d)
            {
                return BenchmarkResult.Failed(name, "No random 4 KB operation completed.", source);
            }

            var readIops = readCount / readSeconds;
            var writeIops = writeCount / writeSeconds;
            return new BenchmarkResult(
                name,
                true,
                FormattableString.Invariant($"{readIops:0} read IOPS, {writeIops:0} write IOPS"),
                WithLoadNote(
                    FormattableString.Invariant(
                        $"Workload: 4 KB blocks, queue depth 1, {RandomFileSizeBytes / 1024 / 1024} MB file, up to {RandomPhaseSeconds} s per phase: {readCount:N0} random reads in {readSeconds:0.0} s, {writeCount:N0} random writes in {writeSeconds:0.0} s."),
                    loadNote),
                "Measured with FileStream and Stopwatch on a local temporary file",
                Math.Round(readIops, 0),
                "IOPS");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return BenchmarkResult.Failed(name, $"Measurement failed: {exception.Message}", source);
        }
        finally
        {
            DeleteTempFile(path);
        }
    }

    /// <summary>
    /// U10: disk latency percentiles under load. Continuous random 4 KB reads
    /// keep the disk busy for up to 2 seconds (queue depth 1); every read's
    /// latency is recorded and reported as p50/p90/p99. The headline value is
    /// p90, the latency most reads stay under while the disk is loaded.
    /// </summary>
    private BenchmarkResult MeasureDiskLatency(CancellationToken cancellationToken)
    {
        const string name = "Disk latency under load";
        const string source = "Measured with a local temporary file";
        var loadNote = SampleLoadNote();
        var path = Path.Combine(Path.GetTempPath(), $"truespec-bench-{Guid.NewGuid():N}.bin");
        try
        {
            var freeSpaceError = CheckTempDriveFreeSpace(RandomFileSizeBytes);
            if (freeSpaceError is not null)
            {
                return BenchmarkResult.Failed(name, freeSpaceError, source);
            }

            WriteRandomTempFile(path, RandomFileSizeBytes, cancellationToken);

            var latencies = new List<double>();
            var (readCount, readSeconds) = RunRandomPhase(path, FileAccess.Read, latencies, cancellationToken);
            var percentiles = LatencyPercentiles(latencies);

            if (readCount == 0 || percentiles is null)
            {
                return BenchmarkResult.Failed(name, "No random 4 KB read completed, so no latency could be measured.", source);
            }

            var (p50, p90, p99) = percentiles.Value;
            return new BenchmarkResult(
                name,
                true,
                FormattableString.Invariant($"p50 {p50:0.00} ms, p90 {p90:0.00} ms, p99 {p99:0.00} ms"),
                WithLoadNote(
                    FormattableString.Invariant(
                        $"Workload: 4 KB random reads, queue depth 1, {RandomFileSizeBytes / 1024 / 1024} MB file, {readCount:N0} reads in {readSeconds:0.0} s under continuous load."),
                    loadNote),
                "Measured with FileStream and Stopwatch on a local temporary file",
                Math.Round(p90, 2),
                "ms");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return BenchmarkResult.Failed(name, $"Measurement failed: {exception.Message}", source);
        }
        finally
        {
            DeleteTempFile(path);
        }
    }

    /// <summary>
    /// One random 4 KB phase (reads or writes) against an existing temp file.
    /// Returns the operations completed and the seconds spent. Each operation
    /// is a single synchronous seek plus a 4 KB transfer (queue depth 1).
    /// When <paramref name="recordLatencies"/> is supplied, every read's
    /// latency in milliseconds is appended; writes do not record latencies.
    /// </summary>
    private static (long Operations, double Seconds) RunRandomPhase(
        string path,
        FileAccess access,
        List<double>? recordLatencies,
        CancellationToken cancellationToken)
    {
        var blockCount = RandomFileSizeBytes / RandomBlockBytes;
        var random = new Random(RandomSeed);
        var buffer = new byte[RandomBlockBytes];
        if (access == FileAccess.Write)
        {
            RandomNumberGenerator.Fill(buffer);
        }

        var options = access == FileAccess.Write
            ? FileOptions.RandomAccess | FileOptions.WriteThrough
            : FileOptions.RandomAccess;
        using var stream = new FileStream(path, FileMode.Open, access, FileShare.None, RandomBlockBytes, options);

        long operations = 0;
        var stopwatch = Stopwatch.StartNew();
        var deadline = TimeSpan.FromSeconds(RandomPhaseSeconds);
        while (operations < RandomMaxOperations && stopwatch.Elapsed < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var offset = (long)random.Next(0, blockCount) * RandomBlockBytes;
            stream.Seek(offset, SeekOrigin.Begin);

            var operationStopwatch = recordLatencies is not null ? Stopwatch.StartNew() : null;
            if (access == FileAccess.Read)
            {
                stream.ReadExactly(buffer, 0, RandomBlockBytes);
            }
            else
            {
                stream.Write(buffer, 0, RandomBlockBytes);
            }

            if (operationStopwatch is not null)
            {
                operationStopwatch.Stop();
                recordLatencies!.Add(operationStopwatch.Elapsed.TotalMilliseconds);
            }

            operations++;
        }

        stopwatch.Stop();
        return (operations, stopwatch.Elapsed.TotalSeconds);
    }

    /// <summary>Fills a new temporary file with random bytes, for the random I/O tests.</summary>
    private static void WriteRandomTempFile(string path, int sizeBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[DiskBufferSize];
        RandomNumberGenerator.Fill(buffer);

        using var writer = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            DiskBufferSize,
            FileOptions.WriteThrough);
        for (var written = 0; written < sizeBytes; written += DiskBufferSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            writer.Write(buffer, 0, Math.Min(DiskBufferSize, sizeBytes - written));
        }

        writer.Flush(true);
    }

    private static void DeleteTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // A leftover temporary file must never hide the measurement result.
        }
    }

    private static BenchmarkResult MeasureDisk(int sizeBytes, CancellationToken cancellationToken, string? loadNote)
    {
        var path = Path.Combine(Path.GetTempPath(), $"truespec-bench-{Guid.NewGuid():N}.bin");
        try
        {
            // The test writes a temporary file. Check first that the
            // temp drive can hold it: without the check a nearly-full drive
            // fails halfway through the write with a generic IOException,
            // and the user learns nothing. A drive that cannot be checked
            // (unknown root, permission error) falls through to the write,
            // which reports its own failure honestly.
            var freeSpaceError = CheckTempDriveFreeSpace(sizeBytes);
            if (freeSpaceError is not null)
            {
                return BenchmarkResult.Failed("Disk sequential read", freeSpaceError, "Measured with a local temporary file");
            }

            var buffer = new byte[DiskBufferSize];
            RandomNumberGenerator.Fill(buffer);

            using (var writer = new FileStream(
                       path,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       DiskBufferSize,
                       FileOptions.WriteThrough))
            {
                for (var written = 0; written < sizeBytes; written += DiskBufferSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    writer.Write(buffer, 0, Math.Min(DiskBufferSize, sizeBytes - written));
                }

                writer.Flush(true);
            }

            var readBuffer = new byte[DiskBufferSize];
            long totalRead = 0;
            var stopwatch = Stopwatch.StartNew();
            using (var reader = new FileStream(
                       path,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read,
                       DiskBufferSize,
                       FileOptions.SequentialScan))
            {
                int read;
                while ((read = reader.Read(readBuffer, 0, readBuffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    totalRead += read;
                }
            }

            stopwatch.Stop();

            if (totalRead < sizeBytes || stopwatch.Elapsed.TotalSeconds <= 0d)
            {
                return BenchmarkResult.Failed(
                    "Disk sequential read",
                    "The temporary file could not be read completely.",
                    "Measured with a local temporary file");
            }

            var megabytes = totalRead / 1024d / 1024d;
            var megabytesPerSecond = megabytes / stopwatch.Elapsed.TotalSeconds;

            return new BenchmarkResult(
                "Disk sequential read",
                true,
                FormattableString.Invariant($"{megabytesPerSecond:0.0} MB/s"),
                WithLoadNote(
                    FormattableString.Invariant(
                        $"Workload: {megabytes:0} MB read sequentially from a temporary file. OS cache may affect the result."),
                    loadNote),
                "Measured with FileStream and Stopwatch on a local temporary file",
                Math.Round(megabytesPerSecond, 1),
                "MB/s");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return BenchmarkResult.Failed(
                "Disk sequential read",
                $"Measurement failed: {exception.Message}",
                "Measured with a local temporary file");
        }
        finally
        {
            DeleteTempFile(path);
        }
    }

    /// <summary>
    /// The free-space pre-check for <see cref="MeasureDisk"/>, split out so
    /// the decision is unit-testable without a real drive. Returns null
    /// when the drive can hold the test file (or cannot be checked, in
    /// which case the write itself reports any failure); otherwise a
    /// plain-language reason the benchmark did not run.
    /// </summary>
    public static string? FreeSpaceFailure(long availableFreeSpaceBytes) =>
        FreeSpaceFailure(availableFreeSpaceBytes, DiskFileSizeBytes);

    /// <summary>Same check for an arbitrary test-file size (the load test uses 128 MB).</summary>
    public static string? FreeSpaceFailure(long availableFreeSpaceBytes, long requiredBytes) =>
        availableFreeSpaceBytes < requiredBytes
            ? FormattableString.Invariant(
                $"Not enough free space on the temporary drive to run the disk test: it needs {requiredBytes / 1024 / 1024} MB free and {availableFreeSpaceBytes / 1024 / 1024} MB is available. Free some space and try again.")
            : null;

    private static string? CheckTempDriveFreeSpace(long requiredBytes)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetTempPath());
            if (string.IsNullOrEmpty(root))
            {
                return null;
            }

            return FreeSpaceFailure(new DriveInfo(root).AvailableFreeSpace, requiredBytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
