using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MemoryPack;
using Microsoft.AspNetCore.Identity;
using Seamless.Database;

namespace Seamless.Management.Server;

[MemoryPackable]public partial record struct ManagementKey(Guid Id);
[MemoryPackable]public partial record struct ManagementRecord(Guid Id,string Kind,string Subject,string Json,int ChunkCount=0,int JsonLength=0,string PayloadHash="");
internal sealed class ManagementDatabase(DatabaseContainerOptions options):DatabaseContainer(options){
    internal Table<ManagementKey,ManagementRecord> Entries=>this.Table<ManagementKey,ManagementRecord>(row=>new(row.Id));
    protected override void EnsureTransactionSchema(){_=this.Entries;}
    protected override string TransactionCatalogIdentity=>"SeamlessManagementV1|"+this.TransactionCodecIdentity<ManagementKey,ManagementRecord>("Entries");
}
internal sealed record StoreHeader(int Format,Guid InstanceId,long HistorySequence,long Revision);
internal sealed record CatalogCounter(long Sequence);
internal sealed record CatalogItem(string Subject);

/// <summary>管理データはSeamlessの取引テーブルに保存。ファイルは明示初期化の印だけを持つ。</summary>
public sealed class ManagementStore:IDisposable{
    private const int ChunkCharacters=64*1024;
    private const int MaximumJsonCharacters=64*1024*1024;
    private readonly ManagementDatabase database;
    private readonly FileStream owner;
    private readonly object gate=new();
    private StoreHeader header;
    internal object Gate=>this.gate;
    public string Root{get;}
    public Guid InstanceId=>this.header.InstanceId;
    public long Revision=>this.header.Revision;
    public static ManagementStore Initialize(string root,string admin,string password){
        ValidateAccountName(admin);if(password.Length<12){throw new ManagementException("Validation","初期パスワードは12文字以上にしてください。");}
        root=Path.GetFullPath(root);if(Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any()){throw new ManagementException("Conflict","初期化先は空の専用ディレクトリにしてください。");}
        Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"management.format"),"3",Encoding.UTF8);var store=new ManagementStore(root,true);
        try{var user=new UserAccount(admin,"",[new("*",ManagementPermission.All)]);user=user with{PasswordHash=new PasswordHasher<UserAccount>().HashPassword(user,password)};store.Write(()=>store.Put("user",Normalize(admin),user));return store;}
        catch{store.Dispose();throw;}
    }
    public ManagementStore(string root):this(root,false){}
    internal ManagementStore(string root,FileStream exclusiveOwner):this(root,false,exclusiveOwner){}
    private ManagementStore(string root,bool initializing,FileStream? exclusiveOwner=null){
        this.Root=Path.GetFullPath(root);
        if(!File.Exists(Path.Combine(this.Root,"management.format"))||File.ReadAllText(Path.Combine(this.Root,"management.format")).Trim()!="3"){throw new ManagementException("InitializationRequired","対応する管理領域をCLIで明示初期化・復旧してください。自動作成・移行は行いません。");}
        this.owner=exclusiveOwner??new FileStream(Path.Combine(this.Root,"management.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        try{
            var storage=Path.Combine(this.Root,"system");if(!initializing&&(!Directory.Exists(storage)||!Directory.EnumerateFileSystemEntries(storage).Any())){throw new ManagementException("RecoveryRequired","システムDBがありません。明示的にバックアップから復旧してください。");}
            var options=DatabaseContainerOptions.OnStorage(Path.Combine(this.Root,"system")) with{Transactions=DatabaseTransactionMode.SharedV2,TransactionMemoryLimit=256L<<20,IndexSize=1024,PageSize=1<<20,SegmentSize=1<<24,MemorySize=1<<24,TemporaryDirectory=Path.Combine(this.Root,"temporary"),MaximumQueryWorkers=Math.Min(8,Environment.ProcessorCount)};
            this.database=new(options);_=this.database.Entries;var saved=this.Read<StoreHeader>("header","instance");
            if(saved is null&&!initializing){throw new ManagementException("RecoveryRequired","管理ヘッダーがありません。バックアップから復旧してください。");}
            if(saved is not null&&saved.Format!=3){throw new ManagementException("MigrationRequired","管理DBの形式をCLIで明示的に更新してください。");}
            this.header=saved??new(3,Guid.NewGuid(),0,0);if(saved is null){this.Write(()=>{});}
        }catch{this.owner.Dispose();throw;}
    }
    public T? Read<T>(string kind,string subject){
        lock(this.gate){if(!this.database.Entries.TryGetValue(new(Key(kind,subject)),out var row)){return default;}if(row.Kind!=kind||row.Subject!=subject){throw new InvalidDataException("管理キーが衝突しています。");}var json=row.Json;
            if(row.ChunkCount>0){if(row.JsonLength<1||row.JsonLength>MaximumJsonCharacters||row.ChunkCount!=(row.JsonLength+ChunkCharacters-1)/ChunkCharacters){throw new InvalidDataException("管理データの長さが不正です。");}var builder=new StringBuilder(row.JsonLength);
                for(var index=0;index<row.ChunkCount;index++){var part=ChunkSubject(kind,subject,index);if(!this.database.Entries.TryGetValue(new(Key("chunk",part)),out var chunk)||chunk.Kind!="chunk"||chunk.Subject!=part){throw new InvalidDataException("管理データのチャンクがありません。");}builder.Append(chunk.Json);}
                json=builder.ToString();if(json.Length!=row.JsonLength||PayloadHash(json)!=row.PayloadHash){throw new InvalidDataException("管理データのチャンクが一致しません。");}
            }
            return JsonSerializer.Deserialize<T>(json,ManagementJson.Options);
        }
    }
    internal void Put<T>(string kind,string subject,T value){
        var key=new ManagementKey(Key(kind,subject));var exists=this.database.Entries.TryGetValue(key,out var previous);if(exists&&(previous.Kind!=kind||previous.Subject!=subject)){throw new InvalidDataException("管理キーが衝突しています。");}
        if(!exists&&kind is not "header" and not "history" and not "catalog" and not "catalogHeader"){
            var counter=this.Read<CatalogCounter>("catalogHeader",kind)??new(0);counter=counter with{Sequence=checked(counter.Sequence+1)};this.Put("catalogHeader",kind,counter);this.Put("catalog",kind+":"+counter.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),new CatalogItem(subject));
        }
        var json=JsonSerializer.Serialize(value,ManagementJson.Options);if(json.Length>MaximumJsonCharacters){throw new ManagementException("Limit","管理データの容量上限を超えています。");}var count=0;var hash="";var inline=json;
        if(json.Length>ChunkCharacters){count=(json.Length+ChunkCharacters-1)/ChunkCharacters;hash=PayloadHash(json);inline="";
            for(var index=0;index<count;index++){var part=ChunkSubject(kind,subject,index);var chunkKey=new ManagementKey(Key("chunk",part));if(this.database.Entries.TryGetValue(chunkKey,out var existing)&&(existing.Kind!="chunk"||existing.Subject!=part)){throw new InvalidDataException("管理キーが衝突しています。");}this.database.Entries[chunkKey]=new(chunkKey.Id,"chunk",part,json.Substring(index*ChunkCharacters,Math.Min(ChunkCharacters,json.Length-index*ChunkCharacters)));}
        }
        for(var index=count;index<previous.ChunkCount;index++){_=this.database.Entries.Remove(new(Key("chunk",ChunkSubject(kind,subject,index))));}
        this.database.Entries[key]=new(key.Id,kind,subject,inline,count,json.Length,hash);
    }
    private static string ChunkSubject(string kind,string subject,int index)=>kind+"\0"+subject+"\0"+index.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static string PayloadHash(string json)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    internal void Write(Action action){
        lock(this.gate){var previous=this.header;using var transaction=this.database.BeginTransaction();
            try{action();this.header=this.header with{Revision=checked(this.header.Revision+1)};this.Put("header","instance",this.header);transaction.Commit();}
            catch{this.header=previous;throw;}
        }
    }
    internal void AddHistory(string user,string db,string action,string id,long version){
        this.header=this.header with{HistorySequence=checked(this.header.HistorySequence+1)};
        this.Put("history",this.header.HistorySequence.ToString(System.Globalization.CultureInfo.InvariantCulture),new HistoryEntry(this.header.HistorySequence,user,db,action,id,version,DateTimeOffset.UtcNow));
    }
    public Page<T> List<T>(string kind,long cursor=0,int limit=100,Func<T,bool>? predicate=null)=>this.SelectPage(kind,cursor,limit,predicate,static item=>item);
    internal Page<TResult> SelectPage<T,TResult>(string kind,long cursor,int limit,Func<T,bool>? predicate,Func<T,TResult> projection){
        if(cursor<0||limit is <1 or >200){throw new ManagementException("InvalidRequest","ページ指定が不正です。");}
        lock(this.gate){var items=new List<TResult>();var sequence=cursor;var last=this.Read<CatalogCounter>("catalogHeader",kind)?.Sequence??0;using var read=this.database.BeginReadSession();
            while(sequence<last&&items.Count<limit){sequence++;var entry=this.Read<CatalogItem>("catalog",kind+":"+sequence.ToString(System.Globalization.CultureInfo.InvariantCulture));if(entry is null){continue;}var item=this.Read<T>(kind,entry.Subject);if(item is not null&&(predicate is null||predicate(item))){items.Add(projection(item));}}
            return new(items.ToArray(),sequence,sequence<last);
        }
    }
    public Page<HistoryEntry> History(long cursor,int limit,Func<HistoryEntry,bool> predicate){
        if(cursor<0||limit is <1 or >200){throw new ManagementException("InvalidRequest","ページ指定が不正です。");}
        lock(this.gate){var items=new List<HistoryEntry>();var sequence=cursor;
            while(sequence<this.header.HistorySequence&&items.Count<limit){sequence++;var entry=this.Read<HistoryEntry>("history",sequence.ToString(System.Globalization.CultureInfo.InvariantCulture));if(entry is not null&&predicate(entry)){items.Add(entry);}}
            return new(items.ToArray(),sequence,sequence<this.header.HistorySequence);
        }
    }
    public void Dispose(){try{this.database.Dispose();}finally{this.owner.Dispose();}}
    internal static string Normalize(string name)=>name.Normalize(NormalizationForm.FormC).ToUpperInvariant();
    internal static void ValidateAccountName(string name){if(name.Length is <1 or >64||name.Any(character=>!char.IsLetterOrDigit(character)&&character is not '_' and not '-' and not '.')){throw new ManagementException("Validation","利用者名が不正です。");}}
    internal static Guid Key(string kind,string subject)=>new(SHA256.HashData(Encoding.UTF8.GetBytes(kind+"\0"+subject)).AsSpan(0,16));
}
