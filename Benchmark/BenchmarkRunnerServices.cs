using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Benchmark;

public interface IBenchmarkCatalogService {
    Task<IReadOnlyList<BenchmarkCase>> LoadAsync(CancellationToken cancellationToken);
}

public sealed class BenchmarkCatalogService:IBenchmarkCatalogService {
    public Task<IReadOnlyList<BenchmarkCase>> LoadAsync(CancellationToken cancellationToken) {
        return Task.Run(() => {
            cancellationToken.ThrowIfCancellationRequested();
            return BenchmarkCatalog.Load();
        },cancellationToken);
    }
}

public interface ICpuTopologyService {
    IReadOnlyList<CpuSelectionItem> Load();
}

public sealed class CpuTopologyService:ICpuTopologyService {
    public IReadOnlyList<CpuSelectionItem> Load() {
        return BenchmarkCpuTopologyProvider.Load()
            .OrderBy(static cpu => cpu.LogicalProcessorIndex)
            .Select((cpu,index) => new CpuSelectionItem(
                cpu.LogicalProcessorIndex,
                cpu.CoreKind,
                cpu.PhysicalCoreIndex,
                cpu.KindCoreIndex,
                index==0))
            .ToArray();
    }
}

internal static class BenchmarkCpuTopologyProvider {
    public static IReadOnlyList<CpuCoreInfo> Load() {
        try {
            if(OperatingSystem.IsWindows()) {
                var windows = LoadWindows();
                if(windows.Count>0) {
                    return windows;
                }
            } else if(OperatingSystem.IsLinux()) {
                var linux = LoadLinux();
                if(linux.Count>0) {
                    return linux;
                }
            } else if(OperatingSystem.IsMacOS()||OperatingSystem.IsMacCatalyst()) {
                return LoadMacOs();
            }
        } catch {
        }

        return LoadFallback(OperatingSystem.IsWindows()||OperatingSystem.IsLinux());
    }

    private static IReadOnlyList<CpuCoreInfo> LoadFallback(bool supportsAffinity) {
        return Enumerable.Range(0,Environment.ProcessorCount)
            .Select(index => CreateCpu(index,CpuCoreKind.Unknown,index,index,supportsAffinity))
            .ToArray();
    }

    private static IReadOnlyList<CpuCoreInfo> LoadWindows() {
        var length = 0;
        _ = GetLogicalProcessorInformationEx(RelationProcessorCore,IntPtr.Zero,ref length);
        if(length<=0) {
            return [];
        }

        var buffer = Marshal.AllocHGlobal(length);
        try {
            if(!GetLogicalProcessorInformationEx(RelationProcessorCore,buffer,ref length)) {
                return [];
            }

            var cores = new List<WindowsCore>();
            var offset = 0;
            while(offset<length) {
                var entry = IntPtr.Add(buffer,offset);
                var size = Marshal.ReadInt32(entry,4);
                if(size<=0) {
                    break;
                }

                var efficiencyClass = Marshal.ReadByte(entry,9);
                var groupCount = Marshal.ReadInt16(entry,30);
                var logicalProcessors = new List<int>();
                var groupAffinityOffset = 32;
                var groupAffinitySize = IntPtr.Size+8;
                for(var groupIndex=0;groupIndex<groupCount;groupIndex++) {
                    var affinity = IntPtr.Add(entry,groupAffinityOffset+(groupIndex*groupAffinitySize));
                    var mask = ReadUIntPtr(affinity);
                    var group = Marshal.ReadInt16(affinity,IntPtr.Size);
                    if(group<0) {
                        continue;
                    }

                    AddMaskBits(logicalProcessors,group*64,mask);
                }

                if(logicalProcessors.Count>0) {
                    cores.Add(new WindowsCore(efficiencyClass,logicalProcessors.Order().ToArray()));
                }

                offset+=size;
            }

            return CreateFromCoreGroups(cores.Select(static core => new CoreGroup(
                core.LogicalProcessors,
                core.EfficiencyClass))).ToArray();
        } finally {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static IReadOnlyList<CpuCoreInfo> LoadLinux() {
        const string cpuRoot = "/sys/devices/system/cpu";
        var online = ReadLinuxOnlineCpuList(Path.Combine(cpuRoot,"online"));
        if(online.Count==0) {
            online=Enumerable.Range(0,Environment.ProcessorCount).ToArray();
        }

        var groups = online
            .Select(cpu => new {
                Cpu=cpu,
                CoreKey=ReadLinuxCoreKey(cpuRoot,cpu),
                Capacity=ReadLinuxInt(Path.Combine(cpuRoot,$"cpu{cpu}","cpu_capacity")),
            })
            .GroupBy(static item => item.CoreKey)
            .Select(static group => new CoreGroup(
                group.Select(static item => item.Cpu).Order().ToArray(),
                group.Max(static item => item.Capacity)))
            .ToArray();

        return CreateFromCoreGroups(groups).ToArray();
    }

    private static IReadOnlyList<CpuCoreInfo> LoadMacOs() {
        var logicalCount = ReadSysctlInt("hw.logicalcpu")??Environment.ProcessorCount;
        var performanceCount = ReadSysctlInt("hw.perflevel0.logicalcpu");
        var efficiencyCount = ReadSysctlInt("hw.perflevel1.logicalcpu");
        var items = new List<CpuCoreInfo>(logicalCount);
        var index = 0;

        if(performanceCount is >0) {
            for(var i=0;i<performanceCount&&index<logicalCount;i++,index++) {
                items.Add(CreateCpu(index,CpuCoreKind.Performance,index,i,false));
            }
        }

        if(efficiencyCount is >0) {
            for(var i=0;i<efficiencyCount&&index<logicalCount;i++,index++) {
                items.Add(CreateCpu(index,CpuCoreKind.Efficiency,index,i,false));
            }
        }

        while(index<logicalCount) {
            items.Add(CreateCpu(index,CpuCoreKind.Unknown,index,index,false));
            index++;
        }

        return items;
    }

    private static IEnumerable<CpuCoreInfo> CreateFromCoreGroups(IEnumerable<CoreGroup> rawGroups) {
        var groups = rawGroups
            .Where(static group => group.LogicalProcessors.Count>0)
            .OrderBy(static group => group.LogicalProcessors.Min())
            .ToArray();
        if(groups.Length==0) {
            yield break;
        }

        var knownScores = groups
            .Select(static group => group.KindScore)
            .Where(static score => score.HasValue)
            .Select(static score => score!.Value)
            .Distinct()
            .Order()
            .ToArray();
        var hasHybridScores = knownScores.Length>1;
        var maxScore = hasHybridScores?knownScores[^1]:(int?)null;
        var minScore = hasHybridScores?knownScores[0]:(int?)null;
        if(hasHybridScores) {
            groups=MergeSingletonEfficiencyGroups(groups,minScore);
        }

        var nextKindIndex = new Dictionary<CpuCoreKind,int>();
        for(var physicalIndex=0;physicalIndex<groups.Length;physicalIndex++) {
            var group = groups[physicalIndex];
            var kind = CpuCoreKind.Unknown;
            if(hasHybridScores&&group.KindScore==maxScore) {
                kind=CpuCoreKind.Performance;
            } else if(hasHybridScores&&group.KindScore==minScore) {
                kind=CpuCoreKind.Efficiency;
            }

            var kindIndex = nextKindIndex.TryGetValue(kind,out var current) ? current : 0;
            nextKindIndex[kind]=kindIndex+1;
            foreach(var logicalProcessor in group.LogicalProcessors.Order()) {
                yield return CreateCpu(logicalProcessor,kind,physicalIndex,kindIndex,CanUseAffinity(logicalProcessor));
            }
        }
    }

    private static CoreGroup[] MergeSingletonEfficiencyGroups(CoreGroup[] groups,int? efficiencyScore) {
        if(efficiencyScore is null) {
            return groups;
        }

        var merged = new List<CoreGroup>(groups.Length);
        for(var index=0;index<groups.Length;) {
            var group = groups[index];
            if(group.KindScore!=efficiencyScore||group.LogicalProcessors.Count!=1) {
                merged.Add(group);
                index++;
                continue;
            }

            var run = new List<CoreGroup>();
            while(index<groups.Length
                &&groups[index].KindScore==efficiencyScore
                &&groups[index].LogicalProcessors.Count==1) {
                run.Add(groups[index]);
                index++;
            }

            for(var runIndex=0;runIndex<run.Count;runIndex+=4) {
                merged.Add(new CoreGroup(
                    run
                        .Skip(runIndex)
                        .Take(4)
                        .SelectMany(static item => item.LogicalProcessors)
                        .Order()
                        .ToArray(),
                    efficiencyScore));
            }
        }

        return merged.ToArray();
    }

    private static CpuCoreInfo CreateCpu(int logicalProcessorIndex,CpuCoreKind kind,int physicalCoreIndex,int kindCoreIndex,bool supportsAffinity) {
        var canUseAffinity = supportsAffinity&&CanUseAffinity(logicalProcessorIndex);
        var mask = canUseAffinity ? 1L<<logicalProcessorIndex : 0L;
        return new CpuCoreInfo(logicalProcessorIndex,kind,physicalCoreIndex,kindCoreIndex,mask,canUseAffinity);
    }

    private static bool CanUseAffinity(int logicalProcessorIndex) {
        return logicalProcessorIndex>=0&&logicalProcessorIndex<(IntPtr.Size*8)-1;
    }

    private static UIntPtr ReadUIntPtr(IntPtr address) {
        return IntPtr.Size==8
            ? new UIntPtr(unchecked((ulong)Marshal.ReadInt64(address)))
            : new UIntPtr(unchecked((uint)Marshal.ReadInt32(address)));
    }

    private static void AddMaskBits(List<int> logicalProcessors,int baseIndex,UIntPtr mask) {
        var value = mask.ToUInt64();
        for(var bit=0;bit<64;bit++) {
            if((value&(1UL<<bit))!=0) {
                logicalProcessors.Add(baseIndex+bit);
            }
        }
    }

    private static IReadOnlyList<int> ReadLinuxOnlineCpuList(string path) {
        if(!File.Exists(path)) {
            return [];
        }

        var text = File.ReadAllText(path).Trim();
        var result = new List<int>();
        foreach(var part in text.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)) {
            var range = part.Split('-',StringSplitOptions.TrimEntries);
            if(range.Length==2
                &&int.TryParse(range[0],NumberStyles.Integer,CultureInfo.InvariantCulture,out var start)
                &&int.TryParse(range[1],NumberStyles.Integer,CultureInfo.InvariantCulture,out var end)) {
                for(var cpu=start;cpu<=end;cpu++) {
                    result.Add(cpu);
                }
            } else if(int.TryParse(part,NumberStyles.Integer,CultureInfo.InvariantCulture,out var single)) {
                result.Add(single);
            }
        }

        return result.Order().Distinct().ToArray();
    }

    private static string ReadLinuxCoreKey(string cpuRoot,int cpu) {
        var topology = Path.Combine(cpuRoot,$"cpu{cpu}","topology");
        var packageId = ReadLinuxString(Path.Combine(topology,"physical_package_id"))??"0";
        var coreId = ReadLinuxString(Path.Combine(topology,"core_id"))??cpu.ToString(CultureInfo.InvariantCulture);
        return packageId+":"+coreId;
    }

    private static string? ReadLinuxString(string path) {
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    private static int? ReadLinuxInt(string path) {
        var text = ReadLinuxString(path);
        return int.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out var value)
            ? value
            : null;
    }

    private static int? ReadSysctlInt(string key) {
        try {
            using var process = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName="sysctl",
                    UseShellExecute=false,
                    RedirectStandardOutput=true,
                    RedirectStandardError=true,
                    CreateNoWindow=true,
                },
            };
            process.StartInfo.ArgumentList.Add("-n");
            process.StartInfo.ArgumentList.Add(key);
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(2000);
            return process.ExitCode==0&&int.TryParse(output.Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out var value)
                ? value
                : null;
        } catch {
            return null;
        }
    }

    [DllImport("kernel32.dll",SetLastError=true)]
    private static extern bool GetLogicalProcessorInformationEx(int relationshipType,IntPtr buffer,ref int returnedLength);

    private const int RelationProcessorCore = 0;

    private sealed record WindowsCore(byte EfficiencyClass,IReadOnlyList<int> LogicalProcessors);
    private sealed record CoreGroup(IReadOnlyList<int> LogicalProcessors,int? KindScore);
}

public interface IBenchmarkQueueService {
    ConcurrentQueue<BenchmarkCase> CreateQueue(IEnumerable<BenchmarkCase> benchmarks);
}

public sealed class BenchmarkQueueService:IBenchmarkQueueService {
    public ConcurrentQueue<BenchmarkCase> CreateQueue(IEnumerable<BenchmarkCase> benchmarks) {
        return new ConcurrentQueue<BenchmarkCase>(benchmarks);
    }
}

public interface IBenchmarkWorkerProcessService:IBenchmarkJobRunner {
    Task<BenchmarkRunResult> RunAsync(BenchmarkCase benchmark,string runRoot,BenchmarkSourceTab sourceTab,IReadOnlyList<int> preparedElementCounts,string scale,int logicalCpu,CancellationToken cancellationToken);
}

public sealed class BenchmarkWorkerProcessService:IBenchmarkWorkerProcessService {
    private readonly BenchmarkWorkerClient client = new();

    public Task<BenchmarkJobRunResult> RunAsync(BenchmarkJob job,string runRoot,int logicalCpu,TimeSpan timeout,CancellationToken cancellationToken) =>
        client.RunAsync(job,runRoot,logicalCpu,timeout,cancellationToken);

    public async Task<BenchmarkRunResult> RunAsync(BenchmarkCase benchmark,string runRoot,BenchmarkSourceTab sourceTab,IReadOnlyList<int> preparedElementCounts,string scale,int logicalCpu,CancellationToken cancellationToken) {
        var result = await client.RunAsync(benchmark,runRoot,sourceTab,preparedElementCounts,0,scale,logicalCpu,TimeSpan.FromMinutes(60),cancellationToken);
        return new BenchmarkRunResult(
            result.Project,
            result.Method,
            result.TypeFullName,
            result.Status,
            result.Mean,
            result.Error,
            result.StdDev,
            result.MeanNanoseconds,
            result.ErrorNanoseconds,
            result.StdDevNanoseconds,
            result.AllocatedBytes,
            result.ExitCode,
            result.ArtifactFolder,
            result.Log);
    }
}

public sealed class BenchmarkPersistentWorkerProcessService:IBenchmarkJobRunner,IBenchmarkDynamicCpuSetRunner,IAsyncDisposable {
    private readonly BenchmarkWorkerClient client = new();
    private readonly ConcurrentDictionary<int,BenchmarkWorkerSession> sessions = new();
    private readonly ConcurrentDictionary<int,int> activeJobs = new();

    public async Task<BenchmarkJobRunResult> RunAsync(
        BenchmarkJob job,
        string runRoot,
        int logicalCpu,
        TimeSpan timeout,
        CancellationToken cancellationToken) {
        var session = await this.GetSessionAsync(runRoot,logicalCpu,cancellationToken);
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,timeoutSource.Token);
        this.activeJobs.AddOrUpdate(logicalCpu,1,static (_,count) => count+1);
        try {
            return await session.RunAsync(job.ToBenchmarkCase(),job.SourceTab,job.PreparedElementCounts,job.ElementCount,"1",linkedSource.Token);
        } catch(OperationCanceledException) {
            await this.ResetSessionAsync(logicalCpu,session);
            if(timeoutSource.IsCancellationRequested&&!cancellationToken.IsCancellationRequested) {
                throw new TimeoutException($"Benchmark job timed out after {timeout}.");
            }

            throw;
        } catch {
            await this.ResetSessionAsync(logicalCpu,session);
            throw;
        } finally {
            this.activeJobs.AddOrUpdate(logicalCpu,0,static (_,count) => Math.Max(0,count-1));
        }
    }

    public async Task ApplyCpuSetAsync(IReadOnlyList<int> logicalCpus) {
        var active = logicalCpus.ToHashSet();
        foreach(var pair in this.sessions.ToArray()) {
            if(active.Contains(pair.Key)||this.activeJobs.GetValueOrDefault(pair.Key)>0) {
                continue;
            }

            await this.ResetSessionAsync(pair.Key,pair.Value);
        }
    }

    public async ValueTask DisposeAsync() {
        var values = this.sessions.Values.ToArray();
        this.sessions.Clear();
        foreach(var session in values) {
            await session.DisposeAsync();
        }
    }

    private async Task<BenchmarkWorkerSession> GetSessionAsync(
        string runRoot,
        int logicalCpu,
        CancellationToken cancellationToken) {
        if(this.sessions.TryGetValue(logicalCpu,out var existing)) {
            return existing;
        }

        var created = await this.client.StartSessionAsync(runRoot,logicalCpu,cancellationToken);
        if(this.sessions.TryAdd(logicalCpu,created)) {
            return created;
        }

        await created.DisposeAsync();
        return this.sessions[logicalCpu];
    }

    private async Task ResetSessionAsync(int logicalCpu,BenchmarkWorkerSession session) {
        if(this.sessions.TryGetValue(logicalCpu,out var current)
            &&ReferenceEquals(current,session)
            &&this.sessions.TryRemove(logicalCpu,out _)) {
            await session.DisposeAsync();
        }
    }
}

public sealed class BenchmarkInProcessJobRunner:IBenchmarkJobRunner {
    public Task<BenchmarkJobRunResult> RunAsync(
        BenchmarkJob job,
        string runRoot,
        int logicalCpu,
        TimeSpan timeout,
        CancellationToken cancellationToken) {
        if(cancellationToken.IsCancellationRequested) {
            return Task.FromCanceled<BenchmarkJobRunResult>(cancellationToken);
        }

        return Task.Run(() => Run(job,runRoot));
    }

    private static BenchmarkJobRunResult Run(
        BenchmarkJob job,
        string runRoot) {
        var benchmark = job.ToBenchmarkCase();
        var artifactFolder = BenchmarkWorkerClient.CreateArtifactFolder(runRoot,benchmark,job.SourceTab,job.ElementCount);
        var logPath = Path.Combine(artifactFolder,"inprocess.log");
        object? instance = null;
        try {
            var assembly = ResolveBenchmarkAssembly(benchmark.AssemblyName);
            var type = assembly.GetType(benchmark.TypeFullName,throwOnError:true,ignoreCase:false)
                ??throw new InvalidOperationException($"Benchmark type was not found: {benchmark.TypeFullName}");
            var method = ResolveBenchmarkMethod(type,benchmark.MethodName);
            instance=method.IsStatic ? null : Activator.CreateInstance(type);
            using var elementScope = BeginElementCountScope(assembly,job.ElementCount,"1");
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            method.Invoke(instance,null);
            stopwatch.Stop();
            var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
            var meanNanoseconds = stopwatch.Elapsed.TotalMilliseconds*1_000_000;
            var allocatedBytes = Math.Max(0,allocatedAfter-allocatedBefore);
            File.WriteAllText(
                logPath,
                $"In-process direct benchmark completed in {BenchmarkFormatting.FormatNanoseconds(meanNanoseconds)}.",
                Encoding.UTF8);
            return new BenchmarkJobRunResult(
                benchmark.Project,
                benchmark.MethodName,
                benchmark.TypeFullName,
                job.ElementCount,
                "Completed",
                BenchmarkFormatting.FormatNanoseconds(meanNanoseconds),
                string.Empty,
                string.Empty,
                meanNanoseconds,
                null,
                null,
                allocatedBytes,
                0,
                artifactFolder,
                string.Empty);
        } catch(Exception ex) when(ex is not OperationCanceledException) {
            var actual = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
            File.WriteAllText(logPath,actual.ToString(),Encoding.UTF8);
            return new BenchmarkJobRunResult(
                benchmark.Project,
                benchmark.MethodName,
                benchmark.TypeFullName,
                job.ElementCount,
                "Failed",
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                null,
                1,
                artifactFolder,
                actual.ToString());
        } finally {
            if(instance is IDisposable disposable) {
                disposable.Dispose();
            }
        }
    }

    private static Assembly ResolveBenchmarkAssembly(string assemblyName) =>
        AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(item => string.Equals(item.GetName().Name,assemblyName,StringComparison.OrdinalIgnoreCase))
        ??Assembly.Load(new AssemblyName(assemblyName));

    private static MethodInfo ResolveBenchmarkMethod(Type type,string methodName) {
        var method = type.GetMethod(methodName,BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static)
            ??throw new MissingMethodException(type.FullName,methodName);
        if(method.GetParameters().Length!=0||!HasBenchmarkAttribute(method)) {
            throw new MissingMethodException(type.FullName,methodName+" [Benchmark]");
        }

        return method;
    }

    private static bool HasBenchmarkAttribute(MethodInfo method) =>
        method.CustomAttributes.Any(static attribute => attribute.AttributeType.Name=="BenchmarkAttribute");

    private static IDisposable BeginElementCountScope(Assembly assembly,int elementCount,string elementScale) {
        var modeType = assembly.GetType("Benchmark.Shared.BenchmarkExecutionMode",throwOnError:false,ignoreCase:false);
        var method = modeType?.GetMethod(
            "UseElementCount",
            BindingFlags.Public|BindingFlags.Static,
            binder:null,
            types:[typeof(int),typeof(string)],
            modifiers:null);
        return method?.Invoke(null,[elementCount,elementScale]) as IDisposable
            ?? EmptyDisposable.Instance;
    }

    private sealed class EmptyDisposable:IDisposable {
        public static EmptyDisposable Instance { get; } = new();

        public void Dispose() {
        }
    }
}

public interface IBenchmarkResultService {
    ReportRow ToReportRow(BenchmarkResultItem item);
}

public sealed class BenchmarkResultService:IBenchmarkResultService {
    public ReportRow ToReportRow(BenchmarkResultItem item) {
        var method = item.ElementCount>0
            ? item.Method+" [N="+item.ElementCount.ToString(CultureInfo.InvariantCulture)+"]"
            : item.Method;
        return new ReportRow(
            item.Project,
            method,
            string.Empty,
            method,
            item.Status,
            item.Mean,
            string.Empty,
            string.Empty,
            item.AllocatedBytesValue?.ToString(CultureInfo.InvariantCulture)??string.Empty,
            string.Empty,
            string.Empty,
            item.MeanNanoseconds??0,
            item.ErrorNanoseconds??0,
            item.StdDevNanoseconds??0,
            item.AllocatedBytesValue??0,
            0,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            item.ArtifactFolder,
            item.ExitCode);
    }
}

public interface IBenchmarkCsvService {
    string WriteComparisonCsv(string runRoot,IReadOnlyList<BenchmarkComparisonRow> rows);
    IReadOnlyList<BenchmarkComparisonRow> ReadComparisonCsv(string path);
}

public sealed class BenchmarkCsvService:IBenchmarkCsvService {
    public string WriteComparisonCsv(string runRoot,IReadOnlyList<BenchmarkComparisonRow> rows) {
        Directory.CreateDirectory(runRoot);
        var path = Path.Combine(runRoot,"BenchmarkComparison.csv");
        File.WriteAllText(path,BenchmarkComparisonFormatter.WriteCsv(rows),new UTF8Encoding(true));
        return path;
    }

    public IReadOnlyList<BenchmarkComparisonRow> ReadComparisonCsv(string path) {
        return BenchmarkComparisonFormatter.ReadCsv(File.ReadAllText(path,Encoding.UTF8));
    }
}

public interface IBenchmarkComparisonService {
    IReadOnlyList<BenchmarkComparisonRow> BuildRows(IEnumerable<BenchmarkResultItem> results);
    IReadOnlyList<ChartComparisonPoint> BuildChartPoints(
        IReadOnlyList<BenchmarkComparisonRow> rows,
        ComparisonPairOption pair,
        BenchmarkMetricKind metric);
}

public sealed class BenchmarkComparisonService:IBenchmarkComparisonService {
    private readonly IBenchmarkResultService resultService;

    public BenchmarkComparisonService(IBenchmarkResultService resultService) {
        this.resultService=resultService;
    }

    public IReadOnlyList<BenchmarkComparisonRow> BuildRows(IEnumerable<BenchmarkResultItem> results) {
        var reportRows = results.Select(resultService.ToReportRow).ToArray();
        return BenchmarkComparisonFormatter.BuildComparisonRows(reportRows);
    }

    public IReadOnlyList<ChartComparisonPoint> BuildChartPoints(
        IReadOnlyList<BenchmarkComparisonRow> rows,
        ComparisonPairOption pair,
        BenchmarkMetricKind metric) {
        return rows
            .Select(row => CreateChartPoint(row,pair,metric))
            .Where(static point => point is not null)
            .Select(static point => point!)
            .OrderByDescending(static point => point.Ratio??(point.LeftValue>0&&point.RightValue==0?double.PositiveInfinity:0))
            .ThenByDescending(static point => Math.Abs(point.LeftValue-point.RightValue))
            .ThenBy(static point => point.Benchmark,StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ChartComparisonPoint? CreateChartPoint(BenchmarkComparisonRow row,ComparisonPairOption pair,BenchmarkMetricKind metric) {
        var left = row.GetProject(pair.LeftProject);
        var right = row.GetProject(pair.RightProject);
        if(!left.HasMeasuredValue||!right.HasMeasuredValue) {
            return null;
        }

        var leftValue = GetMetricValue(left,metric);
        var rightValue = GetMetricValue(right,metric);
        if(leftValue is null||rightValue is null) {
            return null;
        }

        var leftNumber = Math.Max(0,leftValue??0);
        var rightNumber = Math.Max(0,rightValue??0);
        var total = leftNumber+rightNumber;
        double? ratio = leftNumber>0&&rightNumber>0
            ? leftNumber/rightNumber
            : null;
        var metricName = metric==BenchmarkMetricKind.Time?"Time":"Allocated";
        var leftText = total>0 ? (leftNumber/total).ToString("P0",CultureInfo.InvariantCulture) : "0%";
        var rightText = total>0 ? (rightNumber/total).ToString("P0",CultureInfo.InvariantCulture) : "0%";
        var tooltip = $"{row.Benchmark}\n{pair.LeftProject} {metricName}: {FirstText(leftText,"-")}\n{pair.RightProject} {metricName}: {FirstText(rightText,"-")}\n比率: {(ratio is { } value?value.ToString("0.00",CultureInfo.InvariantCulture):"-")}";
        return new ChartComparisonPoint(
            row.Benchmark,
            BenchmarkFormatting.ShortName(row.Benchmark),
            leftNumber,
            rightNumber,
            leftText,
            rightText,
            tooltip,
            ratio);
    }

    private static double? GetMetricValue(ProjectMetrics metrics,BenchmarkMetricKind metric) {
        if(!metrics.HasMeasuredValue) {
            return null;
        }

        return metric==BenchmarkMetricKind.Time
            ? metrics.TimeNanoseconds
            : metrics.AllocatedBytes;
    }

    private static string FormatMetric(double? value,BenchmarkMetricKind metric) {
        if(value is null) {
            return string.Empty;
        }

        return metric==BenchmarkMetricKind.Time
            ? BenchmarkFormatting.FormatNanoseconds(value)
            : BenchmarkFormatting.FormatBytes((long)Math.Round(value.Value,MidpointRounding.AwayFromZero));
    }

    private static string FirstText(params string[] values) {
        return values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))??string.Empty;
    }
}

public interface ILogService {
    void Append(string text);
}

public sealed class MemoryLogService:ILogService {
    private readonly Action<string> append;

    public MemoryLogService(Action<string> append) {
        this.append=append;
    }

    public void Append(string text) {
        append(text);
    }
}

public interface IBenchmarkRunService {
    Task<BenchmarkRunReport> RunAsync(
        IReadOnlyList<BenchmarkCase> benchmarks,
        IReadOnlyList<int> logicalCpus,
        Func<IReadOnlyList<int>> logicalCpuProvider,
        IReadOnlyList<int> elementCounts,
        BenchmarkSourceTab sourceTab,
        BenchmarkRunMode mode,
        BenchmarkExecutionProfile executionProfile,
        TimeSpan adaptiveThreshold,
        bool restartFromRequestedElementCount,
        string? runRootOverride,
        Action<BenchmarkResultItem> resultReceived,
        Action<string> statusChanged,
        CancellationToken cancellationToken);
}

public sealed class BenchmarkRunService:IBenchmarkRunService {
    private readonly IBenchmarkQueueService queueService;
    private readonly IBenchmarkWorkerProcessService workerService;
    private readonly IBenchmarkCsvService csvService;
    private readonly IBenchmarkComparisonService comparisonService;

    public BenchmarkRunService(
        IBenchmarkQueueService queueService,
        IBenchmarkWorkerProcessService workerService,
        IBenchmarkCsvService csvService,
        IBenchmarkComparisonService comparisonService) {
        this.queueService=queueService;
        this.workerService=workerService;
        this.csvService=csvService;
        this.comparisonService=comparisonService;
    }

    public async Task<BenchmarkRunReport> RunAsync(
        IReadOnlyList<BenchmarkCase> benchmarks,
        IReadOnlyList<int> logicalCpus,
        Func<IReadOnlyList<int>> logicalCpuProvider,
        IReadOnlyList<int> elementCounts,
        BenchmarkSourceTab sourceTab,
        BenchmarkRunMode mode,
        BenchmarkExecutionProfile executionProfile,
        TimeSpan adaptiveThreshold,
        bool restartFromRequestedElementCount,
        string? runRootOverride,
        Action<BenchmarkResultItem> resultReceived,
        Action<string> statusChanged,
        CancellationToken cancellationToken) {
        var orchestrationRoot = FindSolutionRoot() ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Seamless");
        var benchmarkRoot = Path.Combine(orchestrationRoot,"Work","Benchmark",BenchmarkExecutionProfileNames.ToFolderName(executionProfile));
        var jobRunner = this.CreateJobRunner(executionProfile);
        var orchestrator = new BenchmarkOrchestrator(
            new BenchmarkStateManager(benchmarkRoot),
            new BenchmarkResultWriter(),
            jobRunner);
        var request = new BenchmarkRunRequest(
            benchmarks,
            logicalCpus,
            elementCounts,
            mode,
            adaptiveThreshold,
            TimeSpan.FromMinutes(60),
            logicalCpuProvider,
            RestartFromRequestedElementCount: restartFromRequestedElementCount,
            ExecutionProfile: executionProfile,
            RunRootOverride: runRootOverride,
            SourceTab: sourceTab);
        try {
            var orchestratorResult = await orchestrator.RunAsync(
                request,
                update => resultReceived(new BenchmarkResultItem(update.Job)),
                statusChanged,
                cancellationToken);
            return new BenchmarkRunReport(orchestratorResult.RunRoot,orchestratorResult.CsvPaths);
        } finally {
            if(jobRunner is IAsyncDisposable asyncDisposable) {
                await asyncDisposable.DisposeAsync();
            } else if(jobRunner is IDisposable disposable) {
                disposable.Dispose();
            }
        }

    }

    private IBenchmarkJobRunner CreateJobRunner(BenchmarkExecutionProfile executionProfile) =>
        executionProfile switch{
            BenchmarkExecutionProfile.Accurate => this.workerService,
            BenchmarkExecutionProfile.Intermediate => new BenchmarkPersistentWorkerProcessService(),
            BenchmarkExecutionProfile.Fast => new BenchmarkInProcessJobRunner(),
            _ => this.workerService,
        };

    private static string CreateRunRoot() {
        var root = FindSolutionRoot() ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Seamless");
        var path = Path.Combine(root,"Work","Benchmark",DateTime.Now.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string? FindSolutionRoot() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null) {
            if(File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))) {
                return directory.FullName;
            }
            directory=directory.Parent;
        }

        return null;
    }

    private static void WriteSummary(string runRoot,int queued,int completed,string csvPath) {
        var path = Path.Combine(runRoot,"BenchmarkRunSummary.txt");
        var builder = new StringBuilder();
        builder.AppendLine($"QueuedTasks: {queued}");
        builder.AppendLine($"CompletedOrFailedTasks: {completed}");
        builder.AppendLine($"CsvReport: {csvPath}");
        File.WriteAllText(path,builder.ToString(),Encoding.UTF8);
    }
}
