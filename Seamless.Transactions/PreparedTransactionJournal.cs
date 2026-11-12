using System.Security.Cryptography;
using Seamless.Execution;

namespace Seamless.Transactions;

public enum PreparedTransactionPhase{PreparationDurable,CommitDurable}
public delegate void PreparedTransactionReplay(Guid id,long lsn,ReadOnlyMemory<byte> metadata,IEnumerable<WalEntry> entries);

/// <summary>新保存プロファイル用。準備チャンクを単独では再生せず、最後の commit と hash に結び付ける。</summary>
public sealed class PreparedTransactionJournal:IAsyncDisposable{
    private const ulong Magic=0x314B4E48434C5753;
    private const int PreparationMetadataBytes=28;
    private const int CommitMetadataBytes=68;
    private readonly TransactionJournal journal;
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly Action<PreparedTransactionPhase>? failureInjection;
    private bool recovered;
    private bool disposed;

    public PreparedTransactionJournal(string directory,WalOptions? options=null,ResourceBudget? budget=null,Action<PreparedTransactionPhase>? failureInjection=null){
        options??=new();options=options with{CatalogIdentity=options.CatalogIdentity+"|PreparedTransactionsV1"};
        this.journal=new(directory,options,budget);this.failureInjection=failureInjection;this.recovered=this.journal.Device.RecoveredLsn==0;
    }
    public TransactionOutcome GetOutcome(Guid id)=>this.journal.GetOutcome(id);
    public long DurableLsn=>this.journal.Writer.DurableLsn;
    internal ValueTask ForgetThroughAsync(long lsn)=>this.journal.ForgetThroughAsync(lsn);
    public Guid WalId=>this.journal.Device.WalId;
    internal TransactionFailure Failure=>this.journal.Writer.Failure;
    internal int MaximumMetadataBytes=>this.journal.Writer.MaximumFrameBytes-64-CommitMetadataBytes;
    public void RecordAborted(Guid id){if(id==Guid.Empty){throw new ArgumentException("A transaction ID is required.",nameof(id));}this.journal.RecordAborted(id);}

    public async ValueTask<TransactionCommitResult> CommitAsync(Guid id,IEnumerable<WalEntry> entries,ReadOnlyMemory<byte> metadata=default,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(entries);if(id==Guid.Empty){throw new ArgumentException("A transaction ID is required.",nameof(id));}
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);var durable=false;var accepted=false;var preparing=false;
        try{
            ObjectDisposedException.ThrowIf(this.disposed,this);if(!this.recovered){throw new InvalidOperationException("Replay the common WAL before accepting transactions.");}
            if(this.journal.GetOutcome(id).Kind!=TransactionOutcomeKind.Unknown){throw new InvalidOperationException("The transaction ID has already completed.");}
            var maximum=this.journal.Writer.MaximumFrameBytes;
            if(metadata.Length>maximum-64-CommitMetadataBytes){throw new InvalidDataException("The commit metadata exceeds the WAL frame bound.");}
            using var working=this.journal.Budget.ReserveMemory(checked(3L*maximum));using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var chunk=new List<WalEntry>();var parts=new SortedSet<int>();long bytes=60+PreparationMetadataBytes;long first=0,last=0,rows=0;var sequence=0;preparing=true;
            async ValueTask Flush(){
                if(chunk.Count==0){return;}
                cancellationToken.ThrowIfCancellationRequested();
                var draft=new WalDraft(id,parts.ToArray(),chunk.ToArray(),PreparationMetadata(id,sequence)){IsPreparation=true};
                var receipt=await this.journal.AppendAsync(draft,cancellationToken).ConfigureAwait(false);await receipt.Durable.ConfigureAwait(false);
                if(first==0){first=receipt.Lsn;}last=receipt.Lsn;rows=checked(rows+chunk.Count);sequence=checked(sequence+1);
                hash.AppendData(SHA256.HashData(WalFormat.Encode(new(receipt.Lsn,draft),maximum)));
                this.failureInjection?.Invoke(PreparedTransactionPhase.PreparationDurable);chunk.Clear();parts.Clear();bytes=60+PreparationMetadataBytes;
            }
            foreach(var entry in entries){
                cancellationToken.ThrowIfCancellationRequested();var size=WalFormat.EntryPayloadBytes(entry.Codec,entry.Key.Length,entry.Row?.Length??0);
                if(size+64+PreparationMetadataBytes>maximum){throw new InvalidDataException("One mutation exceeds the preparation chunk bound.");}
                if(bytes+size+(parts.Contains(entry.Partition)?0:4)>maximum){await Flush().ConfigureAwait(false);}
                if(parts.Add(entry.Partition)){bytes=checked(bytes+4);}bytes=checked(bytes+size);
                // 呼出側が列挙用バッファを再利用しても、既に集めた変更を壊さない。
                chunk.Add(entry with{Key=entry.Key.ToArray(),Row=entry.Row?.ToArray()});
            }
            await Flush().ConfigureAwait(false);cancellationToken.ThrowIfCancellationRequested();
            var final=new WalDraft(id,[0],[],CommitMetadata(first,last,sequence,rows,hash.GetHashAndReset(),metadata.Span));
            var committed=await this.journal.AppendAsync(final,cancellationToken).ConfigureAwait(false);accepted=true;await committed.Durable.ConfigureAwait(false);durable=true;
            this.failureInjection?.Invoke(PreparedTransactionPhase.CommitDurable);return new(id,TransactionCommitKind.Committed,committed.Lsn);
        }catch(Exception error)when(preparing&&error is not TransactionCommitException){
            if(!accepted){this.journal.RecordAborted(id);}
            var outcome=durable?TransactionOutcomeKind.Committed:accepted?TransactionOutcomeKind.Unknown:TransactionOutcomeKind.Aborted;
            throw new TransactionCommitException(id,outcome,error);
        }finally{this.gate.Release();}
    }

    /// <summary>checkpoint後の全確定記録を先に検証してから、確定単位で逐次再生する。callback終了までentriesを列挙する。</summary>
    public void Replay(PreparedTransactionReplay apply,long checkpointLsn=0){
        ArgumentNullException.ThrowIfNull(apply);this.gate.Wait();
        try{
            ObjectDisposedException.ThrowIf(this.disposed,this);this.recovered=false;
            if(checkpointLsn<0||checkpointLsn>this.journal.Writer.DurableLsn||checkpointLsn<this.journal.Device.FirstAvailableLsn-1){throw new InvalidDataException("The common checkpoint is outside retained WAL coverage.");}
            using var indexMemory=this.journal.Budget.ReserveMemory(0);using var working=this.journal.Budget.ReserveMemory(checked(3L*this.journal.Writer.MaximumFrameBytes));
            var commits=new List<(Guid Id,long Lsn,CommitInfo Info)>();using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);Guid pending=default;long first=0,last=0,rows=0;var sequence=0;
            foreach(var record in this.journal.Device.ReadTransactions(this.journal.Writer.DurableLsn)){
                if(record.Lsn<=checkpointLsn){continue;}
                if(record.Draft.IsPreparation){
                    var ordinal=ReadPreparation(record);
                    if(ordinal==0){pending=record.Draft.Id;first=record.Lsn;last=0;rows=0;sequence=0;hash.GetHashAndReset();}
                    if(pending!=record.Draft.Id||ordinal!=sequence||(last!=0&&record.Lsn!=last+1)){throw new InvalidDataException("Preparation sequence mismatch.");}
                    hash.AppendData(SHA256.HashData(WalFormat.Encode(record,this.journal.Writer.MaximumFrameBytes)));last=record.Lsn;rows=checked(rows+record.Draft.Entries.Count);sequence=checked(sequence+1);continue;
                }
                if(record.Draft.Entries.Count!=0){throw new InvalidDataException("A prepared-profile commit must refer to its prepared rows.");}
                var info=ReadCommit(record.Draft.Metadata);
                if(info.Count==0){if(info.First!=0||info.Last!=0||info.Rows!=0||!info.Hash.AsSpan().SequenceEqual(SHA256.HashData(Array.Empty<byte>()))){throw new InvalidDataException("Invalid metadata-only commit.");}}
                else if(pending!=record.Draft.Id||info.First!=first||info.Last!=last||info.Count!=sequence||info.Rows!=rows||record.Lsn!=last+1||!info.Hash.AsSpan().SequenceEqual(hash.GetHashAndReset())){throw new InvalidDataException("The final commit does not match its preparations.");}
                indexMemory.Resize(checked(indexMemory.Bytes+128L+info.Metadata.Length));commits.Add((record.Draft.Id,record.Lsn,info));pending=default;first=last=rows=0;sequence=0;hash.GetHashAndReset();
            }
            // WalFileDevice の列挙は装置排他を持つため、検証走査を閉じてから再生走査を開く。
            foreach(var commit in commits){
                var consumed=false;long delivered=0;
                IEnumerable<WalEntry> Read(){
                    if(commit.Info.Count==0){yield break;}
                    foreach(var record in this.journal.Device.ReadTransactions(commit.Info.Last)){
                        if(record.Lsn<commit.Info.First){continue;}
                        foreach(var entry in record.Draft.Entries){delivered=checked(delivered+1);yield return entry;}
                    }
                }
                using var iterator=Read().GetEnumerator();
                IEnumerable<WalEntry> Once(){
                    if(consumed){throw new InvalidOperationException("Replay entries can be consumed only once.");}consumed=true;
                    while(iterator.MoveNext()){yield return iterator.Current;}
                }
                apply(commit.Id,commit.Lsn,commit.Info.Metadata,Once());
                if(delivered!=commit.Info.Rows){throw new InvalidOperationException("The replay callback did not consume all committed rows.");}
            }
            this.recovered=true;
        }finally{this.gate.Release();}
    }

    private static byte[] PreparationMetadata(Guid id,int sequence){
        using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);writer.Write(Magic);writer.Write(id.ToByteArray());writer.Write(sequence);return stream.ToArray();
    }
    private static int ReadPreparation(WalTransaction record){
        var bytes=record.Draft.Metadata;if(bytes.Length!=PreparationMetadataBytes){throw new InvalidDataException("Invalid preparation metadata length.");}
        using var reader=new BinaryReader(new MemoryStream(bytes));if(reader.ReadUInt64()!=Magic||new Guid(reader.ReadBytes(16))!=record.Draft.Id){throw new InvalidDataException("Invalid preparation identity.");}
        var sequence=reader.ReadInt32();if(sequence<0||record.Draft.Entries.Count==0){throw new InvalidDataException("Invalid empty/negative preparation.");}return sequence;
    }
    private static byte[] CommitMetadata(long first,long last,int count,long rows,byte[] hash,ReadOnlySpan<byte> metadata){
        using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);writer.Write(Magic);writer.Write(first);writer.Write(last);writer.Write(count);writer.Write(rows);writer.Write(hash);writer.Write(metadata);return stream.ToArray();
    }
    private static CommitInfo ReadCommit(byte[] bytes){
        if(bytes.Length<CommitMetadataBytes){throw new InvalidDataException("Invalid commit metadata length.");}
        using var reader=new BinaryReader(new MemoryStream(bytes));if(reader.ReadUInt64()!=Magic){throw new InvalidDataException("Invalid prepared-profile commit.");}
        var first=reader.ReadInt64();var last=reader.ReadInt64();var count=reader.ReadInt32();var rows=reader.ReadInt64();var hash=reader.ReadBytes(32);
        if(first<0||last<first||count<0||rows<0||count!=0&&(first==0||last-first+1!=count)){throw new InvalidDataException("Invalid commit preparation range.");}
        return new(first,last,count,rows,hash,reader.ReadBytes(checked(bytes.Length-CommitMetadataBytes)));
    }
    private sealed record CommitInfo(long First,long Last,int Count,long Rows,byte[] Hash,byte[] Metadata);
    public async ValueTask CheckpointAsync(Func<Guid,long,ValueTask> saveAllParticipants){
        ArgumentNullException.ThrowIfNull(saveAllParticipants);await this.gate.WaitAsync().ConfigureAwait(false);
        try{
            ObjectDisposedException.ThrowIf(this.disposed,this);if(!this.recovered){throw new InvalidOperationException("Finish recovery before checkpointing.");}
            var fence=this.journal.Writer.DurableLsn;await saveAllParticipants(this.WalId,fence).ConfigureAwait(false);
            await this.journal.CheckpointAndPruneAsync(fence).ConfigureAwait(false);
        }finally{this.gate.Release();}
    }
    public async ValueTask DisposeAsync(){
        await this.gate.WaitAsync().ConfigureAwait(false);try{if(this.disposed){return;}await this.journal.DisposeAsync().ConfigureAwait(false);this.disposed=true;}finally{this.gate.Release();}
    }
}
