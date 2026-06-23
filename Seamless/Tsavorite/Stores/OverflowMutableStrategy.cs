namespace Seamless.Tsavorite.Stores;

internal enum OverflowMutableStrategyKind:byte {
    Memory,
    Storage,
}

internal readonly struct MemoryOverflowMutableStrategy {
    internal const int RecordOverheadBytes = 128;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool WouldOverflowMutable(long logMemorySize,double mutableFraction,long beginAddress,long tailAddress,int keyLength,int valueLength) {
        var mutableBytes = (long)(logMemorySize*mutableFraction);
        if(mutableBytes<=0) {
            mutableBytes=logMemorySize;
        }
        var used = tailAddress-beginAddress;
        var required = GetRequiredBytes(keyLength,valueLength);
        return WouldExceedLimit(used,required,mutableBytes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long GetRequiredBytes(int keyLength,int valueLength) {
        return (long)keyLength+valueLength+RecordOverheadBytes;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool WouldExceedLimit(long used,long required,long limit) {
        return used>limit-required;
    }
}

internal readonly struct StorageOverflowMutableStrategy {
    internal const double DefaultLimitFraction = 1.0;
    internal const long DefaultReserveBytes = 0;

    private readonly long storageCapacity;
    private readonly double limitFraction;
    private readonly long reserveBytes;

    public StorageOverflowMutableStrategy(long storageCapacity,double limitFraction = DefaultLimitFraction,long reserveBytes = DefaultReserveBytes) {
        if(double.IsNaN(limitFraction)||limitFraction<0||limitFraction>1) {
            throw new ArgumentOutOfRangeException(nameof(limitFraction));
        }
        if(reserveBytes<0) {
            throw new ArgumentOutOfRangeException(nameof(reserveBytes));
        }
        this.storageCapacity=storageCapacity;
        this.limitFraction=limitFraction;
        this.reserveBytes=reserveBytes;
    }

    internal long LimitBytes {
        get {
            if(storageCapacity<0) {
                return long.MaxValue;
            }

            long fractionLimit;
            if(limitFraction>=1.0) {
                fractionLimit=storageCapacity;
            }
            else {
                fractionLimit=(long)(storageCapacity*limitFraction);
            }
            var reserveLimit = storageCapacity-reserveBytes;
            return Math.Min(fractionLimit,reserveLimit);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool WouldOverflowMutable(long tailAddress,int keyLength,int valueLength) {
        if(storageCapacity<0) {
            return false;
        }

        var required = MemoryOverflowMutableStrategy.GetRequiredBytes(keyLength,valueLength);
        return MemoryOverflowMutableStrategy.WouldExceedLimit(tailAddress,required,LimitBytes);
    }
}
