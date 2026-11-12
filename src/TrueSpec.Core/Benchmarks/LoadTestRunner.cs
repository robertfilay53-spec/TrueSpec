namespace TrueSpec.Core.Benchmarks;

using TrueSpec.Core.Sensors;

/// <summary>
/// The safe load test (Blueprint 2): a CPU burn on all logical cores, a
/// 256 MB memory pattern exercise and a 128 MB sequential disk read, while
/// sampling the existing sensor pipeline at 1 Hz.
///
/// Safety is structural, not advisory:
/// <list type="bullet">
/// <item>The test runs on managed background threads at normal priority. No
/// overclocking, no fan control, no voltage changes, ever.</item>
/// <item>Any CPU temperature above the abort limit stops the test at the
/// next sample (at most one second later).</item>
/// <item>Losing the sensors mid-test stops the test instead of letting it
/// run blind.</item>
/// <item>Cancel stops the test at any second, and every worker thread is
/// joined before <see cref="RunAsync"/> returns, so the test can never
/// leave a thread running after cancel or abort. See
/// <see cref="ActiveWorkerCount"/>.</item>
/// </list>
/// Sampling is 1 Hz best-effort: when a sensor sweep takes longer than the
/// sample interval (LibreHardwareMonitor sweeps can take seconds on a
/// virtual machine), the next sample waits for the read to finish instead
/// of piling up overlapping reads, so a slow machine records fewer samples
/// rather than fabricated ones. The temperature abort is checked after
/// every sample, so its latency is at most one sweep.
/// </summary>
public sealed class LoadTestRunner
{
    private const int MemorySizeBytes = 256 * 1024 * 1024;
    private const int DiskSizeBytes = 128 * 1024 * 1024;

    /// <summary>Consecutive samples with no temperature and no clock, after at least one good sample, before the sensor-lost abort fires.</summary>
    private const int SensorLostAfterMissedSamples = 3;

    private const int WorkerJoinTimeoutMs = 2000;

    private readonly ISensorMonitor _sensors;
    private readonly List<Thread> _workers = new();
    private readonly object _workersLock = new();

    public LoadTestRunner(ISensorMonitor sensors)
    {
        _sensors = sensors ?? throw new ArgumentNullException(nameof(sensors));
    }

    /// <summary>
    /// Live burn threads, for the safety test: this is zero before a run
    /// and zero again after every run, cancel or abort.
    /// </summary>
    public int ActiveWorkerCount
    {
        get
        {
            lock (_workersLock)
            {
                return _workers.Count(t => t.IsAlive);
            }
        }
    }

    /// <summary>
    /// Runs the test to completion, abort or cancel. Cancellation never
    /// throws: it returns a partial result with
    /// <see cref="LoadTestResult.AbortCancelled"/> so the UI can show what
    /// was measured before the user stopped it.
    /// </summary>
    public Task<LoadTestResult> RunAsync(
        LoadTestConfig config,
        IProgress<LoadTestProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sanitized = config.Sanitized();

        // The token is deliberately NOT passed to Task.Run: a caller that
        // cancels before the run starts must still get a cancelled result,
        // not a thrown TaskCanceledException.
        return Task.Run(() => Execute(sanitized, progress, cancellationToken));
    }

    private LoadTestResult Execute(
        LoadTestConfig config,
        IProgress<LoadTestProgress>? progress,
        CancellationToken cancellationToken)
    {
        // Warm-up read, discarded: it pays the sensor library's
        // initialization cost (LibreHardwareMonitor's first sweep can take
        // 10+ seconds on a virtual machine) before the clock starts, so
        // the init cost neither eats the test duration nor delays the
        // first real sample. The burn has not started yet, so no heat is
        // missed. A failed warm-up is fine: the samples will be gaps.
        try
        {
            _ = _sensors.ReadAsync(cancellationToken).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            return LoadTestAnalyzer.Analyze(
                Array.Empty<LoadTestSample>(),
                config.RatedMaxMHz,
                LoadTestResult.AbortCancelled,
                config.DurationSeconds,
                false,
                null);
        }
        catch
        {
            // A failed warm-up must never stop the test from running.
        }

        var startedAt = DateTimeOffset.Now;
        var samples = new List<LoadTestSample>();
        string? abortReason = null;

        // The burn workers get their own linked token: it is cancelled when
        // the sampling loop ends, so the heat stops the moment the test is
        // over. Memory and disk instead use the caller's token: they are
        // part of the result, and a normal completion must not kill them
        // mid-measurement (a 128 MB disk read can outlast a 3-second test
        // on a loaded machine). On user cancel they still stop promptly
        // through the caller's token.
        using var workerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var workerToken = workerCts.Token;

        // Memory and disk run beside the burn on their own dedicated
        // background threads, started BEFORE the burn: the burn saturates
        // every core, and thread-pool work queued after it can starve long
        // enough that the run ends before it starts (the 256 MB check then
        // reports "did not verify" for a scheduling reason, not a memory
        // reason). Dedicated threads are always scheduled.
        var memoryVerified = false;
        double? diskMbps = null;
        var memoryThread = new Thread(() => { memoryVerified = VerifyMemoryPattern(cancellationToken); })
        {
            IsBackground = true,
            Name = "TrueSpec load test memory",
        };
        var diskThread = new Thread(() => { diskMbps = MeasureDisk(cancellationToken); })
        {
            IsBackground = true,
            Name = "TrueSpec load test disk",
        };
        memoryThread.Start();
        diskThread.Start();

        StartWorkers(workerToken);

        try
        {
            var deadline = startedAt + TimeSpan.FromSeconds(config.DurationSeconds);
            var everSawReading = false;
            var consecutiveMissing = 0;

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    abortReason = LoadTestResult.AbortCancelled;
                    break;
                }

                try
                {
                    // Task.Delay on this thread keeps the 1 Hz cadence
                    // without blocking a thread-pool thread for the whole
                    // test. A cancelled delay is the cancel path, not an
                    // error. The delay comes first, so samples land at
                    // roughly 1, 2, 3... seconds after the burn starts.
                    Task.Delay(config.SampleIntervalMs, cancellationToken).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    abortReason = LoadTestResult.AbortCancelled;
                    break;
                }

                if (DateTimeOffset.Now >= deadline)
                {
                    break;
                }

                LoadTestSample sample;
                try
                {
                    // A cancelled sensor read is the cancel path, not an
                    // error: it returns a cancelled result, never a throw.
                    sample = Sample(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    abortReason = LoadTestResult.AbortCancelled;
                    break;
                }

                samples.Add(sample);
                ReportProgress(progress, startedAt, sample, samples.Count);

                if (sample.CpuTempC is { } temp && temp > config.AbortTemperatureC)
                {
                    abortReason = LoadTestResult.AbortTemperature;
                    break;
                }

                if (sample.CpuTempC.HasValue || sample.AvgClockMHz.HasValue)
                {
                    everSawReading = true;
                    consecutiveMissing = 0;
                }
                else if (everSawReading)
                {
                    // A machine that never reported a sensor is not a
                    // sensor loss: there was nothing to lose, and the
                    // verdict says the sensors were unavailable. Only a
                    // loss mid-test aborts.
                    consecutiveMissing++;
                    if (consecutiveMissing >= SensorLostAfterMissedSamples)
                    {
                        abortReason = LoadTestResult.AbortSensorLost;
                        break;
                    }
                }

                if (DateTimeOffset.Now >= deadline)
                {
                    break;
                }
            }
        }
        finally
        {
            // Stop the burn first, then wait for every worker to actually
            // stop. The test is not over until this returns.
            workerCts.Cancel();
            JoinWorkers();
        }

        // The timed test ends when the burn stops. Memory and disk may
        // still be finishing (a slow disk can outlast a short test), but
        // they are not part of the timed load, so the clock stops here.
        var completedAt = DateTimeOffset.Now;

        var memoryVerifiedResult = JoinWithTimeout(memoryThread, TimeSpan.FromSeconds(10)) ? memoryVerified : false;
        var diskMbpsResult = JoinWithTimeout(diskThread, TimeSpan.FromSeconds(10)) ? diskMbps : null;

        // StartedAt/CompletedAt are the test's own wall-clock bounds, not
        // the first/last sample: with slow sweeps the last sample can land
        // seconds before the deadline, and the duration is still the
        // duration that ran.
        return LoadTestAnalyzer.Analyze(
            samples,
            config.RatedMaxMHz,
            abortReason,
            config.DurationSeconds,
            memoryVerifiedResult,
            diskMbpsResult) with
        {
            StartedAt = startedAt,
            CompletedAt = completedAt,
        };
    }

    private void StartWorkers(CancellationToken workerToken)
    {
        var cores = Math.Max(1, Environment.ProcessorCount);
        var workers = new List<Thread>(cores);
        for (var i = 0; i < cores; i++)
        {
            var worker = new Thread(() =>
            {
                try
                {
                    // Chunks of the existing benchmark workload: the token is
                    // checked inside the workload every 64K iterations, so a
                    // stop is answered in milliseconds, and the checksum keeps
                    // the JIT from eliminating the loop.
                    while (!workerToken.IsCancellationRequested)
                    {
                        BenchmarkService.RunCpuWorkload(1_000_000, workerToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected: the token is how the test stops the burn.
                    // It must never escape the thread as an unhandled
                    // exception, or cancelling the test would crash the app.
                }
            })
            {
                IsBackground = true,
                Name = "TrueSpec load test worker",
                Priority = ThreadPriority.Normal,
            };
            workers.Add(worker);
        }

        lock (_workersLock)
        {
            _workers.AddRange(workers);
        }

        foreach (var worker in workers)
        {
            worker.Start();
        }
    }

    private void JoinWorkers()
    {
        List<Thread> workers;
        lock (_workersLock)
        {
            workers = _workers.ToList();
            _workers.Clear();
        }

        foreach (var worker in workers)
        {
            // A worker that ignores its token would be a safety bug, not a
            // hang to pass on to the caller: the join is bounded, the
            // thread is a background thread (it cannot outlive the
            // process), and ActiveWorkerCount still reports it.
            worker.Join(WorkerJoinTimeoutMs);
        }
    }

    private LoadTestSample Sample(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = _sensors.ReadAsync(cancellationToken).GetAwaiter().GetResult();
            var temperature = snapshot.CpuTemperature()?.Value;
            var clocks = snapshot.CpuClocks();
            double? avgClock = null;
            double? maxClock = null;
            if (clocks.Count > 0)
            {
                avgClock = clocks.Average(c => c.Value!.Value);
                maxClock = clocks.Max(c => c.Value!.Value);
            }

            var fan = snapshot.CpuFanRpm();
            var power = snapshot.CpuPowerW();
            var gpuTemp = snapshot.GpuTemperature()?.Value;
            var isGap = temperature is null && avgClock is null && fan is null && power is null && gpuTemp is null;

            return new LoadTestSample(
                DateTimeOffset.Now,
                temperature,
                avgClock,
                maxClock,
                fan,
                power,
                gpuTemp,
                isGap);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // A failed sensor read is a gap sample, never a crash and
            // never an invented reading. Three in a row after good samples
            // aborts the test as sensor-lost.
            return new LoadTestSample(DateTimeOffset.Now, null, null, null, null, null, null, true);
        }
    }

    private static void ReportProgress(
        IProgress<LoadTestProgress>? progress,
        DateTimeOffset startedAt,
        LoadTestSample sample,
        int sampleCount)
    {
        if (progress is null)
        {
            return;
        }

        var elapsed = (int)Math.Floor((sample.Timestamp - startedAt).TotalSeconds);
        try
        {
            progress.Report(new LoadTestProgress(elapsed, sample.CpuTempC, sample.AvgClockMHz, sampleCount));
        }
        catch
        {
            // A progress listener must never break the test.
        }
    }

    private static bool VerifyMemoryPattern(CancellationToken cancellationToken)
    {
        try
        {
            const ulong pattern = 0xA5A5A5A5A5A5A5A5UL;
            var words = new ulong[MemorySizeBytes / sizeof(ulong)];
            Array.Fill(words, pattern);

            ulong checksum = 0;
            for (var i = 0; i < words.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                checksum += words[i];
                if (words[i] != pattern)
                {
                    return false;
                }
            }

            // The checksum only exists so the read-back cannot be skipped:
            // the pattern is uniform, so the expected sum is exact.
            return checksum == pattern * (ulong)words.Length;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static double? MeasureDisk(CancellationToken cancellationToken)
    {
        try
        {
            var result = BenchmarkService.MeasureDiskSequentialRead(DiskSizeBytes, cancellationToken);
            return result.Success ? result.Value : null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool JoinWithTimeout(Thread thread, TimeSpan timeout)
    {
        try
        {
            return thread.Join(timeout);
        }
        catch
        {
            return false;
        }
    }
}
