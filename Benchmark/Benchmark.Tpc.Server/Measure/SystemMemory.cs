using System.Runtime.InteropServices;
namespace Benchmark.Tpc.Server;
/// <summary>PC 全体のメモリ使用率。長い実行で、他の作業を巻き込まないための見張りに使う。</summary>
public static class SystemMemory{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Auto)]
    private struct MemoryStatus{
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }
    [DllImport("kernel32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    /// <summary>使用率(0〜100)。取れなければ 0。</summary>
    public static int UsedPercent(){
        var status=new MemoryStatus{Length=(uint)Marshal.SizeOf<MemoryStatus>()};
        if(!GlobalMemoryStatusEx(ref status)){
            return 0;
        }
        return (int)status.MemoryLoad;
    }
}
