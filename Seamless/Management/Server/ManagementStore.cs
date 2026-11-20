using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MemoryPack;
using Microsoft.AspNetCore.Identity;
using Seamless.Database;

namespace Seamless.Management.Server;

[MemoryPackable]public partial record struct ManagementKey(Guid Id);
[MemoryPackable]public partial record struct ManagementRecord(Guid Id,string Kind,string Subject,string Json,int ChunkCount=0,int JsonLength=0,string PayloadHash="");
internal class ManagementDatabase(DatabaseContainerOptions options):DatabaseContainer(options){
    internal Table<ManagementKey,ManagementRecord> Entries=>this.Table<ManagementKey,ManagementRecord>(row=>new(row.Id));
    protected override void EnsureTransactionSchema(){_=this.Entries;}
    protected override string TransactionCatalogIdentity=>"SeamlessManagementV1|"+this.TransactionCodecIdentity<ManagementKey,ManagementRecord>("Entries");
}
internal sealed record StoreHeader(int Format,Guid InstanceId,long HistorySequence,long Revision,long? CatalogRevision=null);
internal sealed record CatalogCounter(long Sequence);
internal sealed record CatalogItem(string Subject);

/// <summary>管理データはSeamlessの取引テーブルに保存。ファイルは明示初期化の印だけを持つ。</summary>
public sealed partial class ManagementStore:IDisposable{
    private const int ChunkCharacters=64*1024;
    private const int MaximumJsonCharacters=64*1024*1024;
    private readonly ManagementDatabase database;
    private readonly FileStream owner;
    private readonly object gate=new();
    private long authorizationEpoch;
    private bool authorizationPending;
    private int writeDepth;
    private StoreHeader header;
    internal object Gate=>this.gate;
    internal global::Seamless.Execution.ResourceBudget ResourceBudget=>this.database.ExecutionResources.Budget;
    public string Root{get;}
    public Guid InstanceId=>this.header.InstanceId;
    public long Revision=>this.header.Revision;
    internal long CatalogRevision=>this.header.CatalogRevision??this.header.Revision;
    internal long AuthorizationEpoch=>Volatile.Read(ref this.authorizationEpoch);
    internal bool WriteInProgress=>this.writeDepth>0;
    public int Format{get;private set;}
    public bool HasAudit=>this.Format>=4;
    internal Type SystemCatalogType=>this.database.GetType();
    private bool leaveOwnerOpen;
    public static ManagementStore Initialize(string root,string admin,string password){
        return InitializeCore(root,admin,password,6);
    }
    internal static ManagementStore InitializeLegacy(string root,string admin,string password)=>InitializeCore(root,admin,password,3);
    internal static ManagementStore InitializeAuditLegacy(string root,string admin,string password)=>InitializeCore(root,admin,password,4);
    internal static ManagementStore InitializeReadAuditLegacy(string root,string admin,string password)=>InitializeCore(root,admin,password,5);
    private static ManagementStore InitializeCore(string root,string admin,string password,int format){
        ValidateAccountName(admin);if(password.Length<12){throw new ManagementException("Validation","初期パスワードは12文字以上にしてください。");}
        root=Path.GetFullPath(root);if(Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any()){throw new ManagementException("Conflict","初期化先は空の専用ディレクトリにしてください。");}
        var buildWorkRoot=ResolveBuildWorkRoot(root);
        Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"management.format"),format.ToString(System.Globalization.CultureInfo.InvariantCulture),Encoding.UTF8);var store=new ManagementStore(root,true,buildWorkRoot:buildWorkRoot);
        try{var user=new UserAccount(admin,"",[new("*",ManagementPermission.All)]);user=user with{PasswordHash=new PasswordHasher<UserAccount>().HashPassword(user,password)};store.Write(()=>store.Put("user",Normalize(admin),user));return store;}
        catch{store.Dispose();throw;}
    }
    public ManagementStore(string root):this(root,false){}
    internal ManagementStore(string root,FileStream exclusiveOwner):this(root,false,exclusiveOwner){}
    internal ManagementStore(string root,FileStream exclusiveOwner,bool leaveOwnerOpen):this(root,false,exclusiveOwner){this.leaveOwnerOpen=leaveOwnerOpen;}
    internal static ManagementStore OpenUpgradeRecovery(string root,FileStream owner){var store=new ManagementStore(root,false,owner,true);store.leaveOwnerOpen=true;return store;}
    private ManagementStore(string root,bool initializing,FileStream? exclusiveOwner=null,bool skipUpgradeFence=false,string? buildWorkRoot=null){
        this.Root=Path.GetFullPath(root);
        this.BuildWorkRoot=buildWorkRoot??ResolveBuildWorkRoot(this.Root);
        if(!skipUpgradeFence&&File.Exists(Path.Combine(this.Root,"management.audit-upgrade.json"))){throw new ManagementException("RecoveryRequired","監査テーブルの更新が中断しています。CLIのrollback-audit-upgradeで明示復旧してください。");}
        if(!skipUpgradeFence&&File.Exists(Path.Combine(this.Root,"management.prepared-wal-upgrade.json"))){throw new ManagementException("RecoveryRequired","分割WALへの更新が中断しています。CLIのrollback-prepared-wal-upgradeで明示復旧してください。");}
        if(!skipUpgradeFence&&File.Exists(Path.Combine(this.Root,"management.typed-upgrade.json"))){throw new ManagementException("RecoveryRequired","型付き管理領域への更新が中断しています。CLIのrollback-managed-types-upgradeで明示復旧してください。");}
        var marker=Path.Combine(this.Root,"management.format");var format="";if(File.Exists(marker)){format=File.ReadAllText(marker).Trim();}
        if(format is not "3" and not "4" and not "5" and not "6" and not "7"){throw new ManagementException("InitializationRequired","対応する管理領域をCLIで明示初期化・復旧してください。自動作成・移行は行いません。");}this.Format=int.Parse(format,System.Globalization.CultureInfo.InvariantCulture);
        this.owner=exclusiveOwner??new FileStream(Path.Combine(this.Root,"management.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        ManagementDatabase? opened=null;
        try{
            var storage=Path.Combine(this.Root,"system");if(!initializing&&(!Directory.Exists(storage)||!Directory.EnumerateFileSystemEntries(storage).Any())){throw new ManagementException("RecoveryRequired","システムDBがありません。明示的にバックアップから復旧してください。");}
            var options=DatabaseContainerOptions.OnStorage(Path.Combine(this.Root,"system")) with{PreparedTransactionWal=this.Format>=6,TransactionMemoryLimit=256L<<20,IndexSize=1024,PageSize=1<<20,SegmentSize=1<<24,MemorySize=1<<24,TemporaryDirectory=Path.Combine(this.Root,"temporary"),MaximumQueryWorkers=Math.Min(8,Environment.ProcessorCount)};
            var auditSizes=new DatabaseTableStorageOptions{IndexSize=1024,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};options=options with{TableStorage=ManagementAuditDatabase.TableNames.ToDictionary(static name=>name,_=>auditSizes,StringComparer.Ordinal)};
            if(this.Format>=7){var sizes=new Dictionary<string,DatabaseTableStorageOptions>(options.TableStorage,StringComparer.Ordinal);foreach(var name in new[]{"Users","UserGrants","ManagedTextParts"}){sizes.Add(name,new(){IndexSize=1024,PageSize=1<<17,SegmentSize=1<<20,MemorySize=1<<20});}options=options with{TableStorage=sizes};}
            if(this.Format>=7){opened=new ManagementTypedDatabase(options);}else if(this.HasAudit){opened=new ManagementAuditDatabase(options);}else{opened=new(options);}this.database=opened;_=this.database.Entries;var saved=this.Read<StoreHeader>("header","instance");
            if(saved is null&&!initializing){throw new ManagementException("RecoveryRequired","管理ヘッダーがありません。バックアップから復旧してください。");}
            if(saved is not null&&saved.Format!=this.Format){throw new ManagementException("MigrationRequired","管理DBの形式をCLIで明示的に更新してください。");}
            this.header=saved??new(this.Format,Guid.NewGuid(),0,0);if(saved is null){this.Write(()=>{});}
            this.RecoverSourceBuilds();
        }catch(Exception error){try{opened?.Dispose();}catch(Exception cleanup){throw new AggregateException("管理DBの初期化と資源解放に失敗しました。",error,cleanup);}finally{this.owner.Dispose();}throw;}
    }
    internal string CurrentDatabase(Definition definition)=>this.Read<DefinitionVersion>("active",definition.Id)?.Database??definition.Database;
    internal DefinitionVersion DatabaseDefinition(string name)=>this.FindDatabase(name)??throw new ManagementException("NotFound","先にDB定義を適用してください: "+name);
    internal DefinitionVersion? FindDatabase(string name){
        lock(this.gate){
            var cursor=0L;
            do{
                var page=this.List<DefinitionVersion>("active",cursor,predicate:version=>version.Definition.Kind==DefinitionKind.Database&&version.Database==name&&!this.IsDisplacedPlacement(version));
                if(page.Items.Length>0){return page.Items[0];}
                if(!page.HasMore){break;}cursor=page.NextCursor;
            }while(true);
            return null;
        }
    }
    public T? Read<T>(string kind,string subject){
        lock(this.gate){if(this.TypedKind(kind)){return this.ReadTyped<T>(kind,subject);}if(!this.database.Entries.TryGetValue(new(Key(kind,subject)),out var row)){return default;}if(row.Kind!=kind||row.Subject!=subject){throw new InvalidDataException("管理キーが衝突しています。");}var json=row.Json;
            if(row.ChunkCount>0){if(row.JsonLength<1||row.JsonLength>MaximumJsonCharacters||row.ChunkCount!=(row.JsonLength+ChunkCharacters-1)/ChunkCharacters){throw new InvalidDataException("管理データの長さが不正です。");}var builder=new StringBuilder(row.JsonLength);
                for(var index=0;index<row.ChunkCount;index++){var part=ChunkSubject(kind,subject,index);if(!this.database.Entries.TryGetValue(new(Key("chunk",part)),out var chunk)||chunk.Kind!="chunk"||chunk.Subject!=part){throw new InvalidDataException("管理データのチャンクがありません。");}builder.Append(chunk.Json);}
                json=builder.ToString();if(json.Length!=row.JsonLength||PayloadHash(json)!=row.PayloadHash){throw new InvalidDataException("管理データのチャンクが一致しません。");}
            }
            return JsonSerializer.Deserialize<T>(json,ManagementJson.Options);
        }
    }
    internal void Put<T>(string kind,string subject,T value){
        if(this.TypedKind(kind)){this.PutTyped(kind,subject,value);return;}
        if(kind=="authorization"&&subject=="active"){this.header=this.header with{CatalogRevision=checked(this.CatalogRevision+1)};this.authorizationPending=true;}
        if(kind is "active" or "user"){this.header=this.header with{CatalogRevision=checked(this.CatalogRevision+1)};}
        var key=new ManagementKey(Key(kind,subject));var exists=this.database.Entries.TryGetValue(key,out var previous);if(exists&&(previous.Kind!=kind||previous.Subject!=subject)){throw new InvalidDataException("管理キーが衝突しています。");}
        this.RegisterCatalogSubject(kind,subject,exists);
        var json=JsonSerializer.Serialize(value,ManagementJson.Options);if(json.Length>MaximumJsonCharacters){throw new ManagementException("Limit","管理データの容量上限を超えています。");}var count=0;var hash="";var inline=json;
        if(json.Length>ChunkCharacters){count=(json.Length+ChunkCharacters-1)/ChunkCharacters;hash=PayloadHash(json);inline="";
            for(var index=0;index<count;index++){var part=ChunkSubject(kind,subject,index);var chunkKey=new ManagementKey(Key("chunk",part));if(this.database.Entries.TryGetValue(chunkKey,out var existing)&&(existing.Kind!="chunk"||existing.Subject!=part)){throw new InvalidDataException("管理キーが衝突しています。");}this.database.Entries[chunkKey]=new(chunkKey.Id,"chunk",part,json.Substring(index*ChunkCharacters,Math.Min(ChunkCharacters,json.Length-index*ChunkCharacters)));}
        }
        for(var index=count;index<previous.ChunkCount;index++){_=this.database.Entries.Remove(new ManagementKey(Key("chunk",ChunkSubject(kind,subject,index))));}
        this.database.Entries[key]=new(key.Id,kind,subject,inline,count,json.Length,hash);
    }
    private static string ChunkSubject(string kind,string subject,int index)=>kind+"\0"+subject+"\0"+index.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static string PayloadHash(string json)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    internal void Write(Action action){
        lock(this.gate){var previous=this.header;var previousAuthorization=this.authorizationEpoch;var previousPending=this.authorizationPending;var outer=this.writeDepth==0;var commitStarted=false;var nextAuthorization=previousAuthorization;using var transaction=this.database.BeginTransaction();this.writeDepth++;if(outer){this.authorizationPending=false;}
            try{
                this.header=this.header with{CatalogRevision=this.CatalogRevision};action();if(transaction.IsRollbackOnly){throw new ManagementException("TransactionAborted","管理DBの取引は取消状態のため確定できません。");}this.header=this.header with{Revision=checked(this.header.Revision+1)};this.Put("header","instance",this.header);if(outer&&this.authorizationPending){nextAuthorization=checked(previousAuthorization+1);}
                // Commitは確定要求だけを記録する。WALの確定・反映を行うDisposeの完了を待つ。
                transaction.Commit();commitStarted=true;transaction.Dispose();if(outer){Volatile.Write(ref this.authorizationEpoch,nextAuthorization);this.authorizationPending=previousPending;}
            }
            catch{var invalidate=commitStarted&&this.authorizationPending;this.header=previous;this.authorizationPending=previousPending;if(invalidate){Volatile.Write(ref this.authorizationEpoch,nextAuthorization);}else{Volatile.Write(ref this.authorizationEpoch,previousAuthorization);}throw;}
            finally{this.writeDepth--;}
        }
    }
    internal void AddHistory(string user,string db,string action,string id,long version){
        this.header=this.header with{HistorySequence=checked(this.header.HistorySequence+1)};
        var entry=new HistoryEntry(this.header.HistorySequence,user,db,action,id,version,DateTimeOffset.UtcNow){DatabaseDefinitionId=this.FindDatabase(db)?.Definition.Id};this.Put("history",this.header.HistorySequence.ToString(System.Globalization.CultureInfo.InvariantCulture),entry);if(this.HasAudit){this.AppendLegacyHistoryAudit(entry);}
    }
    public Page<T> List<T>(string kind,long cursor=0,int limit=100,Func<T,bool>? predicate=null)=>this.SelectPage(kind,cursor,limit,predicate,static item=>item);
    internal Page<TResult> SelectPage<T,TResult>(string kind,long cursor,int limit,Func<T,bool>? predicate,Func<T,TResult> projection,int maximumScanned=int.MaxValue){
        if(cursor<0||limit is <1 or >200){throw new ManagementException("InvalidRequest","ページ指定が不正です。");}
        lock(this.gate){var items=new List<TResult>();var sequence=cursor;var last=this.Read<CatalogCounter>("catalogHeader",kind)?.Sequence??0;using var read=this.database.BeginReadSession();
            while(sequence<last&&items.Count<limit&&sequence-cursor<maximumScanned){sequence++;var entry=this.Read<CatalogItem>("catalog",kind+":"+sequence.ToString(System.Globalization.CultureInfo.InvariantCulture));if(entry is null){continue;}var item=this.Read<T>(kind,entry.Subject);if(item is not null&&(predicate is null||predicate(item))){items.Add(projection(item));}}
            return new(items.ToArray(),sequence,sequence<last);
        }
    }
    public Page<HistoryEntry> History(long cursor,int limit,Func<HistoryEntry,bool> predicate)=>this.History(cursor,limit,predicate,CancellationToken.None);
    internal Page<HistoryEntry> History(long cursor,int limit,Func<HistoryEntry,bool> predicate,CancellationToken cancellationToken){
        if(cursor<0||limit is <1 or >200){throw new ManagementException("InvalidRequest","ページ指定が不正です。");}
        cancellationToken.ThrowIfCancellationRequested();
        lock(this.gate){var items=new List<HistoryEntry>();var sequence=cursor;
            while(sequence<this.header.HistorySequence&&items.Count<limit){cancellationToken.ThrowIfCancellationRequested();sequence++;var entry=this.Read<HistoryEntry>("history",sequence.ToString(System.Globalization.CultureInfo.InvariantCulture));if(entry is not null&&predicate(entry)){items.Add(entry);}}
            cancellationToken.ThrowIfCancellationRequested();
            return new(items.ToArray(),sequence,sequence<this.header.HistorySequence);
        }
    }
    public void Dispose(){try{this.database.Dispose();}finally{try{this.SourceBuilds.Stop();}finally{if(!this.leaveOwnerOpen){this.owner.Dispose();}}}}
    internal static string Normalize(string name)=>name.Normalize(NormalizationForm.FormC).ToUpperInvariant();
    internal static void ValidateAccountName(string name){if(name.Length is <1 or >64||name.Any(character=>!char.IsLetterOrDigit(character)&&character is not '_' and not '-' and not '.')){throw new ManagementException("Validation","利用者名が不正です。");}}
    internal static Guid Key(string kind,string subject)=>new(SHA256.HashData(Encoding.UTF8.GetBytes(kind+"\0"+subject)).AsSpan(0,16));
}
