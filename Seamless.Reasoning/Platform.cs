using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に非 null 値が入っていなければなりません。'required' 修飾子を追加するか、null 許容として宣言することを検討してください。
namespace Seamless.Reasoning;

public static class 実行環境 {
    [DllImport("Kernel32.dll")] public static extern uint GetCurrentProcessorNumber();
    [DllImport("Kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll")] public static extern nint GetCurrentThread();
    [DllImport("kernel32.dll")] public static extern nint SetThreadAffinityMask(IntPtr hThread,IntPtr dwThreadAffinityMask);
    private const int RelationCache = 2;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetLogicalProcessorInformationEx(int relationshipType, IntPtr buffer, ref int returnedLength);

    public static long GetCacheBytes(int cacheLevel)
    {
        int length = 0;
        // 1. 必要なバッファサイズを確認
        GetLogicalProcessorInformationEx(RelationCache, IntPtr.Zero, ref length);
        if (length == 0) return 0;

        IntPtr buffer = Marshal.AllocHGlobal(length);
        try
        {
            // 2. 実際のデータを取得
            if (!GetLogicalProcessorInformationEx(RelationCache, buffer, ref length))
                return 0;

            long totalSizeBytes = 0;
            int offset = 0;

            while (offset < length)
            {
                // SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX の先頭部分を読み取る
                var info = Marshal.PtrToStructure<SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX>(buffer + offset);

                if (info.Relationship == RelationCache)
                {
                    // Cache情報を特定し、レベルが一致するか確認
                    if (info.Cache.Level == cacheLevel)
                    {
                        // データキャッシュ(1) または 統合キャッシュ(0) を合算
                        // 命令キャッシュ(2)を除外したい場合は Type で判定
                        if (info.Cache.Type != 2) 
                        {
                            totalSizeBytes += info.Cache.CacheSize;
                        }
                    }
                }
                // 次の構造体へオフセットを移動
                offset += info.Size;
            }
            return totalSizeBytes;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // --- 構造体定義 (Win32 API 互換) ---
    [StructLayout(LayoutKind.Sequential)]
    struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX
    {
        public int Relationship;
        public int Size;
        public CACHE_RELATIONSHIP Cache;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct CACHE_RELATIONSHIP
    {
        public byte Level;
        public byte Associativity;
        public ushort LineSize;
        public uint CacheSize;
        public byte Type; // 0:Unified, 1:Data, 2:Instruction
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public byte[] Reserved;
        public long GroupMask;
    }
}