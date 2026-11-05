using Seamless.Collections.Iteration;
using Seamless.Collections.Tsavorite;
using Seamless.Execution;
namespace Seamless.Transactions.Collections;

public enum VersionedRecordEncoding{Canonical,Auto}

/// <summary>私有変更を確定まで隔離し、Tsavoriteのコピー更新と前リンクでsnapshotを選ぶ表。</summary>
public sealed partial class VersionedTransactionalTable<TKey,TRecord>:ITransactionalTable<TKey,TRecord>,IReclaimableTransactionParticipant,IRoutedVersionedTransactionParticipant,IDisposable where TKey:notnull{
    private readonly HistoryStore data;
    private readonly ExecutionScope resources;
    private readonly long memoryLimit;
    private readonly Func<TRecord,TKey> keyOf;
    private readonly VersionedRecordEncoding encoding;
    private readonly bool supportsJoinKeyEncoding;
    private readonly global::Seamless.Execution.Codecs.IAdapter<TKey> keyCodec;
    private readonly global::Seamless.Execution.Codecs.IAdapter<TRecord> rowCodec;
    private ParallelRegion<HistoryAccessor>? region;
    public static string StorageIdentity(string codecIdentity,VersionedRecordEncoding encoding)=>codecIdentity+"|RowEncoding="+encoding;
    public VersionedTransactionalTable(int partition,int tableId,string codecIdentity,HistoryStore data,ExecutionScope resources,long memoryLimit,Func<TRecord,TKey> keyOf,VersionedRecordEncoding encoding=VersionedRecordEncoding.Canonical){
        if(!Enum.IsDefined(encoding)||data.SchemaIdentity!=StorageIdentity(codecIdentity,encoding)){throw new InvalidDataException("The versioned row encoding does not match the saved schema identity.");}this.encoding=encoding;
        this.Partition=partition;this.TableId=tableId;this.CodecIdentity=codecIdentity;this.data=data;this.resources=resources;this.memoryLimit=memoryLimit;this.keyOf=keyOf;
        this.keyCodec=resources.Codecs.Get<TKey>();this.rowCodec=resources.Codecs.Get<TRecord>();this.region=data.BeginParallel();
        // Canonicalの整数表現は、LINQの既定等値比較と同じ値だけを同じバイト列にする。
        this.supportsJoinKeyEncoding=encoding==VersionedRecordEncoding.Canonical&&!resources.Codecs.HasCustomCodecs
            &&(typeof(TKey)==typeof(int)||typeof(TKey)==typeof(uint)||typeof(TKey)==typeof(long)||typeof(TKey)==typeof(ulong)
                ||typeof(TKey)==typeof(short)||typeof(TKey)==typeof(ushort)||typeof(TKey)==typeof(byte)||typeof(TKey)==typeof(sbyte));
        if(data.HasRecoveredCheckpoint||data.MaximumCommittedVersion!=0){this.snapshotState=1;}
    }
    public int Partition{get;}
    public VersionedParticipantRoute? Routing{get;}
    public VersionedTransactionalTable(int partition,int tableId,string codecIdentity,HistoryStore data,ExecutionScope resources,long memoryLimit,Func<TRecord,TKey> keyOf,VersionedRecordEncoding encoding,VersionedParticipantRoute routing)
        :this(ValidateRoute(partition,routing),tableId,codecIdentity,data,resources,memoryLimit,keyOf,encoding){
        this.Routing=routing;
    }
    private static int ValidateRoute(int partition,VersionedParticipantRoute routing){ArgumentNullException.ThrowIfNull(routing);if(routing.Group.Partition!=partition){throw new ArgumentException("The routed table must belong to the same database partition.",nameof(routing));}return partition;}
    public int TableId{get;}
    public string CodecIdentity{get;}
    public void PrepareForTransactions()=>this.SealUnpublishedSnapshot();
    public long RecoveredVersion{get{this.CheckUnpublishedSnapshot(false);return this.data.MaximumCommittedVersion;}}
    public TransactionCollection<TKey,TRecord> For(TransactionScope scope)=>new(this,scope);
    internal bool MatchesJoinKey(Func<TRecord,TKey> expectedKey)=>this.supportsJoinKeyEncoding&&this.keyOf==expectedKey;
    public ITransactionChanges CreateChanges()=>throw new NotSupportedException("A versioned transaction context is required.");
    public IVersionedTransactionChanges CreateChanges(TransactionContext context){this.SealUnpublishedSnapshot();return new Changes(this,context);}
    public void Replay(WalEntry entry)=>throw new NotSupportedException("Versioned replay requires an installation context.");
    public void Replay(WalEntry entry,TransactionInstallationContext context){
        this.SealUnpublishedSnapshot();
        if(entry.Partition!=this.Partition||entry.TableId!=this.TableId||entry.Codec!=this.CodecIdentity){throw new InvalidDataException("Collection identity mismatch.");}
        var key=this.keyCodec.Decode(entry.Key);
        if(entry.Mutation!=WalMutation.Delete){
            if(entry.Row is null){throw new InvalidDataException("Missing row.");}
            if(!EqualityComparer<TKey>.Default.Equals(key,this.keyOf(this.rowCodec.Decode(entry.Row)))){throw new InvalidDataException("WAL row/key mismatch.");}
        }
        using var access=this.Access();TRecord record=default!;if(entry.Row is not null){record=this.rowCodec.Decode(entry.Row);}this.Append(access,key,record,context.CommitVersion,entry.Mutation==WalMutation.Delete);
    }
    private HistoryAccessor Access()=>(this.region??throw new InvalidOperationException("The versioned table is closed for maintenance.")).Create();
    public ValueTask ReclaimAsync(){this.CloseForMaintenance();this.data.Reclaim();return ValueTask.CompletedTask;}
    public void CloseForMaintenance(){this.SealUnpublishedSnapshot();Interlocked.Exchange(ref this.region,null)?.Dispose();}
    public void ReopenAfterMaintenance(){this.SealUnpublishedSnapshot();if(this.region is not null){throw new InvalidOperationException("The table is already open.");}this.region=this.data.BeginParallel();}
    public void Dispose(){lock(this.snapshotGate){if(this.snapshotState==4){return;}try{this.snapshot?.Dispose();}finally{this.snapshotState=4;Interlocked.Exchange(ref this.region,null)?.Dispose();}}}
    private TRecord Clone(TRecord row){using var bytes=this.resources.Encode(row);return this.rowCodec.Decode(bytes.Memory.Span);}
    private TKey DecodeStoreKey(ReadOnlySpan<byte> bytes){if(this.encoding==VersionedRecordEncoding.Auto){return global::Seamless.Collections.Serializers.Auto<TKey>.Deserialize(bytes);}return this.keyCodec.Decode(bytes);}
    private TRecord DecodeStoreRow(ReadOnlySpan<byte> bytes){if(this.encoding==VersionedRecordEncoding.Auto){return global::Seamless.Collections.Serializers.Auto<TRecord>.Deserialize(bytes);}return this.rowCodec.Decode(bytes);}
    private HistoryRecord? ReadStored(HistoryAccessor access,TKey key,long? version=null){
        if(this.encoding==VersionedRecordEncoding.Auto){var bytes=global::Seamless.Collections.Serializers.Auto<TKey>.Serialize(ref key);if(version.HasValue){return access.ReadVisibleRecord(bytes,version.Value);}return access.ReadRecord(bytes);}
        using var encoded=this.resources.Encode(key);if(version.HasValue){return access.ReadVisibleRecord(encoded.Memory.Span,version.Value);}return access.ReadRecord(encoded.Memory.Span);
    }
    private long EstimateAppend(TKey key,TRecord row,bool deleted){
        if(this.encoding==VersionedRecordEncoding.Auto){
            var keyLength=global::Seamless.Collections.Serializers.Auto<TKey>.Serialize(ref key).Length;var rowLength=0;
            if(!deleted){rowLength=global::Seamless.Collections.Serializers.Auto<TRecord>.Serialize(ref row).Length;}return this.data.EstimateAppendBytes(keyLength,rowLength);
        }
        var length=0;if(!deleted){length=this.rowCodec.GetEncodedLength(row);}return this.data.EstimateAppendBytes(this.keyCodec.GetEncodedLength(key),length);
    }
    private void Append(HistoryAccessor access,TKey key,TRecord row,long version,bool deleted){
        if(this.encoding==VersionedRecordEncoding.Auto){
            var keyBytes=global::Seamless.Collections.Serializers.Auto<TKey>.Serialize(ref key);
            if(deleted){access.AppendCommittedVersion(keyBytes,[],version,true);}
            else{var rowBytes=global::Seamless.Collections.Serializers.Auto<TRecord>.Serialize(ref row);access.AppendCommittedVersion(keyBytes,rowBytes,version,false);}return;
        }
        using var encodedKey=this.resources.Encode(key);
        if(deleted){access.AppendCommittedVersion(encodedKey.Memory.Span,[],version,true);}
        else{using var encodedRow=this.resources.Encode(row);access.AppendCommittedVersion(encodedKey.Memory.Span,encodedRow.Memory.Span,version,false);}
    }

    private sealed partial class Changes:ITableChanges<TKey,TRecord>,IVersionedTransactionChanges{
        private readonly VersionedTransactionalTable<TKey,TRecord> table;
        private readonly TransactionContext context;
        private TransactionChangeSet<TKey,TRecord> changes;
        private readonly global::System.Collections.Generic.HashSet<ScanState> scans=[];
        private bool disposed;
        internal Changes(VersionedTransactionalTable<TKey,TRecord> table,TransactionContext context){this.table=table;this.context=context;this.changes=new(table.resources,table.memoryLimit);}
        public (bool Found,TRecord Row) Read(TKey key){
            this.context.ThrowIfUnavailable();using var encoded=this.table.resources.Encode(key);this.context.ObserveRead(this.table,encoded.Memory.Span);
            if(this.changes.TryGetValue(key,out var own)){if(own.Deleted){return(false,default!);}return(true,this.table.Clone(own.Record));}
            using var working=this.table.resources.Budget.ReserveMemory(checked(this.table.data.PageBytes*4));using var access=this.table.Access();
            // Canonicalでは競合判定と履歴索引が同じキー表現を使う。
            var visible=this.table.encoding==VersionedRecordEncoding.Canonical
                ?access.ReadVisibleRecord(encoded.Memory.Span,this.context.ReadVersion)
                :this.table.ReadStored(access,key,this.context.ReadVersion);
            if(!visible.HasValue||visible.Value.Deleted){return(false,default!);}return(true,this.table.DecodeStoreRow(visible.Value.Row));
        }
        public void Set(TKey key,TRecord row){
            this.context.EnsureWritable();if(!EqualityComparer<TKey>.Default.Equals(key,this.table.keyOf(row))){throw new ArgumentException("Record key does not match the indexer key.",nameof(row));}
            using var encoded=this.table.resources.Encode(key);this.context.ObserveWrite(this.table,encoded.Memory.Span);
            this.changes.Set(this.table.keyCodec.Decode(encoded.Memory.Span),new(false,this.table.Clone(row)));
        }
        public bool Add(TRecord row){this.context.EnsureWritable();var key=this.table.keyOf(row);if(this.Read(key).Found){return false;}this.Set(key,row);return true;}
        public bool Remove(TKey key){
            this.context.EnsureWritable();if(!this.Read(key).Found){return false;}
            using var encoded=this.table.resources.Encode(key);this.context.ObserveWrite(this.table,encoded.Memory.Span);this.changes.Set(this.table.keyCodec.Decode(encoded.Memory.Span),new(true,default!));return true;
        }
        public ITransactionChanges CreateSnapshot(){
            this.context.ThrowIfUnavailable();var copy=new Changes(this.table,this.context);
            try{var changes=this.changes.CreateSnapshot();copy.changes.Dispose();copy.changes=changes;return copy;}catch{copy.Dispose();throw;}
        }
        public IEnumerable<TRecord> Rows(){
            this.context.ThrowIfUnavailable();this.context.ObserveTableRead(this.table);
            var memory=this.table.resources.Budget.ReserveMemory(checked(this.table.data.PageBytes*128+4096));ScanState scan;
            try{using var access=this.table.Access();scan=new(access.OpenSnapshot(this.context.ReadVersion),memory);this.scans.Add(scan);}catch{memory.Dispose();throw;}
            try{while(this.ReadNext(scan,out var row)){yield return row;}}
            finally{this.scans.Remove(scan);scan.Dispose();}
        }
        private bool ReadNext(ScanState scan,out TRecord row){
            this.context.ThrowIfUnavailable();ObjectDisposedException.ThrowIf(this.disposed,this);ObjectDisposedException.ThrowIf(scan.Disposed,scan);
            while(!scan.NativeFinished){
                if(scan.Index>=scan.Batch.Count){
                    scan.ClearBatch();scan.Batch=scan.Cursor.ReadBatch();scan.Index=0;
                    if(scan.Batch.Count==0){scan.NativeFinished=true;break;}
                }
                var record=scan.Batch[scan.Index++];this.context.ThrowIfUnavailable();
                if(!this.changes.IsEmpty&&this.changes.TryGetValue(this.table.DecodeStoreKey(record.Key),out var own)){
                    if(own.Deleted){continue;}row=this.table.Clone(own.Record);return true;
                }
                if(record.Deleted){continue;}row=this.table.DecodeStoreRow(record.Row);return true;
            }
            scan.PrivateChanges??=this.changes.Enumerate().GetEnumerator();
            while(scan.PrivateChanges.MoveNext()){
                this.context.ThrowIfUnavailable();var change=scan.PrivateChanges.Current;if(change.Value.Deleted){continue;}
                using var access=this.table.Access();if(this.table.ReadStored(access,change.Key,this.context.ReadVersion).HasValue){continue;}
                row=this.table.Clone(change.Value.Record);return true;
            }
            row=default!;return false;
        }
        private sealed class ScanState(HistorySnapshotCursor cursor,ResourceReservation memory):IDisposable{
            internal readonly HistorySnapshotCursor Cursor=cursor;
            internal IReadOnlyList<HistoryRecord> Batch=[];
            internal int Index;
            internal bool NativeFinished,Disposed;
            internal IEnumerator<KeyValuePair<TKey,TransactionChange<TRecord>>>? PrivateChanges;
            private readonly ResourceReservation memory=memory;
            internal void ClearBatch(){if(this.Batch is List<HistoryRecord> rows){rows.Clear();}this.Batch=[];}
            public void Dispose(){
                if(this.Disposed){return;}this.Disposed=true;
                try{this.PrivateChanges?.Dispose();}finally{this.PrivateChanges=null;this.ClearBatch();try{this.Cursor.Dispose();}finally{this.memory.Dispose();}}
            }
        }
        private void CloseScans(){
            try{foreach(var scan in this.scans){scan.Dispose();}}finally{this.scans.Clear();}
        }
        public void Iterate<TStrategy>(ref TStrategy strategy) where TStrategy:struct,IStrategy<TStrategy,TRecord>{
            this.context.ThrowIfUnavailable();this.context.ObserveTableRead(this.table);
            using var working=this.table.resources.Budget.ReserveMemory(checked(this.table.data.PageBytes*4));using var access=this.table.Access();
            var overlay=new Overlay<TStrategy>(this,strategy);
            try{
                access.IterateRecords(ref overlay,this.context.ReadVersion);
                if(!overlay.Continue){return;}
                foreach(var change in this.changes.Enumerate()){
                    this.context.ThrowIfUnavailable();if(change.Value.Deleted){continue;}
                    if(this.table.ReadStored(access,change.Key,this.context.ReadVersion).HasValue){continue;}
                    var keep=TStrategy.Invoke(ref overlay.Strategy,this.table.Clone(change.Value.Record));this.context.ThrowIfUnavailable();if(!keep){return;}
                }
            }finally{strategy=overlay.Strategy;}
        }
        public QueryResultBuffer<TResult> CreateResultBuffer<TResult>()=>new(this.table.resources,this.context.ThrowIfUnavailable);
        public ExecutionScope CreateQueryScope()=>this.table.resources.CreateChild(this.context.ThrowIfUnavailable);
        public QueryDictionaryBuffer<TResultKey,TResultValue> CreateDictionaryBuffer<TResultKey,TResultValue>(IEqualityComparer<TResultKey>? comparer) where TResultKey:notnull{
            return new(this.table.resources,comparer,this.context.ThrowIfUnavailable);
        }
        private struct Overlay<TStrategy>(Changes changes,TStrategy strategy):IHistoryStrategy<Overlay<TStrategy>> where TStrategy:struct,IStrategy<TStrategy,TRecord>{
            internal TStrategy Strategy=strategy;
            internal bool Continue=true;
            private readonly Changes owner=changes;
            public static bool Invoke(ref Overlay<TStrategy> self,ReadOnlySpan<byte> key,ReadOnlySpan<byte> row,long version,bool deleted){
                var owner=self.owner;owner.context.ThrowIfUnavailable();TRecord value;
                if(!owner.changes.IsEmpty&&owner.changes.TryGetValue(owner.table.DecodeStoreKey(key),out var own)){if(own.Deleted){return true;}value=owner.table.Clone(own.Record);}
                else{if(deleted){return true;}value=owner.table.DecodeStoreRow(row);}
                self.Continue=TStrategy.Invoke(ref self.Strategy,value);owner.context.ThrowIfUnavailable();return self.Continue;
            }
        }
        public ValueTask<PreparedTransactionParticipant> PrepareAsync(TransactionPreparationContext preparation,CancellationToken cancellationToken){
            this.context.ThrowIfUnavailable();this.CloseScans();using var working=this.table.resources.Budget.ReserveMemory(checked(this.table.data.PageBytes*4));
            long count=0,append=0,memory=0,storage=0;using var access=this.table.Access();
            foreach(var pair in this.changes.Enumerate()){
                cancellationToken.ThrowIfCancellationRequested();append=checked(append+this.table.EstimateAppend(pair.Key,pair.Value.Record,pair.Value.Deleted));count=checked(count+1);
                var previous=this.table.ReadStored(access,pair.Key);
                if(previous.HasValue){
                    // 旧レコードとページ末尾の空きを保守的な二倍幅で計上する。現行行を含むTail-Beginを履歴量にしない。
                    var retained=this.table.data.EstimateAppendBytes(previous.Value.Key.Length,previous.Value.Row.Length);
                    if(this.table.data.Kind==StoreKind.Memory){memory=checked(memory+retained);}else{storage=checked(storage+retained);}
                }
            }
            var history=this.context.ReserveHistory(memory,storage);
            try{
                var capacity=this.table.data.ReserveAppend(append);
                try{return ValueTask.FromResult<PreparedTransactionParticipant>(new Preparation(this,count,history,capacity,preparation));}catch{capacity.Dispose();throw;}
            }catch{history.Dispose();throw;}
        }
        public void Install(TransactionInstallationContext installation,PreparedTransactionParticipant preparation){
            var prepared=(Preparation)preparation;if(!ReferenceEquals(prepared.Owner,this)){throw new InvalidOperationException("The preparation belongs to a different table change set.");}
            using var access=this.table.Access();
            foreach(var pair in this.changes.Enumerate()){
                this.table.Append(access,pair.Key,pair.Value.Record,installation.CommitVersion,pair.Value.Deleted);
            }
            prepared.History.Retain();
        }
        public IReadOnlyList<WalEntry> Encode()=>throw new NotSupportedException("Prepare the versioned change set before encoding WAL rows.");
        public void Apply()=>throw new NotSupportedException("Versioned changes require atomic domain installation.");
        public void Dispose(){
            if(this.disposed){return;}this.disposed=true;
            try{this.CloseScans();}finally{this.changes.Dispose();}
        }
        private sealed class Preparation:PreparedTransactionParticipant{
            internal readonly Changes Owner;
            internal readonly TransactionHistoryReservation History;
            private readonly HistoryAppendReservation capacity;
            private readonly TransactionPreparationContext context;
            private readonly ResourceReservation working,walMemory;
            private WalEntry[]? encoded;
            public override long MutationCount{get;}
            internal Preparation(Changes owner,long count,TransactionHistoryReservation history,HistoryAppendReservation capacity,TransactionPreparationContext context){
                this.Owner=owner;this.MutationCount=count;this.History=history;this.capacity=capacity;this.context=context;
                this.working=owner.table.resources.Budget.ReserveMemory(checked(owner.table.data.PageBytes*4));
                try{this.walMemory=owner.table.resources.Budget.ReserveMemory(0);}catch{this.working.Dispose();throw;}
            }
            public override IEnumerable<WalEntry> Entries{
                get{
                    if(!this.context.WalRequired){throw new InvalidOperationException("A memory transaction must not encode WAL rows.");}
                    if(this.context.MaximumFrameBytes==int.MaxValue){return this.StreamEntries();}
                    if(this.encoded is null){this.encoded=this.StreamEntries().ToArray();}return this.encoded;
                }
            }
            private IEnumerable<WalEntry> StreamEntries(){
                var table=this.Owner.table;long bytes=0;
                foreach(var pair in this.Owner.changes.Enumerate()){
                    using var key=table.resources.Encode(pair.Key);byte[]? row=null;var mutation=WalMutation.Delete;var rowLength=0;
                    if(!pair.Value.Deleted){rowLength=table.rowCodec.GetEncodedLength(pair.Value.Record);mutation=WalMutation.Upsert;}
                    bytes=checked(bytes+WalFormat.EntryPayloadBytes(table.CodecIdentity,key.Memory.Length,rowLength));
                    if(bytes>this.context.MaximumFrameBytes){throw new TransactionAbortedException(this.Owner.context.Id,TransactionAbortReason.ResourceLimit);}
                    if(this.context.MaximumFrameBytes==int.MaxValue){this.walMemory.Resize(checked(128L+key.Memory.Length+rowLength));}
                    else{this.walMemory.Resize(checked(this.walMemory.Bytes+128L+key.Memory.Length+rowLength));}
                    if(!pair.Value.Deleted){using var encoded=table.resources.Encode(pair.Value.Record);row=encoded.Memory.ToArray();}
                    yield return new(table.Partition,table.TableId,mutation,table.CodecIdentity,key.Memory.ToArray(),row);
                }
            }
            public override void Dispose(){try{this.capacity.Dispose();this.History.Dispose();}finally{this.working.Dispose();this.walMemory.Dispose();}}
        }
    }
}
