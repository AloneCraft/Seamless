using System.Runtime.InteropServices;

namespace Benchmark.Dynamic.Worker;

public static class Program{
    public static Task<int> Main(string[] args){
        論理CPUへピン留め(args);
        var Host=new WorkerHost(Console.In,Console.Out,Console.Error);
        return Host.RunAsync();
    }

    private static void 論理CPUへピン留め(string[] args){
        var 論理CPU=引数から論理CPUを読む(args);
        if(論理CPU is null||論理CPU<0||論理CPU>=Environment.ProcessorCount||論理CPU>=63){
            return;
        }

        var マスク=1L<<論理CPU.Value;
        if(!OperatingSystem.IsWindows()){
            return;
        }

        try{
            System.Diagnostics.Process.GetCurrentProcess().ProcessorAffinity=new IntPtr(マスク);
        }catch(PlatformNotSupportedException){
        }catch(InvalidOperationException){
        }catch(System.ComponentModel.Win32Exception){
        }

        SetThreadAffinityMask(GetCurrentThread(),new UIntPtr((ulong)マスク));
    }

    private static int? 引数から論理CPUを読む(string[] args){
        for(var 番号=0;番号<args.Length-1;番号++){
            if(string.Equals(args[番号],"--logical-cpu",StringComparison.OrdinalIgnoreCase)
                &&int.TryParse(args[番号+1],out var 値)){
                return 値;
            }
        }
        return null;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll",SetLastError = true)]
    private static extern UIntPtr SetThreadAffinityMask(IntPtr スレッドハンドル,UIntPtr スレッドアフィニティマスク);
}
