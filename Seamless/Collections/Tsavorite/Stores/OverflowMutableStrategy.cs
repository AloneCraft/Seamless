using EqualityEmpty = Seamless.Collections.Tsavorite.EqualityEmpty;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SpanByteStoreAllocator = Tsavorite.core.SpanByteAllocator<
    Tsavorite.core.StoreFunctions<
        Tsavorite.core.SpanByteComparer,
        Tsavorite.core.DefaultRecordTriggers
    >
>;
using SpanByteStoreFunctions = Tsavorite.core.StoreFunctions<
    Tsavorite.core.SpanByteComparer,
    Tsavorite.core.DefaultRecordTriggers
>;
using Garnet.common;
using Seamless.Collections;
using Tsavorite.core;
namespace Seamless.Collections.Tsavorite;

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
            if(this.storageCapacity<0) {
                return long.MaxValue;
            }

            long fractionLimit;
            if(this.limitFraction>=1.0) {
                fractionLimit=this.storageCapacity;
            } else {
                fractionLimit=(long)(this.storageCapacity*this.limitFraction);
            }
            var reserveLimit = this.storageCapacity-this.reserveBytes;
            return Math.Min(fractionLimit,reserveLimit);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool WouldOverflowMutable(long tailAddress,int keyLength,int valueLength) {
        if(this.storageCapacity<0) {
            return false;
        }

        var required = MemoryOverflowMutableStrategy.GetRequiredBytes(keyLength,valueLength);
        return MemoryOverflowMutableStrategy.WouldExceedLimit(tailAddress,required,this.LimitBytes);
    }
}
