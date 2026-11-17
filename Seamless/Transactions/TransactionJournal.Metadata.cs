using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Seamless.Execution;

namespace Seamless.Transactions;

public sealed partial class TransactionJournal{
    private readonly string? metadataConsumerName;
    private long acknowledgedMetadataLsn;
    private ResourceReservation? metadataAckStorage;
    private string MetadataAckPath=>WalMetadataAcknowledgement.GetPath(this.Device.DirectoryPath);
    internal Action<string>? MetadataCheckpoint{get;set;}
    public string? MetadataConsumer=>this.metadataConsumerName;
    private WalMetadataPosition? metadataPosition;
    private WalMetadataPosition? metadataCandidate;
    private long metadataCandidateThrough=-1;
    public WalMetadataPage ReadMetadataBatch(int maximumRecords=128,int maximumPayloadBytes=1<<20,int maximumScanBytes=8<<20,int maximumFrames=256){
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);this.RequireMetadataConsumer();this.Writer.Failure.ThrowIfFailed();
            if(maximumRecords is <1 or >4096||maximumPayloadBytes is <1 or >16<<20||maximumScanBytes is <8192 or >64<<20||maximumFrames is <1 or >4096){throw new ArgumentOutOfRangeException(nameof(maximumRecords),"Invalid metadata page bounds.");}
            var page=this.Device.ReadMetadata(this.acknowledgedMetadataLsn,this.Writer.DurableLsn,this.metadataPosition,maximumRecords,maximumPayloadBytes,maximumScanBytes,maximumFrames,this.Budget);
            if(page.ThroughLsn==this.acknowledgedMetadataLsn){this.metadataPosition=page.NextPosition;}
            this.metadataCandidate=page.NextPosition;this.metadataCandidateThrough=page.ThroughLsn;return page;
        }
    }
    /// <summary>利用側が永続的に処理済みとしたLSN。未処理prefixをcheckpointだけで削除しない。</summary>
    public long MetadataAcknowledgedLsn{get{lock(this.gate){ObjectDisposedException.ThrowIf(this.disposed,this);this.RequireMetadataConsumer();return this.acknowledgedMetadataLsn;}}}
    private void RequireMetadataConsumer(){if(this.metadataConsumerName is null){throw new NotSupportedException("Select the metadata-retaining profile explicitly on a new WAL.");}}
    private void InitializeMetadataRetention(){
        if(this.metadataConsumerName is null){return;}this.metadataAckStorage=this.Budget.ReserveTemporaryStorage(WalMetadataAcknowledgement.Length*2);File.Delete(this.MetadataAckPath+".new");
        if(!File.Exists(this.MetadataAckPath)){if(this.Device.RecoveredLsn!=0||this.Device.FirstAvailableLsn!=1){throw new InvalidDataException("The required metadata acknowledgement is missing.");}this.WriteMetadataAcknowledgement(0);return;}
        this.acknowledgedMetadataLsn=WalMetadataAcknowledgement.Read(this.Device.DirectoryPath,this.Device.WalId,this.metadataConsumerName,this.Device.RecoveredLsn,this.Device.FirstAvailableLsn-1);
    }
    /// <summary>転記先の確定後だけ進める。保存失敗時は古い削除境界を維持する。</summary>
    public void AcknowledgeMetadataThrough(long lsn){
        lock(this.gate){ObjectDisposedException.ThrowIf(this.disposed,this);this.RequireMetadataConsumer();this.Writer.Failure.ThrowIfFailed();if(lsn<this.acknowledgedMetadataLsn||lsn>this.Writer.DurableLsn){throw new ArgumentOutOfRangeException(nameof(lsn));}if(lsn==this.acknowledgedMetadataLsn){return;}this.WriteMetadataAcknowledgement(lsn);}
    }
    private void WriteMetadataAcknowledgement(long lsn){
        var bytes=WalMetadataAcknowledgement.Encode(this.Device.WalId,this.metadataConsumerName!,lsn);
        var temporary=this.MetadataAckPath+".new";using(var file=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None)){file.Write(bytes);file.Flush(true);}this.MetadataCheckpoint?.Invoke("Written");File.Move(temporary,this.MetadataAckPath,true);this.acknowledgedMetadataLsn=lsn;
        this.metadataPosition=null;if(this.metadataCandidateThrough==lsn){this.metadataPosition=this.metadataCandidate;}this.metadataCandidate=null;this.metadataCandidateThrough=-1;this.MetadataCheckpoint?.Invoke("Published");
    }
}
