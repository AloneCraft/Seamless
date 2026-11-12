using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Seamless.Management.Server;

namespace Management.Tests;

internal sealed record LockOwner(int Pid,DateTime Created,string Application,string Service);
internal static class RecoveryLockDiagnostic{
    internal static int ProcessSignal(Process child){
        if(!OperatingSystem.IsWindows()){return -1;}
        var status=WaitForSingleObject(child.SafeHandle,0);if(status==uint.MaxValue){throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}return checked((int)status);
    }
    internal static int OwnerHandleFlags(ManagementStore store){
        if(!OperatingSystem.IsWindows()){return -1;}
        var field=typeof(ManagementStore).GetField("owner",BindingFlags.Instance|BindingFlags.NonPublic)??throw new MissingFieldException("ManagementStore.owner");
        var file=(FileStream)field.GetValue(store)!;
        if(!GetHandleInformation(file.SafeFileHandle,out var flags)){throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
        return checked((int)flags);
    }
    internal static void Record(string root,Process child,long waitReturned,long openedAt,long finishedAt,JsonElement ready,int nativeSignal,Exception? failure){
        var path=Path.Combine(root,"management.lock");LockOwner[] owners=[];string? observationError=null;
        if(failure is not null){
            var match=System.Text.RegularExpressions.Regex.Match(failure.Message,"'([^']+)'",System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if(match.Success&&Path.IsPathFullyQualified(match.Groups[1].Value)&&Path.GetFullPath(match.Groups[1].Value).StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)){path=match.Groups[1].Value;}
            try{owners=Owners(path);}catch(Exception error){observationError=error.ToString();}
        }
        using var observer=Process.GetCurrentProcess();
        var value=new{Utc=DateTimeOffset.UtcNow,Root=root,ObserverPid=observer.Id,ChildPid=child.Id,ChildCreated=ready.GetProperty("Created").GetDateTime(),ChildExited=child.HasExited,ChildExitCode=child.ExitCode,
            Ready=ready,Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,NativeSignalAfterWait=nativeSignal,StopwatchFrequency=Stopwatch.Frequency,WaitReturned=waitReturned,OpenedAt=openedAt,FinishedAt=finishedAt,OpenedWithoutRetry=failure is null,Path=path,
            FailureType=failure?.GetType().FullName,FailureHResult=failure?.HResult,FailureMessage=failure?.Message,Owners=owners,ObservationError=observationError};
        File.WriteAllText(root+".recovery.json",JsonSerializer.Serialize(value));
    }
    internal static LockOwner[] Owners(string path){
        if(!OperatingSystem.IsWindows()){throw new PlatformNotSupportedException("Restart Managerの所有者観測はWindowsで実行してください。");}
        var code=RmStartSession(out var session,0,new StringBuilder(33));Check(code,"RmStartSession");
        try{
            Check(RmRegisterResources(session,1,[path],0,IntPtr.Zero,0,IntPtr.Zero),"RmRegisterResources");uint reasons=0,count=0;
            for(var attempt=0;attempt<3;attempt++){
                code=RmGetList(session,out var needed,ref count,null,ref reasons);if(code==0){return [];}if(code!=234){Check(code,"RmGetList");}
                if(needed>1024){throw new InvalidOperationException("ロック所有者一覧の上限を超えました。");}count=needed;var rows=new Info[count];
                code=RmGetList(session,out needed,ref count,rows,ref reasons);if(code==234){count=0;continue;}Check(code,"RmGetList");
                return rows.Take(checked((int)count)).Select(row=>new LockOwner(checked((int)row.Process.Pid),DateTime.FromFileTimeUtc(((long)row.Process.Start.dwHighDateTime<<32)|(uint)row.Process.Start.dwLowDateTime),row.Application,row.Service)).ToArray();
            }
            throw new InvalidOperationException("ロック所有者一覧が観測中に変化しました。");
        }finally{RmEndSession(session);}
    }
    private static void Check(int code,string operation){if(code!=0){throw new System.ComponentModel.Win32Exception(code,operation);}}
    [StructLayout(LayoutKind.Sequential)]private struct Unique{public uint Pid;public System.Runtime.InteropServices.ComTypes.FILETIME Start;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct Info{
        public Unique Process;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Application;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)]public string Service;
        public uint Type,Status,Session;
        [MarshalAs(UnmanagedType.Bool)]public bool Restartable;
    }
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetHandleInformation(SafeFileHandle handle,out uint flags);
    [DllImport("kernel32.dll",SetLastError=true)]private static extern uint WaitForSingleObject(SafeProcessHandle handle,uint milliseconds);
    [DllImport("rstrtmgr.dll",CharSet=CharSet.Unicode)]private static extern int RmStartSession(out uint session,int flags,StringBuilder key);
    [DllImport("rstrtmgr.dll",CharSet=CharSet.Unicode)]private static extern int RmRegisterResources(uint session,uint count,string[] files,uint apps,IntPtr application,uint services,IntPtr service);
    [DllImport("rstrtmgr.dll")]private static extern int RmGetList(uint session,out uint needed,ref uint count,[In,Out]Info[]? rows,ref uint reasons);
    [DllImport("rstrtmgr.dll")]private static extern int RmEndSession(uint session);
}
