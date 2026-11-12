using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Core.Realistic;
using Benchmark.Infrastructure;
using System.Text.Json;

namespace Benchmark.Dynamic.Worker;

public sealed class RealisticCaseRunner(BenchmarkRepository repository) {
    public IReadOnlyList<GateEvaluation> Run(
        RealisticSourceKind sourceKind = RealisticSourceKind.Array,
        int count = 256,
        int seed = 42,
        int flags = 15,
        string? caseID = null,
        RealisticDistribution distribution = RealisticDistribution.UniformWithDuplicates,
        CancellationToken cancellationToken = default) {
        
        using var lease = MeasurementLease.Acquire(cancellationToken);
        var environment = EnvironmentSnapshot.Capture("Reliable-v1");
        var runId = Guid.NewGuid().ToString("N");
        var run = new Run(
            runId,
            $"Dynamic.Realistic.{sourceKind}",
            "Running",
            JsonSerializer.Serialize(new { count, seed, flags, caseID, sourceKind = sourceKind.ToString(), distribution = distribution.ToString() }),
            JsonSerializer.Serialize(environment),
            BuildFingerprint.Capture(typeof(RealisticCaseRunner).Assembly.Location),
            DateTimeOffset.UtcNow);

        repository.SaveRun(run);
        var gateResults = new List<GateEvaluation>();

        try {
            foreach (var item in RealisticCaseCatalog.All.Where(item => caseID is null || item.ID == caseID)) {
                SystemResources.Check(true);
                cancellationToken.ThrowIfCancellationRequested();

                var source = RealisticCaseCatalog.Emit(item, sourceKind, flags);
                var compiled = new DynamicQueryCompiler().Compile(
                    new("realistic-" + Guid.NewGuid().ToString("N"), "Benchmark.Dynamic.Generated.RealisticQuery", source, item.ID + "." + sourceKind + ".cs"),
                    cancellationToken);

                if (!compiled.Success) {
                    throw new InvalidOperationException(item.ID + " (" + sourceKind + "): " + string.Join("\n", compiled.Diagnostics.Where(d => d.Severity == "Error").Select(d => d.Message)));
                }

                var loaded = compiled.LoadedQuery!;
                bool executedSuccessfully = false;
                try {
                    ExecuteCase(loaded, item, source, sourceKind, count, seed, distribution, flags, environment, runId, compiled, repository, gateResults, cancellationToken);
                    executedSuccessfully = true;
                } finally {
                    var unloadResult = loaded.UnloadAndVerify();
                    if (executedSuccessfully && !unloadResult.Unloaded) {
                        throw new InvalidOperationException($"Realistic case {item.ID} ({sourceKind}) assembly did not unload.");
                    }
                }
            }

            var allPassed = gateResults.All(g => g.Passed);
            repository.SaveRun(run with { Status = allPassed ? "Passed" : "Failed", CompletedAt = DateTimeOffset.UtcNow });
            return gateResults;
        } catch {
            repository.SaveRun(run with { Status = "Failed", CompletedAt = DateTimeOffset.UtcNow });
            throw;
        }
    }


    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void ExecuteCase(
        LoadedDynamicQuery loaded,
        RealisticQueryCase item,
        string source,
        RealisticSourceKind sourceKind,
        int count,
        int seed,
        RealisticDistribution distribution,
        int flags,
        EnvironmentSnapshot environment,
        string runId,
        DynamicCompilationResult compiled,
        BenchmarkRepository repository,
        List<GateEvaluation> gateResults,
        CancellationToken cancellationToken) {
                    // 入力データの生成
                    var seed256 = Seed256.Parse(seed.ToString());
                    var (primaryObj, secondaryObj) = CreateInputs(item.PrimaryType, item.SecondaryType, sourceKind, count, seed256, distribution);

                    // 1. 結果一致性 (Parity) & フォールバックゼロ検証
                    var expected = loaded.Run(DynamicImplementationKind.System, primaryObj, secondaryObj);
                    foreach (var implementation in new[] { DynamicImplementationKind.FLinqUnoptimized, DynamicImplementationKind.FLinq, DynamicImplementationKind.ZLinq, DynamicImplementationKind.Ideal }.Where(loaded.Has)) {
                        var observation = DynamicFallbackCapture.Run(() => loaded.Run(implementation, primaryObj, secondaryObj));
                        if (implementation is DynamicImplementationKind.FLinqUnoptimized or DynamicImplementationKind.FLinq && observation.FallbackCount != 0) {
                            throw new InvalidDataException($"{item.ID} ({sourceKind}) {implementation}: FLinqRuntimeFallback={observation.FallbackCount}");
                        }
                        if (observation.Exception is { } failure) {
                            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
                        }
                        var actual = observation.Value!;
                        var comparison = DynamicQueryComparer.Compare(expected, actual);
                        if (!comparison.IsMatch) {
                            throw new InvalidDataException($"{item.ID} ({sourceKind}) {implementation} mismatch: {JsonSerializer.Serialize(comparison)}");
                        }
                    }

                    // 2. 計測 (Reliable-v1 プロファイル使用)
                    var engine = new MeasurementEngine(MeasurementProfile.ReliableV1);
                    var measured = engine.Measure(loaded, primaryObj, secondaryObj, 0, 0, cancellationToken);

                    MeasurementSummary? sysSummary = null;
                    MeasurementSummary? flinqSummary = null;
                    MeasurementSummary? idealSummary = null;

                    foreach (var summary in measured.Measurements) {
                        switch (summary.Implementation) {
                            case DynamicImplementationKind.System:
                                sysSummary = summary;
                                break;
                            case DynamicImplementationKind.FLinq:
                                flinqSummary = summary;
                                break;
                            case DynamicImplementationKind.Ideal:
                                idealSummary = summary;
                                break;
                        }

                        var conditions = new Conditions(
                            $"{item.ID}-{sourceKind}",
                            Fingerprint.OfJSON(new { count, seed, distribution = distribution.ToString(), sourceKind = sourceKind.ToString() }),
                            count,
                            item.PrimaryType,
                            "System",
                            sourceKind.ToString(),
                            summary.Implementation.ToString(),
                            flags.ToString(),
                            "{}",
                            "{}",
                            environment.Key,
                            "Reliable-v1",
                            SemanticsVersion: Fingerprint.Of(item.Expression));

                        var samples = summary.Samples.Select(s => new Sample(s.ElapsedNanoseconds, s.OperationCount, s.AllocatedBytes, s.Gen0Collections, s.Gen1Collections, s.Gen2Collections)).ToArray();
                        var measurement = Measurement.Create(Guid.NewGuid().ToString("N"), runId, conditions, Fingerprint.OfJSON(expected), samples) with {
                            DiagnosticsJSON = JsonSerializer.Serialize(new {
                                item.ID,
                                SourceKind = sourceKind.ToString(),
                                Distribution = distribution.ToString(),
                                item.Expression,
                                SourceSHA256 = Fingerprint.Of(source),
                                GeneratedSHA256 = Fingerprint.Of(compiled.ExpandedSource),
                                compiled.ExpandedSource
                            })
                        };
                        repository.SaveMeasurement(measurement);
                    }

                    // 3. 合否判定
                    if (flinqSummary != null && sysSummary != null) {
                        var evaluation = BenchmarkGateEvaluator.Evaluate($"{item.ID}-{sourceKind}", flinqSummary, sysSummary, idealSummary);
                        gateResults.Add(evaluation);
                    }
    }

    private static (object Primary, object? Secondary) CreateInputs(
        string primaryType,
        string? secondaryType,
        RealisticSourceKind kind,
        int count,
        Seed256 seed,
        RealisticDistribution dist) {

        var primary = GenerateObject(primaryType, kind, count, seed, dist);
        object? secondary = null;
        if (secondaryType != null) {
            var secondSeed = SeedDerivation.Derive(seed, "secondary");
            secondary = GenerateObject(secondaryType, kind, count, secondSeed, dist);
        }
        return (primary, secondary);
    }

    private static object GenerateObject(string typeName, RealisticSourceKind kind, int count, Seed256 seed, RealisticDistribution dist) {
        return typeName switch {
            "Trade" => ToSourceKind(RealisticDataGenerator.GenerateTrades(count, seed, dist), kind),
            "Account" => ToSourceKind(RealisticDataGenerator.GenerateAccounts(count, seed, dist), kind),
            "Security" => ToSourceKind(RealisticDataGenerator.GenerateSecurities(count, seed, dist), kind),
            "Customer" => ToSourceKind(RealisticDataGenerator.GenerateCustomers(count, seed, dist), kind),
            "SalesOrder" => ToSourceKind(RealisticDataGenerator.GenerateSalesOrders(count, seed, dist), kind),
            "int" => ToSourceKind(RealisticDataGenerator.GenerateInts(count, seed, dist), kind),
            _ => ToSourceKind(RealisticDataGenerator.GenerateInts(count, seed, dist), kind)
        };
    }

    private static object ToSourceKind<T>(T[] array, RealisticSourceKind kind) {
        return kind switch {
            RealisticSourceKind.Array => array,
            RealisticSourceKind.List => new List<T>(array),
            RealisticSourceKind.IEnumerable => Enumerate(array),
            _ => array
        };
    }

    private static IEnumerable<T> Enumerate<T>(T[] array) {
        for (int i = 0; i < array.Length; i++) {
            yield return array[i];
        }
    }
}
