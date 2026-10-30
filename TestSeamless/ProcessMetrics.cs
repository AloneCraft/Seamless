using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TestSeamless;

public sealed record ResourceMeasurement(string Status,int Pid,DateTime? CreatedUtc,double? CpuMilliseconds,long? WorkingSetObservedPeak,long? PrivateBytesObservedPeak,ulong? ReadTransferBytes,ulong? WriteTransferBytes,string? Reason,string IoMeaning="OS process transfer counters; not physical disk I/O");
public sealed class ProcessMetrics:IAsyncDisposable{
    private readonly Process? process;private readonly int pid;private readonly DateTime expected;private readonly CancellationTokenSource stop=new();private readonly Task sampling;private TimeSpan? cpu;private IoCounters? io;private long working,privateBytes;private string? error;
    public ProcessMetrics(int pid,DateTime expected){this.pid=pid;this.expected=expected;try{this.process=Process.GetProcessById(pid);this.CheckIdentity();this.cpu=this.process.TotalProcessorTime;if(GetProcessIoCounters(this.process.Handle,out var counters)){this.io=counters;}this.Sample();}catch(Exception ex){this.error=ex.Message;}this.sampling=this.SampleAsync();}
    private void CheckIdentity(){if(this.process is null||this.process.StartTime.ToUniversalTime()!=this.expected){throw new InvalidOperationException("Process PID/creation time does not match the measurement target.");}}
    private void Sample(){this.CheckIdentity();this.process!.Refresh();this.working=Math.Max(this.working,this.process.WorkingSet64);this.privateBytes=Math.Max(this.privateBytes,this.process.PrivateMemorySize64);}
    private async Task SampleAsync(){try{while(!this.stop.IsCancellationRequested){await Task.Delay(20,this.stop.Token);this.Sample();}}catch(OperationCanceledException){}catch(Exception ex){this.error=ex.Message;}}
    public async Task<ResourceMeasurement> FinishAsync(){this.stop.Cancel();await this.sampling;try{this.Sample();var duration=this.process!.TotalProcessorTime-this.cpu;ulong? read=null,write=null;if(this.io is not null&&GetProcessIoCounters(this.process.Handle,out var end)){read=checked(end.ReadTransferCount-this.io.Value.ReadTransferCount);write=checked(end.WriteTransferCount-this.io.Value.WriteTransferCount);}var status="Available";var reason=this.error;if(duration is null||read is null||write is null||reason is not null){status="Unavailable";reason??="One or more OS process counters could not be read.";}return new(status,this.pid,this.expected,duration?.TotalMilliseconds,this.working,this.privateBytes,read,write,reason);}catch(Exception ex){return new("Unavailable",this.pid,this.expected,null,null,null,null,null,ex.Message);}}
    public async ValueTask DisposeAsync(){this.stop.Cancel();await this.sampling;this.process?.Dispose();this.stop.Dispose();}
    [StructLayout(LayoutKind.Sequential)]private struct IoCounters{public ulong ReadOperationCount,WriteOperationCount,OtherOperationCount,ReadTransferCount,WriteTransferCount,OtherTransferCount;}
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetProcessIoCounters(IntPtr process,out IoCounters counters);
}
