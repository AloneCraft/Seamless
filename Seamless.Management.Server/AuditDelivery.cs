using System.Runtime.CompilerServices;
using MemoryPack;
using Seamless.Database.Client;
using Seamless.Transactions;

namespace Seamless.Management.Server;

internal readonly record struct AuditDeliveryReceipt(long AfterLsn,long ThroughLsn,long DurableCeiling,int EventCount,bool HasMore);
internal sealed class AuditDelivery{
    private static readonly ConditionalWeakTable<TransactionJournal,SemaphoreSlim> Gates=new();
    internal Action<string>? Checkpoint{get;set;}
    internal async Task<AuditDeliveryReceipt> DeliverOnceAsync(TransactionJournal source,ManagementStore target,CancellationToken cancellationToken=default){
        if(source.MetadataConsumer!="audit-v1"){throw new NotSupportedException("The audit-v1 metadata profile is required.");}
        if(!target.HasAudit){throw new ManagementException("MigrationRequired","監査テーブルを明示導入してください。");}
        var gate=Gates.GetValue(source,static _=>new(1,1));await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try{
            cancellationToken.ThrowIfCancellationRequested();using var page=source.ReadMetadataBatch();
            using var decoded=source.Budget.ReserveMemory(checked(page.PayloadBytes*3L+page.Records.Count*512L));
            var events=new List<AuditEvent>(page.Records.Count);foreach(var record in page.Records){cancellationToken.ThrowIfCancellationRequested();events.Add(AuditWalPayload.Decode(record.Payload.Span,target.InstanceId,source.Device.WalId,record.TransactionId));}
            if(events.Count>0){
                this.Checkpoint?.Invoke("BeforeTargetCommit");
                target.Write(()=>{foreach(var value in events){cancellationToken.ThrowIfCancellationRequested();target.AppendAuditInside(value);}});
                this.Checkpoint?.Invoke("TargetCommitted");
            }
            cancellationToken.ThrowIfCancellationRequested();source.AcknowledgeMetadataThrough(page.ThroughLsn);this.Checkpoint?.Invoke("Acknowledged");
            return new(page.AfterLsn,page.ThroughLsn,page.DurableCeiling,events.Count,page.HasMore);
        }finally{gate.Release();}
    }
}
internal static class AuditWalPayload{
    private const int PrefixBytes=72;
    private const int MaximumBytes=128<<10;
    private static readonly byte[] Contract=Convert.FromHexString(NativeWireContract.For<AuditEvent>());
    private static readonly NativeWireLimits Limits=new(){MaximumBytes=MaximumBytes,MaximumStringCharacters=8192,MaximumArrayElements=1,MaximumValueNodes=256};
    internal static byte[] Encode(Guid target,Guid wal,AuditEvent value){
        ManagementStore.ValidateAuditEvent(value);if(target==Guid.Empty||wal==Guid.Empty||value.TransactionId is null||value.TransactionId==Guid.Empty){throw new InvalidDataException("Audit WAL identity is required.");}
        var bytes=MemoryPackSerializer.Serialize(value);if(bytes.Length>MaximumBytes-PrefixBytes){throw new InvalidDataException("Audit metadata exceeds its bound.");}NativeWireValidation.Validate<AuditEvent>(bytes,Limits);
        var result=new byte[PrefixBytes+bytes.Length];"SLAUD001"u8.CopyTo(result);Contract.CopyTo(result,8);target.TryWriteBytes(result.AsSpan(40,16));wal.TryWriteBytes(result.AsSpan(56,16));bytes.CopyTo(result,PrefixBytes);return result;
    }
    internal static AuditEvent Decode(ReadOnlySpan<byte> bytes,Guid target,Guid wal,Guid transaction){
        if(bytes.Length<=PrefixBytes||bytes.Length>MaximumBytes||!bytes[..8].SequenceEqual("SLAUD001"u8)||!bytes.Slice(8,32).SequenceEqual(Contract)||new Guid(bytes.Slice(40,16))!=target||new Guid(bytes.Slice(56,16))!=wal){throw new InvalidDataException("Audit metadata format/contract/destination mismatch.");}
        var body=bytes[PrefixBytes..];NativeWireValidation.Validate<AuditEvent>(body,Limits);var value=MemoryPackSerializer.Deserialize<AuditEvent>(body);ManagementStore.ValidateAuditEvent(value);if(transaction==Guid.Empty||value.TransactionId!=transaction){throw new InvalidDataException("Audit transaction identity mismatch.");}return value;
    }
}
