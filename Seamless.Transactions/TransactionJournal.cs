using System.Diagnostics;
using Seamless.Execution;
namespace Seamless.Transactions;
public enum TransactionOutcomeKind{Unknown,Aborted,Committed}
public readonly record struct TransactionOutcome(Guid Id,TransactionOutcomeKind Kind,long Lsn);
/// <summary>WALの派生台帳。台帳を確定するまでWAL prefixを削除しない。</summary>
public sealed partial class TransactionJournal:IAsyncDisposable{
    private static readonly ActivitySource CheckpointActivities=new("Seamless.Checkpoint.Journal");
    private readonly object gate=new();
    private readonly Dictionary<Guid,long> outcomes=[];
    private readonly Dictionary<Guid,long> pendingOutcomes=[];
    private readonly HashSet<Guid> recentAborts=[];
    private readonly Queue<Guid> abortOrder=[];
    private readonly ResourceReservation memory;
    private readonly ResourceReservation storage;
    private long forgottenThrough;
    private bool disposed;
    public WalFileDevice Device{get;}
    public WalWriter Writer{get;}
    public ResourceBudget Budget{get;}
    public WalOptions Options{get;}
    public TransactionJournal(string directory,WalOptions? options=null,ResourceBudget? budget=null,TransactionFailure? failure=null){
        options??=new();this.Options=options;
        this.metadataConsumerName=options?.MetadataConsumer;
        budget??=new ResourceBudget(256L<<20,16L<<30);
        this.Budget=budget;
        this.memory=budget.ReserveMemory(0);this.storage=budget.ReserveTemporaryStorage(0);
        this.Device=new(directory,options,budget);
        try{
            this.InitializeMetadataRetention();
            File.Delete(this.SnapshotPath+".new");
            if(this.Device.FirstAvailableLsn>1&&!File.Exists(this.SnapshotPath)){throw new InvalidDataException("Outcome catalog is missing for a pruned WAL prefix.");}
            this.ReadSnapshot();
            // 起動時だけ保持WALを検証して台帳を再構成する。稼働中は受付済みreceiptとdurable境界から更新する。
            foreach(var transaction in this.Device.ReadTransactions(this.Device.RecoveredLsn)){
                if(!transaction.Draft.IsPreparation&&(transaction.Draft.Entries.Count>0||transaction.Draft.Metadata.Length>0)){this.Add(transaction.Draft.Id,transaction.Lsn);}
            }
            this.Writer=new(this.Device,options,budget,failure,this.Device.RecoveredLsn);
        }catch{this.Device.Dispose();this.memory.Dispose();this.storage.Dispose();this.metadataAckStorage?.Dispose();throw;}
    }
    public IEnumerable<WalTransaction> ReadTransactions()=>this.Device.ReadTransactions();
    public async ValueTask<WalReceipt> AppendAsync(WalDraft draft,CancellationToken cancellationToken=default){
        var tracked=!draft.IsPreparation&&(draft.Entries.Count>0||draft.Metadata.Length>0);
        if(tracked){
            lock(this.gate){
                ObjectDisposedException.ThrowIf(this.disposed,this);
                if(this.outcomes.ContainsKey(draft.Id)||this.pendingOutcomes.ContainsKey(draft.Id)){throw new InvalidOperationException("Transaction identity has already been accepted.");}
                this.memory.Resize(checked((this.outcomes.Count+this.pendingOutcomes.Count+this.recentAborts.Count+1L)*96));this.pendingOutcomes.Add(draft.Id,0);
            }
        }
        try{
            var receipt=await this.Writer.AppendAsync(draft,cancellationToken).ConfigureAwait(false);
            if(tracked){lock(this.gate){this.pendingOutcomes[draft.Id]=receipt.Lsn;}}
            return receipt;
        }
        catch{
            if(tracked){lock(this.gate){this.pendingOutcomes.Remove(draft.Id);this.ResizeMemory();}}
            throw;
        }
    }
    private void ResizeMemory()=>this.memory.Resize(checked((this.outcomes.Count+this.pendingOutcomes.Count+this.recentAborts.Count)*96L));
    public void RecordAborted(Guid id){
        lock(this.gate){
            if(this.recentAborts.Contains(id)){return;}
            if(this.abortOrder.Count==256){this.recentAborts.Remove(this.abortOrder.Dequeue());}
            try{this.memory.Resize(checked((this.outcomes.Count+this.pendingOutcomes.Count+this.recentAborts.Count+1L)*96));}
            catch(ResourceLimitException){return;}
            this.recentAborts.Add(id);this.abortOrder.Enqueue(id);
        }
    }
    public TransactionOutcome GetOutcome(Guid id){
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);this.Refresh(this.Writer.DurableLsn);
            if(this.outcomes.TryGetValue(id,out var lsn)){return new(id,TransactionOutcomeKind.Committed,lsn);}
            if(this.recentAborts.Contains(id)){return new(id,TransactionOutcomeKind.Aborted,0);}
            return new(id,TransactionOutcomeKind.Unknown,0);
        }
    }
    public async ValueTask ForgetThroughAsync(long lsn){
        await this.Writer.WaitForDurableAsync(lsn).ConfigureAwait(false);
        lock(this.gate){
            this.Writer.Failure.ThrowIfFailed();this.Refresh(this.Writer.DurableLsn);
            var previous=this.forgottenThrough;this.forgottenThrough=Math.Max(previous,lsn);
            try{this.SaveSnapshot();}catch(Exception error){this.forgottenThrough=previous;this.Writer.Fail(error);throw;}
            foreach(var id in this.outcomes.Where(p=>p.Value<=this.forgottenThrough).Select(static p=>p.Key).ToArray()){this.outcomes.Remove(id);}
            this.ResizeMemory();
        }
    }
    public async ValueTask CheckpointAndPruneAsync(long checkpointLsn){
        using(CheckpointActivities.StartActivity("Wait for durable WAL")){
            await this.Writer.WaitForDurableAsync(checkpointLsn).ConfigureAwait(false);
        }
        lock(this.gate){
            try{
                this.Writer.Failure.ThrowIfFailed();this.Refresh(this.Writer.DurableLsn);
                using(CheckpointActivities.StartActivity("Save transaction outcomes")){
                    this.SaveSnapshot();
                }
                var prune=checkpointLsn;if(this.metadataConsumerName is not null){prune=Math.Min(prune,this.acknowledgedMetadataLsn);}
                using(CheckpointActivities.StartActivity("Prune WAL")){
                    this.Device.PruneThrough(prune);
                }
            }catch(Exception error){this.Writer.Fail(error);throw;}
        }
    }
    private void Add(Guid id,long lsn){
        if(lsn<=this.forgottenThrough){return;}
        if(this.outcomes.TryGetValue(id,out var prior)){if(prior!=lsn){throw new InvalidDataException("Duplicate committed transaction identity.");}return;}
        if(!this.pendingOutcomes.ContainsKey(id)){this.memory.Resize(checked((this.outcomes.Count+this.pendingOutcomes.Count+this.recentAborts.Count+1L)*96));}
        this.outcomes.Add(id,lsn);this.pendingOutcomes.Remove(id);this.ResizeMemory();
    }
    private void Refresh(long durable){
        // LSN=0はまだwriter受付前。receiptを返す前にLSNを登録し、durable以前はCommittedにしない。
        while(true){
            Guid id=default;long lsn=0;
            foreach(var pending in this.pendingOutcomes){if(pending.Value>0&&pending.Value<=durable){id=pending.Key;lsn=pending.Value;break;}}
            if(lsn==0){return;}
            this.Add(id,lsn);
            // ForgetThroughと受付が重なった場合も、忘却済みreceiptを残さない。
            if(this.pendingOutcomes.Remove(id)){this.ResizeMemory();}
        }
    }
    private string SnapshotPath=>Path.Combine(this.Device.DirectoryPath,"outcomes.snapshot");
    private void ReadSnapshot(){
        if(!File.Exists(this.SnapshotPath)){return;}
        using var stream=File.OpenRead(this.SnapshotPath);using var reader=new BinaryReader(stream);
        var header=reader.ReadBytes(44);
        if(header.Length!=44||System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(header)!=0x325354554F4C5753||new Guid(header.AsSpan(8,16))!=this.Device.WalId||System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(40))!=System.IO.Hashing.Crc32.HashToUInt32(header.AsSpan(0,40))){throw new InvalidDataException("Outcome catalog identity/header CRC mismatch.");}
        this.forgottenThrough=System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(24));var count=System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(32));
        if(this.forgottenThrough<0||this.forgottenThrough>this.Device.RecoveredLsn||count<0||count>(stream.Length-stream.Position)/28){throw new InvalidDataException("Invalid outcome index length.");}
        for(long index=0;index<count;index++){
            var data=reader.ReadBytes(24);var crc=reader.ReadUInt32();
            if(data.Length!=24||System.IO.Hashing.Crc32.HashToUInt32(data)!=crc){throw new InvalidDataException("Outcome record CRC mismatch.");}
            var lsn=System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(16));
            if(lsn<=0||lsn>this.Device.RecoveredLsn){throw new InvalidDataException("Invalid outcome LSN.");}this.Add(new Guid(data.AsSpan(0,16)),lsn);
        }
        if(stream.Position!=stream.Length){throw new InvalidDataException("Trailing outcome bytes.");}
        this.storage.Resize(stream.Length);
    }
    private void SaveSnapshot(){
        var count=this.outcomes.Count(p=>p.Value>this.forgottenThrough);
        var bytes=checked(44L+28L*count);
        // 置換が完了するまで新旧世代の同時存在を予約する。
        var old=this.storage.Bytes;this.storage.Resize(checked(old+bytes));
        var temporary=this.SnapshotPath+".new";
        try{
            using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None)){
                using var writer=new BinaryWriter(stream);
                var header=new byte[44];System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(header,0x325354554F4C5753UL);this.Device.WalId.TryWriteBytes(header.AsSpan(8,16));
                System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(24),this.forgottenThrough);System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(32),count);
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(40),System.IO.Hashing.Crc32.HashToUInt32(header.AsSpan(0,40)));writer.Write(header);
                var data=new byte[24];
                foreach(var pair in this.outcomes){
                    if(pair.Value<=this.forgottenThrough){continue;}
                    pair.Key.TryWriteBytes(data);System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(16),pair.Value);
                    writer.Write(data);writer.Write(System.IO.Hashing.Crc32.HashToUInt32(data));
                }
                writer.Flush();stream.Flush(true);
            }
            File.Move(temporary,this.SnapshotPath,true);this.storage.Resize(bytes);
        }catch(Exception error){
            try{File.Delete(temporary);this.storage.Resize(old);}catch(Exception cleanup){error.Data["snapshot-cleanup-failure"]=cleanup.Message;}
            throw;
        }
    }
    public async ValueTask DisposeAsync(){
        await this.Writer.StopAsync().ConfigureAwait(false);
        lock(this.gate){if(this.disposed){return;}this.disposed=true;}
        try{await this.Writer.DisposeAsync().ConfigureAwait(false);}finally{this.memory.Dispose();this.storage.Dispose();this.metadataAckStorage?.Dispose();}
    }
}
