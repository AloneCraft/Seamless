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

internal static class TsavoriteSetOperations {
    public static void ExceptWith<TRecord>(ICollection<TRecord> target,Seamless.Collections.ISet<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other)
            target.Remove(item);
    }

    public static void IntersectWith<TRecord>(ICollection<TRecord> target,Seamless.Collections.ISet<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        var toRemove = new List<TRecord>();
        foreach(var item in target) {
            if(!other.Contains(item))
                toRemove.Add(item);
        }
        foreach(var item in toRemove)
            target.Remove(item);
    }

    public static bool IsSubsetOf<TRecord>(Seamless.Collections.ISet<TRecord> target,Seamless.Collections.ISet<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        if(target.GetLongCount()>other.GetLongCount())
            return false;
        foreach(var item in target) {
            if(!other.Contains(item))
                return false;
        }
        return true;
    }

    public static bool IsSupersetOf<TRecord>(Seamless.Collections.ISet<TRecord> target,Seamless.Collections.ISet<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other) {
            if(!target.Contains(item))
                return false;
        }
        return true;
    }

    public static bool IsProperSubsetOf<TRecord>(Seamless.Collections.ISet<TRecord> target,Seamless.Collections.ISet<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        return target.GetLongCount()<other.GetLongCount()&&IsSubsetOf(target,other);
    }

    public static bool IsProperSupersetOf<TRecord>(Seamless.Collections.ISet<TRecord> target,Seamless.Collections.ISet<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        return target.GetLongCount()>other.GetLongCount()&&IsSupersetOf(target,other);
    }

    public static bool Overlaps<TRecord>(Seamless.Collections.ISet<TRecord> target,Seamless.Collections.ISet<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other) {
            if(target.Contains(item))
                return true;
        }
        return false;
    }

    public static bool SetEquals<TRecord>(Seamless.Collections.ISet<TRecord> target,Seamless.Collections.ISet<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        return target.GetLongCount()==other.GetLongCount()&&IsSubsetOf(target,other);
    }

    public static void SymmetricExceptWith<TRecord>(ICollection<TRecord> target,Seamless.Collections.ISet<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        var toAdd = new List<TRecord>();
        var toRemove = new List<TRecord>();
        foreach(var item in other) {
            if(target.Contains(item))
                toRemove.Add(item);
            else
                toAdd.Add(item);
        }
        foreach(var key in toRemove)
            target.Remove(key);
        foreach(var item in toAdd)
            target.Add(item);
    }

    public static void UnionWith<TRecord>(ICollection<TRecord> target,Seamless.Collections.ISet<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other)
            target.Add(item);
    }
}
