namespace Seamless.Collections;

using System;
using System.Diagnostics.CodeAnalysis;
using Tsavorite.core;

/// <summary>
/// Unmanaged Struct 用のセッション関数。
/// 固定長であるため InPlace での更新や単純な読み取りが可能。
/// RMWなどで高度な判定を行いたい場合などに拡張できる。
/// </summary>
public struct StructSessionFunctions<TKey, TValue> : ISessionFunctions<TKey, TValue, TValue, TValue, Empty>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue>
{
    private readonly SimpleFunctions<TKey, TValue> _inner = new();

    public StructSessionFunctions() { }

#if TSAVORITE2_0BETA3
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord, ref TValue input, ref TValue output, ref ReadInfo readInfo)
        where TSourceLogRecord : ISourceLogRecord => _inner.Reader(in srcLogRecord, ref input, ref output, ref readInfo);

    public bool SingleDeleter(ref TKey key, ref TValue value, ref DeleteInfo deleteInfo) => _inner.SingleDeleter(ref key, ref value, ref deleteInfo);

    public bool ConcurrentDeleter(ref TKey key, ref TValue value, ref RecordInfo recordInfo, ref DeleteInfo deleteInfo) => _inner.ConcurrentDeleter(ref key, ref value, ref recordInfo, ref deleteInfo);
    
    public bool InPlaceWriter(ref LogRecord logRecord, in RecordSizeInfo sizeInfo, ref TValue input, ReadOnlySpan<byte> srcValue, ref TValue output, ref UpsertInfo upsertInfo) => _inner.InPlaceWriter(ref logRecord, in sizeInfo, ref input, srcValue, ref output, ref upsertInfo);

    public bool InPlaceUpdater(ref LogRecord logRecord, in RecordSizeInfo sizeInfo, ref TValue input, ref TValue output, ref RMWInfo rmwInfo) => _inner.InPlaceUpdater(ref logRecord, in sizeInfo, ref input, ref output, ref rmwInfo);

    public bool InitialWriter(ref LogRecord logRecord, in RecordSizeInfo sizeInfo, ref TValue input, ReadOnlySpan<byte> srcValue, ref TValue output, ref UpsertInfo upsertInfo) => _inner.InitialWriter(ref logRecord, in sizeInfo, ref input, srcValue, ref output, ref upsertInfo);
#endif    
}
