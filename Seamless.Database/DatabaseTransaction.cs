using Seamless.Transactions.Collections;
namespace Seamless.Database;

/// <summary>データベース全体の直列化可能なトランザクションスコープ。</summary>
public sealed class DatabaseTransaction:IGatewayDatabaseTransaction {
    private readonly DatabaseContainer database;
    private readonly DatabaseTransactionState state;
    private bool commitRequested;
    private bool disposed;
    public bool IsRollbackOnly=>this.state.Aborted;
    internal Guid MetadataTransactionId{get{this.ValidateMetadataContext();return this.state.SharedScope!.Id;}}
    private void ValidateMetadataContext(){
        ObjectDisposedException.ThrowIf(this.disposed,this);this.database.ValidateTransactionContext(this.state);
        if(this.state.Depth!=1||this.state.SharedScope is null||this.state.Aborted){throw new InvalidOperationException("Use the outermost active standalone SharedV2 transaction for metadata.");}
        if(this.database.MetadataJournal is null){throw new NotSupportedException("Select metadata retention explicitly on a new persistent database.");}
    }
    internal void CommitWithMetadata(ReadOnlyMemory<byte> metadata){
        this.ValidateMetadataContext();var journal=this.database.MetadataJournal!;
        if(metadata.IsEmpty||metadata.Length>journal.Writer.MaximumFrameBytes-64){throw new ArgumentOutOfRangeException(nameof(metadata));}
        if(this.state.CommitMetadataMemory is not null){throw new InvalidOperationException("Commit metadata has already been supplied.");}
        var retained=journal.Budget.ReserveMemory(metadata.Length+32L);
        try{this.state.CommitMetadata=metadata.ToArray();this.state.CommitMetadataMemory=retained;this.commitRequested=true;}catch{retained.Dispose();throw;}
    }
    public IEnumerable<global::Seamless.Transactions.WalEntry> PrepareGatewayCommit(GatewayTransactionOwner owner)=>this.database.PrepareGatewayCommit(this.state,owner);
    public void ApplyGatewayCommit(GatewayTransactionOwner owner,long lsn)=>this.database.ApplyGatewayCommit(this.state,owner,lsn);
    public GatewayPreparedSnapshot SealGatewaySnapshot(GatewayTransactionOwner owner)=>this.database.SealGatewaySnapshot(this.state,owner);

    internal DatabaseTransaction(DatabaseContainer database,DatabaseTransactionState state) {
        this.database=database;
        this.state=state;
    }

    public void Commit() {
        ObjectDisposedException.ThrowIf(this.disposed,this);
        this.commitRequested=true;
    }

    public void Rollback() {
        ObjectDisposedException.ThrowIf(this.disposed,this);
        this.state.Aborted=true;
        this.commitRequested=false;
    }

    public void CreateSavepoint(string name){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        this.database.ValidateTransactionContext(this.state);
        this.state.CreateSavepoint(name);
    }

    public void RollbackToSavepoint(string name){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        this.database.ValidateTransactionContext(this.state);
        this.state.RollbackToSavepoint(name);
    }
    public void ReleaseSavepoint(string name){
        ObjectDisposedException.ThrowIf(this.disposed,this);this.database.ValidateTransactionContext(this.state);this.state.ReleaseSavepoint(name);
    }

    public void Dispose() {
        if(this.disposed) {
            return;
        }
        this.disposed=true;
        this.database.EndTransaction(this.state,this.commitRequested);
    }
}

internal sealed class DatabaseTransactionState(long transactionMemoryLimit,IDisposable writeLease):IDisposable {
    internal byte[] CommitMetadata{get;set;}=[];
    internal global::Seamless.Execution.ResourceReservation? CommitMetadataMemory{get;set;}
    internal bool IsDisposed{get;private set;}
    internal GatewayTransactionOwner? GatewayOwner{get;init;}
    internal bool GatewayPrepared{get;set;}
    internal bool GatewayApplied{get;set;}
    internal global::Seamless.Transactions.TransactionScope? SharedScope{get;init;}
    public int Depth { get; set; }
    public bool Aborted { get; set; }
    public Dictionary<object,ITransactionOverlay> Overlays { get; }=
        new(global::System.Collections.Generic.ReferenceEqualityComparer.Instance);
    private readonly List<Savepoint> savepoints=[];

    internal void CreateSavepoint(string name){
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if(this.Aborted){throw new InvalidOperationException("The transaction is rollback-only.");}
        var snapshot=new Savepoint(name);
        try{
            foreach(var pair in this.Overlays){snapshot.Overlays.Add(pair.Key,pair.Value.CreateSnapshot());}
            this.SharedScope?.CreateSavepoint(name);
            this.savepoints.Add(snapshot);
        }catch{snapshot.Dispose();throw;}
    }

    internal void RollbackToSavepoint(string name){
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if(this.Aborted){throw new InvalidOperationException("The transaction is rollback-only.");}
        var index=this.savepoints.FindLastIndex(point=>string.Equals(point.Name,name,StringComparison.Ordinal));
        if(index<0){throw new KeyNotFoundException("Savepoint not found: "+name);}
        var restored=new Savepoint(name);
        try{
            foreach(var pair in this.savepoints[index].Overlays){restored.Overlays.Add(pair.Key,pair.Value.CreateSnapshot());}
            this.SharedScope?.RollbackToSavepoint(name);
        }catch{restored.Dispose();throw;}
        foreach(var overlay in this.Overlays.Values){overlay.Dispose();}
        this.Overlays.Clear();
        foreach(var pair in restored.Overlays){this.Overlays.Add(pair.Key,pair.Value);}
        restored.Overlays.Clear();
        for(var item=this.savepoints.Count-1;item>index;item--){this.savepoints[item].Dispose();this.savepoints.RemoveAt(item);}
    }
    internal void ReleaseSavepoint(string name){
        var index=this.savepoints.FindLastIndex(point=>string.Equals(point.Name,name,StringComparison.Ordinal));
        if(index<0){throw new KeyNotFoundException("Savepoint not found: "+name);}
        this.SharedScope?.ReleaseSavepoint(name);this.savepoints[index].Dispose();this.savepoints.RemoveAt(index);
    }

    public void Dispose() {
        List<Exception>? exceptions=null;
        if(this.IsDisposed){return;}this.IsDisposed=true;
        try {
            foreach(var overlay in this.Overlays.Values) {
                try {
                    overlay.Dispose();
                } catch(Exception exception) {
                    exceptions??=[];
                    exceptions.Add(exception);
                }
            }
            this.Overlays.Clear();
            foreach(var point in this.savepoints){try{point.Dispose();}catch(Exception exception){exceptions??=[];exceptions.Add(exception);}}
            this.savepoints.Clear();
            if(exceptions is null) {
                return;
            }
            if(exceptions.Count==1) {
                global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
            }
            throw new AggregateException(exceptions);
        } finally {
            try{this.CommitMetadata=[];this.CommitMetadataMemory?.Dispose();}finally{writeLease.Dispose();}
        }
    }

    public long TransactionMemoryLimit { get; }=transactionMemoryLimit;

    private sealed class Savepoint(string name):IDisposable{
        internal string Name{get;}=name;
        internal Dictionary<object,ITransactionOverlay> Overlays{get;}=new(global::System.Collections.Generic.ReferenceEqualityComparer.Instance);
        public void Dispose(){foreach(var overlay in this.Overlays.Values){overlay.Dispose();}this.Overlays.Clear();}
    }
}

internal interface ITransactionOverlay:IDisposable {
    ITransactionOverlay CreateSnapshot();
    IEnumerable<DatabaseWalOperation> CreateWalOperations();
    void Apply();
}

internal static class DatabaseWalOperationFactory {
    public static DatabaseWalOperation Create<TKey,TRecord>(
        global::Seamless.Execution.ExecutionScope resources,
        string tableName,
        bool deleted,
        TKey key,
        TRecord record) {
        var keyLength=resources.Codecs.Get<TKey>().GetEncodedLength(key);
        var recordLength=0;
        if(!deleted){recordLength=resources.Codecs.Get<TRecord>().GetEncodedLength(record);}
        var keyCharacters=Base64Length(keyLength);
        var recordCharacters=deleted?0:Base64Length(recordLength);
        var reservation=resources.Budget.ReserveMemory(checked(256L+2L*keyCharacters+2L*recordCharacters));
        try {
            using var encodedKey=resources.Rent(keyLength);
            resources.Codecs.Get<TKey>().Encode(key,encodedKey.Memory.Span);
            string? recordData=null;
            if(!deleted) {
                using var encodedRecord=resources.Rent(recordLength);
                resources.Codecs.Get<TRecord>().Encode(record,encodedRecord.Memory.Span);
                recordData=Convert.ToBase64String(encodedRecord.Memory.Span);
            }
            return new(2,tableName,deleted,Convert.ToBase64String(encodedKey.Memory.Span),recordData,reservation);
        } catch {
            reservation.Dispose();
            throw;
        }
    }

    private static long Base64Length(int byteLength)=>checked(4L*((byteLength+2L)/3L));
}

internal sealed class UnkeyedTableTransactionOverlay<TRecord>(
    Table<TRecord> table,
    global::Seamless.Execution.ExecutionScope resources,
    long memoryLimit):ITransactionOverlay
    where TRecord:notnull {
    private TransactionChangeSet<TRecord,TRecord> changes=new(resources,memoryLimit);
    private bool cleared;

    public ITransactionOverlay CreateSnapshot(){
        var snapshot=new UnkeyedTableTransactionOverlay<TRecord>(table,resources,memoryLimit);
        try{var changes=this.changes.CreateSnapshot();snapshot.changes.Dispose();snapshot.changes=changes;snapshot.cleared=this.cleared;return snapshot;}catch{snapshot.Dispose();throw;}
    }

    public long LongCount {
        get {
            var count=this.cleared?0:table.DirectLongCount;
            foreach(var pair in this.changes.Enumerate()) {
                var exists=!this.cleared&&table.DirectContains(pair.Key);
                if(pair.Value.Deleted&&exists){count--;}
                if(!pair.Value.Deleted&&!exists){count++;}
            }
            return count;
        }
    }

    public IEnumerable<TRecord> Rows=>this.EnumerateRows();

    public bool Contains(TRecord item) {
        if(this.changes.TryGetValue(item,out var change)){return !change.Deleted;}
        if(this.cleared){return false;}
        return table.DirectContains(item);
    }

    public bool Add(TRecord item) {
        if(this.Contains(item)){return false;}
        this.changes.Set(item,new(false,item));
        return true;
    }

    public bool Remove(TRecord item) {
        if(!this.Contains(item)){return false;}
        this.changes.Set(item,new(true,default!));
        return true;
    }

    public void Clear() {
        this.changes.Dispose();
        this.changes=new(resources,memoryLimit);
        this.cleared=true;
    }

    public void Apply() {
        if(this.cleared){table.DirectClear();}
        foreach(var pair in this.changes.Enumerate()) {
            if(pair.Value.Deleted){_ = table.DirectRemove(pair.Key);}
            else{_ = table.DirectAdd(pair.Value.Record);}
        }
    }

    public IEnumerable<DatabaseWalOperation> CreateWalOperations() {
        if(this.cleared) {
            foreach(var item in table.DirectRows) {
                yield return DatabaseWalOperationFactory.Create(resources,table.Name,true,item,default(TRecord)!);
            }
        }
        foreach(var pair in this.changes.Enumerate()) {
            yield return DatabaseWalOperationFactory.Create(
                resources,
                table.Name,
                pair.Value.Deleted,
                pair.Key,
                pair.Value.Record);
        }
    }

    private IEnumerable<TRecord> EnumerateRows() {
        if(!this.cleared) {
            foreach(var item in table.DirectRows) {
                if(!this.changes.TryGetValue(item,out var change)){yield return item;}
                else if(!change.Deleted){yield return change.Record;}
            }
        }
        foreach(var pair in this.changes.Enumerate()) {
            if(!pair.Value.Deleted&&(this.cleared||!table.DirectContains(pair.Key))){yield return pair.Value.Record;}
        }
    }

    public void Dispose()=>this.changes.Dispose();
}

internal sealed class TableTransactionOverlay<TKey,TRecord>(
    Table<TKey,TRecord> table,
    global::Seamless.Execution.ExecutionScope resources,
    long memoryLimit):ITransactionOverlay
    where TKey:struct
    where TRecord:struct {
    private TransactionChangeSet<TKey,TRecord> changes=new(resources,memoryLimit,table.GatewayKeyComparer);
    private int references=1;
    private sealed class HistorySnapshot(TableTransactionOverlay<TKey,TRecord> owner,TransactionChangeSet<TKey,TRecord>.HistoryPoint point):ITransactionOverlay{
        private bool disposed;
        public ITransactionOverlay CreateSnapshot(){ObjectDisposedException.ThrowIf(this.disposed,this);owner.changes.RollbackToHistoryPoint(point);owner.references++;return owner;}
        public void Apply()=>throw new InvalidOperationException("A savepoint cannot be committed directly.");
        public IEnumerable<DatabaseWalOperation> CreateWalOperations()=>throw new InvalidOperationException("A savepoint cannot be prepared directly.");
        public void Dispose(){if(this.disposed){return;}this.disposed=true;try{point.Dispose();}finally{owner.Dispose();}}
    }
    private TRecord OwnRecord(TRecord record){
        if(!resources.Codecs.UsesRecordGraphSerialization&&(table.GatewayKeyComparer is null||!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<TRecord>())){return record;}
        using var encoded=resources.Encode(record);return resources.Codecs.Get<TRecord>().Decode(encoded.Memory.Span);
    }
    private TKey OwnKey(TKey key){
        if(!resources.Codecs.UsesRecordGraphSerialization&&(table.GatewayKeyComparer is null||!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<TKey>())){return key;}
        using var encoded=resources.Encode(key);return resources.Codecs.Get<TKey>().Decode(encoded.Memory.Span);
    }

    public ITransactionOverlay CreateSnapshot(){
        if(table.GatewayKeyComparer is not null){var point=this.changes.CreateHistoryPoint();this.references++;return new HistorySnapshot(this,point);}
        var snapshot=new TableTransactionOverlay<TKey,TRecord>(table,resources,memoryLimit);
        try{var changes=this.changes.CreateSnapshot();snapshot.changes.Dispose();snapshot.changes=changes;return snapshot;}catch{snapshot.Dispose();throw;}
    }

    public long LongCount {
        get {
            var count=table.DirectLongCount;
            foreach(var pair in this.changes.Enumerate()) {
                var exists=table.DirectTryGetValue(pair.Key,out _);
                if(pair.Value.Deleted&&exists){count--;}
                if(!pair.Value.Deleted&&!exists){count++;}
            }
            return count;
        }
    }
    public int Count=>checked((int)this.LongCount);
    public IEnumerable<TKey> Keys {
        get {
            foreach(var pair in this.EnumeratePairs()){yield return pair.Key;}
        }
    }
    public IEnumerable<TRecord> Rows {
        get {
            foreach(var pair in this.EnumeratePairs()){yield return pair.Value;}
        }
    }

    public bool TryGetValue(TKey key,out TRecord record) {
        if(this.changes.TryGetValue(key,out var change)) {
            record=change.Deleted?default:this.OwnRecord(change.Record);
            return !change.Deleted;
        }
        return table.DirectTryGetValue(key,out record);
    }

    public bool Add(TKey key,TRecord record) {
        if(this.TryGetValue(key,out _)) {
            return false;
        }
        this.changes.Set(this.OwnKey(key),new(false,this.OwnRecord(record)));
        table.GatewayChanged?.Invoke(null,record);
        return true;
    }

    public void Set(TKey key,TRecord record){if(!table.KeysEqual(key,table.RecordKey(record))){throw new ArgumentException("Record key does not match its index key.",nameof(record));}var existed=this.TryGetValue(key,out var previous);this.changes.Set(this.OwnKey(key),new(false,this.OwnRecord(record)));table.GatewayChanged?.Invoke(existed?previous:null,record);}
    public bool Replace(TKey previousKey,TKey key,TRecord record){if(!this.TryGetValue(previousKey,out var previous)){return false;}if(table.KeysEqual(previousKey,key)){this.Set(key,record);return true;}if(this.TryGetValue(key,out _)){return false;}this.changes.Set(this.OwnKey(previousKey),new(true,default));this.changes.Set(this.OwnKey(key),new(false,this.OwnRecord(record)));table.GatewayChanged?.Invoke(previous,record);return true;}

    public bool Remove(TKey key) {
        if(!this.TryGetValue(key,out var previous)) {
            return false;
        }
        this.changes.Set(this.OwnKey(key),new(true,default));
        table.GatewayChanged?.Invoke(previous,null);
        return true;
    }

    public void Apply() {
        foreach(var pair in this.changes.Enumerate()) {
            if(pair.Value.Deleted) {
                _=table.DirectRemove(pair.Key);
            } else {
                table.DirectSet(pair.Key,pair.Value.Record);
            }
        }
    }

    public IEnumerable<DatabaseWalOperation> CreateWalOperations() {
        foreach(var pair in this.changes.Enumerate()) {
            yield return DatabaseWalOperationFactory.Create(
                resources,
                table.Name,
                pair.Value.Deleted,
                pair.Key,
                pair.Value.Record);
        }
    }

    private IEnumerable<KeyValuePair<TKey,TRecord>> EnumeratePairs() {
        foreach(var pair in table.DirectRows) {
            if(!this.changes.TryGetValue(pair.Key,out var change)) {
                yield return pair;
            } else if(!change.Deleted) {
                yield return new(pair.Key,this.OwnRecord(change.Record));
            }
        }
        foreach(var pair in this.changes.Enumerate()) {
            if(!pair.Value.Deleted&&!table.DirectTryGetValue(pair.Key,out _)) {
                yield return new(this.OwnKey(pair.Key),this.OwnRecord(pair.Value.Record));
            }
        }
    }

    public void Dispose(){if(--this.references==0){this.changes.Dispose();}}
}

internal sealed class MaterializedViewTransactionOverlay<TKey,TRecord>(
    MaterializedView<TKey,TRecord> view,
    global::Seamless.Execution.ExecutionScope resources,
    long memoryLimit):ITransactionOverlay
    where TKey:struct
    where TRecord:struct {
    private TransactionChangeSet<TKey,TRecord> additions=new(resources,memoryLimit);
    private TValue Own<TValue>(TValue value){
        if(!resources.Codecs.UsesRecordGraphSerialization){return value;}
        using var encoded=resources.Encode(value);return resources.Codecs.Get<TValue>().Decode(encoded.Memory.Span);
    }

    public ITransactionOverlay CreateSnapshot(){
        var snapshot=new MaterializedViewTransactionOverlay<TKey,TRecord>(view,resources,memoryLimit);
        try{var additions=this.additions.CreateSnapshot();snapshot.additions.Dispose();snapshot.additions=additions;return snapshot;}catch{snapshot.Dispose();throw;}
    }

    public long LongCount {
        get {
            var count=view.DirectLongCount;
            foreach(var pair in this.additions.Enumerate()) {
                if(!pair.Value.Deleted&&!view.DirectTryGetValue(pair.Key,out _)){count++;}
            }
            return count;
        }
    }
    public int Count=>checked((int)this.LongCount);
    public IEnumerable<TKey> Keys {
        get {
            foreach(var pair in this.EnumeratePairs()){yield return pair.Key;}
        }
    }
    public IEnumerable<TRecord> Rows {
        get {
            foreach(var pair in this.EnumeratePairs()){yield return pair.Value;}
        }
    }

    public bool TryGetValue(TKey key,out TRecord record) {
        if(this.additions.TryGetValue(key,out var change)) {
            record=this.Own(change.Record);
            return !change.Deleted;
        }
        return view.DirectTryGetValue(key,out record);
    }

    public bool Add(TKey key,TRecord record) {
        if(this.TryGetValue(key,out _)) {
            return false;
        }
        this.additions.Set(key,new(false,this.Own(record)));
        return true;
    }

    public void Apply() {
        foreach(var pair in this.additions.Enumerate()) {
            if(!pair.Value.Deleted) {
                view.DirectSet(pair.Key,pair.Value.Record);
            }
        }
    }

    public IEnumerable<DatabaseWalOperation> CreateWalOperations() {
        foreach(var pair in this.additions.Enumerate()) {
            yield return DatabaseWalOperationFactory.Create(
                resources,
                view.Name,
                pair.Value.Deleted,
                pair.Key,
                pair.Value.Record);
        }
    }

    private IEnumerable<KeyValuePair<TKey,TRecord>> EnumeratePairs() {
        foreach(var pair in view.DirectRows){yield return pair;}
        foreach(var pair in this.additions.Enumerate()) {
            if(!pair.Value.Deleted&&!view.DirectTryGetValue(pair.Key,out _)) {
                yield return new(this.Own(pair.Key),this.Own(pair.Value.Record));
            }
        }
    }

    public void Dispose()=>this.additions.Dispose();
}
