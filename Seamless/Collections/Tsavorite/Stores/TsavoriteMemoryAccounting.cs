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

/// <summary>採用Tsavoriteの既知の常駐配列を割当前に覆う。使用中bucket数と確保済みpoolを区別する。</summary>
internal static class TsavoriteMemoryAccounting{
    private const long OverflowPageEntries=65536;
    // 2.2.5の公開floorは先読み2枚とsector paddingを含む。将来の割当ては事前予約する。
    // CLR配列headerと可変長のpage tableは公開payload統計に含まれないため別途覆う。
    internal static long OverflowBytes(long buckets,long indexBytes=64){
        var levels=OverflowLevels(buckets,indexBytes);
        var pages=Math.Min(levels,checked(buckets/OverflowPageEntries+2));
        return checked(pages*(KVSettings.OverflowBucketFloorMemorySize/2+24)+2*(levels*8+24));
    }
    internal static long ObservedOverflowBytes(long payload,long buckets,long indexBytes){
        var levels=OverflowLevels(buckets,indexBytes);
        return checked(payload+Math.Min(levels,buckets/OverflowPageEntries+2)*24+2*(levels*8+24));
    }
    private static long OverflowLevels(long buckets,long indexBytes){
        var configured=KVSettings.GetIndexOverflowMaxMemorySize(indexBytes/KVSettings.HashBucketSizeBytes,KVSettings.DefaultIndexOverflowThreshold)/(OverflowPageEntries*KVSettings.HashBucketSizeBytes);
        return Math.Max(configured,checked((buckets+OverflowPageEntries-1)/OverflowPageEntries));
    }
    internal static long OverflowBucketLimit(long buckets)=>checked((buckets/OverflowPageEntries+1)*OverflowPageEntries);
    internal static long IndexBytes(long bytes,long sector)=>Math.Max(checked(Align(bytes,sector)+sector+24),NativeAllocationBytes(bytes,sector));
    internal static long PageBytes(long bytes,long sector)=>Math.Max(checked(bytes+2*sector+24),NativeAllocationBytes(checked(bytes+2*sector),sector));
    private static long NativeAllocationBytes(long bytes,long sector){
        var alignment=OperatingSystem.IsLinux()&&bytes>=2L<<20?Math.Max(sector,2L<<20):sector;
        return Align(checked(bytes+alignment),Environment.SystemPageSize);
    }
    private static long Align(long bytes,long alignment)=>checked((bytes+alignment-1)/alignment*alignment);
    // page status、参照/ポインタ配列、StorageのPendingFlushListとnative block表を含む上界。
    // epoch等の固定管理領域も予約する。全CLR heapやcaller所有の結果の測定値ではない。
    internal static long ManagementBytes(long capacity,long page)=>checked(capacity/page*256+65536);
    internal static long Required(long index,long overflow,long capacity,long page,long sector,long residentPages)=>
        checked(IndexBytes(index,sector)+overflow+ManagementBytes(capacity,page)+(residentPages+4)*PageBytes(page,sector));
    internal static long NativeCapacity(global::Seamless.Execution.Store owner,long index,long page,long sector,long configured,bool grow,long existingReservation){
        var available=checked(owner.Budget.AvailableMemoryBytes+existingReservation);var capacity=configured;
        if(grow){
            var remaining=Math.Max(0,available-OverflowBytes(0,index)-IndexBytes(index,sector));long target=checked(page*2);
            while(target<=remaining/2){target=checked(target*2);}capacity=Math.Max(configured,target);
            while(capacity>configured&&Required(index,OverflowBytes(0,index),capacity,page,sector,2)>available){capacity/=2;}
        }
        return capacity;
    }
}
