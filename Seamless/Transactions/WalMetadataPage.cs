using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Seamless.Execution;

namespace Seamless.Transactions;

public readonly record struct WalMetadataRecord(Guid TransactionId,long Lsn,ReadOnlyMemory<byte> Payload);
internal readonly record struct WalMetadataPosition(long Segment,long Offset,long ExpectedLsn);
/// <summary>確定済みWALの有界な付随情報。読取では処理済み位置を進めない。</summary>
public sealed class WalMetadataPage:IDisposable{
    private readonly ResourceReservation memory;
    internal WalMetadataPosition? NextPosition{get;set;}
    internal WalMetadataPage(long after,long ceiling,ResourceReservation memory){this.AfterLsn=after;this.ThroughLsn=after;this.DurableCeiling=ceiling;this.memory=memory;}
    public long AfterLsn{get;}
    public long ThroughLsn{get;internal set;}
    public long DurableCeiling{get;}
    public bool HasMore=>this.ThroughLsn<this.DurableCeiling;
    public IReadOnlyList<WalMetadataRecord> Records{get;internal set;}=Array.Empty<WalMetadataRecord>();
    public long ScannedBytes{get;internal set;}
    public int ScannedFrames{get;internal set;}
    public int PayloadBytes{get;internal set;}
    public void Dispose()=>this.memory.Dispose();
}
