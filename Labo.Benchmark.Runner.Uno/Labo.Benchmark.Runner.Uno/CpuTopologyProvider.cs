using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Labo.Benchmark.Runner.Uno;

internal static class CpuTopologyProvider{
    public static IReadOnlyList<CpuCoreInfo> Load(){
        try{
            if(OperatingSystem.IsWindows()){
                var windows = LoadWindows();
                if(windows.Count>0){
                    return windows;
                }
            }else if(OperatingSystem.IsLinux()){
                var linux = LoadLinux();
                if(linux.Count>0){
                    return linux;
                }
            }else if(OperatingSystem.IsMacOS()){
                return LoadMacOs();
            }
        }catch{
            // Fall through to a plain logical CPU list. The runner can still
            // control parallelism even when the host does not expose topology.
        }

        return LoadFallback(supportsAffinity:OperatingSystem.IsWindows()||OperatingSystem.IsLinux());
    }

    private static IReadOnlyList<CpuCoreInfo> LoadFallback(bool supportsAffinity){
        return Enumerable.Range(0,Environment.ProcessorCount)
            .Select(index => CreateCpu(index,CpuCoreKind.Unknown,index,index,supportsAffinity))
            .ToArray();
    }

    private static IReadOnlyList<CpuCoreInfo> LoadWindows(){
        var length = 0;
        _ = GetLogicalProcessorInformationEx(RelationProcessorCore,IntPtr.Zero,ref length);
        if(length<=0){
            return [];
        }

        var buffer = Marshal.AllocHGlobal(length);
        try{
            if(!GetLogicalProcessorInformationEx(RelationProcessorCore,buffer,ref length)){
                return [];
            }

            var cores = new List<WindowsCore>();
            var offset = 0;
            while(offset<length){
                var entry = IntPtr.Add(buffer,offset);
                var size = Marshal.ReadInt32(entry,4);
                if(size<=0){
                    break;
                }

                var efficiencyClass = Marshal.ReadByte(entry,9);
                var groupCount = Marshal.ReadInt16(entry,30);
                var logicalProcessors = new List<int>();
                var groupAffinityOffset = 32;
                var groupAffinitySize = IntPtr.Size+8;
                for(var groupIndex=0;groupIndex<groupCount;groupIndex++){
                    var affinity = IntPtr.Add(entry,groupAffinityOffset+(groupIndex*groupAffinitySize));
                    var mask = ReadUIntPtr(affinity);
                    var group = Marshal.ReadInt16(affinity,IntPtr.Size);
                    if(group<0){
                        continue;
                    }

                    AddMaskBits(logicalProcessors,group*64,mask);
                }

                if(logicalProcessors.Count>0){
                    cores.Add(new WindowsCore(efficiencyClass,logicalProcessors.Order().ToArray()));
                }

                offset+=size;
            }

            return CreateFromCoreGroups(cores.Select(static core => new CoreGroup(
                core.LogicalProcessors,
                core.EfficiencyClass))).ToArray();
        }finally{
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static IReadOnlyList<CpuCoreInfo> LoadLinux(){
        const string cpuRoot = "/sys/devices/system/cpu";
        var online = ReadLinuxOnlineCpuList(Path.Combine(cpuRoot,"online"));
        if(online.Count==0){
            online=Enumerable.Range(0,Environment.ProcessorCount).ToArray();
        }

        var groups = online
            .Select(cpu => new{
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

    private static IReadOnlyList<CpuCoreInfo> LoadMacOs(){
        var logicalCount = ReadSysctlInt("hw.logicalcpu")??Environment.ProcessorCount;
        var performanceCount = ReadSysctlInt("hw.perflevel0.logicalcpu");
        var efficiencyCount = ReadSysctlInt("hw.perflevel1.logicalcpu");
        var items = new List<CpuCoreInfo>(logicalCount);
        var index = 0;

        if(performanceCount is >0){
            for(var i=0;i<performanceCount&&index<logicalCount;i++,index++){
                items.Add(CreateCpu(index,CpuCoreKind.Performance,index,i,supportsAffinity:false));
            }
        }

        if(efficiencyCount is >0){
            for(var i=0;i<efficiencyCount&&index<logicalCount;i++,index++){
                items.Add(CreateCpu(index,CpuCoreKind.Efficiency,index,i,supportsAffinity:false));
            }
        }

        while(index<logicalCount){
            items.Add(CreateCpu(index,CpuCoreKind.Unknown,index,index,supportsAffinity:false));
            index++;
        }

        return items;
    }

    private static IEnumerable<CpuCoreInfo> CreateFromCoreGroups(IEnumerable<CoreGroup> rawGroups){
        var groups = rawGroups
            .Where(static group => group.LogicalProcessors.Count>0)
            .OrderBy(static group => group.LogicalProcessors.Min())
            .ToArray();
        if(groups.Length==0){
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
        var nextKindIndex = new Dictionary<CpuCoreKind,int>();

        for(var physicalIndex=0;physicalIndex<groups.Length;physicalIndex++){
            var group = groups[physicalIndex];
            var kind = CpuCoreKind.Unknown;
            if(hasHybridScores&&group.KindScore==maxScore){
                kind=CpuCoreKind.Performance;
            }else if(hasHybridScores&&group.KindScore==minScore){
                kind=CpuCoreKind.Efficiency;
            }

            var kindIndex = nextKindIndex.TryGetValue(kind,out var current) ? current : 0;
            nextKindIndex[kind]=kindIndex+1;
            foreach(var logicalProcessor in group.LogicalProcessors.Order()){
                yield return CreateCpu(logicalProcessor,kind,physicalIndex,kindIndex,supportsAffinity:CanUseAffinity(logicalProcessor));
            }
        }
    }

    private static CpuCoreInfo CreateCpu(int logicalProcessorIndex,CpuCoreKind kind,int physicalCoreIndex,int kindCoreIndex,bool supportsAffinity){
        var canUseAffinity = supportsAffinity&&CanUseAffinity(logicalProcessorIndex);
        var mask = canUseAffinity ? 1L<<logicalProcessorIndex : 0L;
        return new CpuCoreInfo(logicalProcessorIndex,kind,physicalCoreIndex,kindCoreIndex,mask,canUseAffinity);
    }

    private static bool CanUseAffinity(int logicalProcessorIndex){
        return logicalProcessorIndex>=0&&logicalProcessorIndex<(IntPtr.Size*8)-1;
    }

    private static UIntPtr ReadUIntPtr(IntPtr address){
        return IntPtr.Size==8
            ? new UIntPtr(unchecked((ulong)Marshal.ReadInt64(address)))
            : new UIntPtr(unchecked((uint)Marshal.ReadInt32(address)));
    }

    private static void AddMaskBits(List<int> logicalProcessors,int baseIndex,UIntPtr mask){
        var value = mask.ToUInt64();
        for(var bit=0;bit<64;bit++){
            if((value&(1UL<<bit))!=0){
                logicalProcessors.Add(baseIndex+bit);
            }
        }
    }

    private static IReadOnlyList<int> ReadLinuxOnlineCpuList(string path){
        if(!File.Exists(path)){
            return [];
        }

        var text = File.ReadAllText(path).Trim();
        var result = new List<int>();
        foreach(var part in text.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)){
            var range = part.Split('-',StringSplitOptions.TrimEntries);
            if(range.Length==2
                &&int.TryParse(range[0],NumberStyles.Integer,CultureInfo.InvariantCulture,out var start)
                &&int.TryParse(range[1],NumberStyles.Integer,CultureInfo.InvariantCulture,out var end)){
                for(var cpu=start;cpu<=end;cpu++){
                    result.Add(cpu);
                }
            }else if(int.TryParse(part,NumberStyles.Integer,CultureInfo.InvariantCulture,out var single)){
                result.Add(single);
            }
        }

        return result.Order().Distinct().ToArray();
    }

    private static string ReadLinuxCoreKey(string cpuRoot,int cpu){
        var topology = Path.Combine(cpuRoot,$"cpu{cpu}","topology");
        var packageId = ReadLinuxString(Path.Combine(topology,"physical_package_id"))??"0";
        var coreId = ReadLinuxString(Path.Combine(topology,"core_id"))??cpu.ToString(CultureInfo.InvariantCulture);
        return packageId+":"+coreId;
    }

    private static string? ReadLinuxString(string path){
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    private static int? ReadLinuxInt(string path){
        var text = ReadLinuxString(path);
        return int.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out var value)
            ? value
            : null;
    }

    private static int? ReadSysctlInt(string key){
        try{
            using var process = new Process{
                StartInfo = new ProcessStartInfo{
                    FileName = "sysctl",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
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
        }catch{
            return null;
        }
    }

    [DllImport("kernel32.dll",SetLastError=true)]
    private static extern bool GetLogicalProcessorInformationEx(int relationshipType,IntPtr buffer,ref int returnedLength);

    private const int RelationProcessorCore = 0;

    private sealed record WindowsCore(byte EfficiencyClass,IReadOnlyList<int> LogicalProcessors);
    private sealed record CoreGroup(IReadOnlyList<int> LogicalProcessors,int? KindScore);
}
