using System.Runtime.CompilerServices;

namespace Seamless.Stores;

internal enum OverflowMutableStrategyKind : byte
{
    Memory,
    Storage,
}

internal interface IOverflowMutableStrategy<TSelf>
    where TSelf : struct, IOverflowMutableStrategy<TSelf>
{
    static abstract bool WouldOverflowMutable(long logMemorySize, double mutableFraction, long beginAddress, long tailAddress, int keyLength, int valueLength);
}

internal readonly struct MemoryOverflowMutableStrategy : IOverflowMutableStrategy<MemoryOverflowMutableStrategy>
{
    private const int RecordOverheadBytes = 128;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool WouldOverflowMutable(long logMemorySize, double mutableFraction, long beginAddress, long tailAddress, int keyLength, int valueLength)
    {
        var mutableBytes = (long)(logMemorySize * mutableFraction);
        if (mutableBytes <= 0)
        {
            mutableBytes = logMemorySize;
        }
        var used = tailAddress - beginAddress;
        var required = (long)keyLength + valueLength + RecordOverheadBytes;
        return used + required > mutableBytes;
    }
}

internal readonly struct StorageOverflowMutableStrategy : IOverflowMutableStrategy<StorageOverflowMutableStrategy>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool WouldOverflowMutable(long logMemorySize, double mutableFraction, long beginAddress, long tailAddress, int keyLength, int valueLength) => false;
}