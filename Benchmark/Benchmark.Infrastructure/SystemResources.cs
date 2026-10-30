using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Benchmark.Infrastructure;

public sealed record EnvironmentSnapshot(string OS,string Architecture,string Runtime,int Processors,string CPU,string Affinity,string MeasurementVersion){
    [System.Text.Json.Serialization.JsonIgnore]public string Key=>Fingerprint.OfJSON(this);
    [System.Text.Json.Serialization.JsonIgnore]public string ProfileEnvironment=>Seamless.Execution.ProfileEnvironment.Identify(this.OS,this.Architecture,this.Runtime,this.CPU);
    public static EnvironmentSnapshot Capture(string measurementVersion="balanced-v1"){
        var cpu=Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")??RuntimeInformation.ProcessArchitecture.ToString();
        var affinity="default";
        if(OperatingSystem.IsWindows()){
            using var process=Process.GetCurrentProcess();
            affinity=process.ProcessorAffinity.ToInt64().ToString("X");
            using var key=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            cpu=key?.GetValue("ProcessorNameString")?.ToString()?.Trim()??cpu;
        }
        return new(RuntimeInformation.OSDescription,RuntimeInformation.ProcessArchitecture.ToString(),RuntimeInformation.FrameworkDescription,
            Environment.ProcessorCount,cpu,affinity,measurementVersion);
    }
}

public readonly record struct MemorySnapshot(double UsedPercent,long AvailableBytes,long TotalBytes);

public static class SystemResources{
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryState{
        public uint Length,Load;
        public ulong TotalPhysical,AvailablePhysical,TotalPageFile,AvailablePageFile,TotalVirtual,AvailableVirtual,Extended;
    }
    [DllImport("kernel32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryState state);

    public static MemorySnapshot Memory(){
        if(OperatingSystem.IsWindows()){
            var state=new MemoryState{Length=(uint)Marshal.SizeOf<MemoryState>()};
            if(!GlobalMemoryStatusEx(ref state)){throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
            var used=100d*(state.TotalPhysical-state.AvailablePhysical)/state.TotalPhysical;
            return new(used,checked((long)state.AvailablePhysical),checked((long)state.TotalPhysical));
        }
        if(OperatingSystem.IsLinux()){
            var values=File.ReadLines("/proc/meminfo").Select(static line=>line.Split(':',2)).ToDictionary(static item=>item[0],static item=>long.Parse(item[1].Trim().Split(' ')[0])*1024);
            var total=values["MemTotal"];var available=values["MemAvailable"];
            return new(100d*(total-available)/total,available,total);
        }
        throw new PlatformNotSupportedException("Physical memory monitoring is required for benchmark execution.");
    }
    public static void Check(bool starting=false){
        var memory=Memory();
        var limit=88;
        if(starting){limit=85;}
        if(memory.UsedPercent>=limit){throw new ResourceGuardException($"物理メモリ使用率{memory.UsedPercent:F1}%が実行上限{limit}%に達しました。");}
    }
    public static void CheckBudget(long bytes){
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        var memory=Memory();
        var headroom=Math.Max(0,memory.TotalBytes*0.85-(memory.TotalBytes-memory.AvailableBytes));
        if(bytes>headroom){throw new ResourceGuardException($"指定したメモリ予算{bytes:N0}Bが、物理メモリ85%までの余裕{headroom:N0}Bを超えます。");}
    }
}

public sealed class ResourceGuardException(string message):OperationCanceledException(message);
