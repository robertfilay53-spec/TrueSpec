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
}

/// <summary>
/// Real, local measurements. Nothing here is a sample result: the CPU test runs
/// a fixed single-threaded workload and the disk test reads a real temporary
/// file. Results are reported only after the measurement finishes.
/// </summary>
public sealed class BenchmarkService : IBenchmarkService
{
    private const int CpuIterations = 20_000_000;
    private const int DiskFileSizeBytes = 64 * 1024 * 1024;
    private const int DiskBufferSize = 1024 * 1024;

    public Task<BenchmarkResult> RunCpuSingleThreadAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => MeasureCpu(cancellationToken), cancellationToken);

    public Task<BenchmarkResult> RunDiskSequentialReadAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => MeasureDisk(cancellationToken), cancellationToken);

    private static BenchmarkResult MeasureCpu(CancellationToken cancellationToken)
    {
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
                FormattableString.Invariant(
                    $"Fixed single-thread workload, best of 3 runs, {best * 1000d:0} ms."),
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

    private static long RunCpuWorkload(int iterations, CancellationToken cancellationToken)
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

    private static BenchmarkResult MeasureDisk(CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), $"truespec-bench-{Guid.NewGuid():N}.bin");
        try
        {
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
                for (var written = 0; written < DiskFileSizeBytes; written += DiskBufferSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    writer.Write(buffer, 0, Math.Min(DiskBufferSize, DiskFileSizeBytes - written));
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

            if (totalRead < DiskFileSizeBytes || stopwatch.Elapsed.TotalSeconds <= 0d)
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
                FormattableString.Invariant(
                    $"{megabytes:0} MB read sequentially from a temporary file. OS cache may affect the result."),
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
    }
}
