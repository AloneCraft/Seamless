using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Seamless.Database;
using Seamless.Execution.Codecs;

namespace Seamless.Management.Server;

internal sealed partial class NativeRuntime{
    private const int MaximumRowResponseBytes=4*1024*1024;
    private const int MaximumRowBatch=1000;

    internal async Task<TableRows> ReadRowsAsync(TableRowsRequest request,string admittedId,CancellationToken cancellation,Action? authorize=null){
        if(request.Limit is <1 or >MaximumRowBatch||string.IsNullOrWhiteSpace(request.Table)||
            request.Cursor is{} cursor&&(request.Key is not null||request.ExpectedVersion is null||cursor.Offset<1||cursor.PrefixHash is not{Length:64}||!cursor.PrefixHash.All(Uri.IsHexDigit))){
            throw new ManagementException("InvalidRequest","表と1〜1000行の表示上限を指定してください。");
        }
        var gate=this.Gate("db:"+admittedId);
        await gate.WaitAsync(cancellation).ConfigureAwait(false);
        try{
            authorize?.Invoke();
            var version=this.RowEditingVersion(request.Database,request.ExpectedVersion,admittedId);
            var database=this.Open(version).Database;
            var declaration=FindRowTable(DatabaseTableDeclaration.Read(database.GetType()),request.Table);
            var read=RowHandler<Func<object,TableRowsRequest,long,CancellationToken,TableRows>>(declaration,nameof(ReadTypedRows));
            using var snapshot=database.BeginReadSession(cancellation);
            var result=read(declaration.GetTable(database),request,version.Version,cancellation);authorize?.Invoke();return result;
        }finally{gate.Release();}
    }

    internal async Task<TableRowCommit> CommitRowsAsync(TableRowBatch request,string admittedId,UserAccount user,CancellationToken cancellation){
        if(request.ExpectedVersion<=0||request.OperationId==Guid.Empty||request.Changes is null||request.Changes.Length is <1 or >MaximumRowBatch||
            request.Changes.Any(change=>change is null||string.IsNullOrWhiteSpace(change.Table))){
            throw new ManagementException("InvalidRequest","稼働版、操作ID、1〜1000行の変更を指定してください。");
        }
        var started=DateTimeOffset.UtcNow;
        var gate=this.Gate("db:"+admittedId);
        await gate.WaitAsync(cancellation).ConfigureAwait(false);
        try{
            var version=this.RowEditingVersion(request.Database,request.ExpectedVersion,admittedId);
            var database=this.Open(version).Database;
            var declarations=DatabaseTableDeclaration.Read(database.GetType());
            using var transaction=database.BeginTransaction(cancellation);
            foreach(var group in request.Changes.GroupBy(change=>change.Table,StringComparer.Ordinal)){
                var declaration=FindRowTable(declarations,group.Key);
                var change=RowHandler<Action<object,IEnumerable<TableRowChange>,CancellationToken>>(declaration,nameof(ApplyTypedRows));
                change(declaration.GetTable(database),group,cancellation);
            }
            cancellation.ThrowIfCancellationRequested();
            if(database.MetadataJournal is{} journal){
                var audit=new AuditEvent(Guid.NewGuid(),request.OperationId,started,DateTimeOffset.UtcNow,this.store.InstanceId,
                    ManagementStore.Normalize(user.Name),request.Database,version.Definition.Id,AuditAction.DataChange,AuditOutcome.Succeeded,
                    null,version.ArtifactHash,transaction.MetadataTransactionId,"Committed");
                transaction.CommitWithMetadata(AuditWalPayload.Encode(this.store.InstanceId,journal.Device.WalId,audit));
                transaction.Dispose();
                this.NotifyAudit(journal.Device.WalId);
            }else{transaction.Commit();}
            return new(version.Version,request.OperationId,request.Changes.Length);
        }finally{gate.Release();}
    }

    private DefinitionVersion RowEditingVersion(string database,long? expected,string admittedId){
        var version=this.DatabaseDefinition(database);if(version.Definition.Id!=admittedId){throw new ManagementException("Conflict","受付後にDB名の接続先が変わりました。再度開いてください。");}
        if(expected.HasValue&&version.Version!=expected.Value){throw new ManagementException("SchemaMismatch","稼働版が変わっています。行の変更を保持して読み直してください。");}
        return version;
    }

    private static DatabaseTableDeclaration FindRowTable(DatabaseTableDeclaration[] declarations,string path){
        var declaration=declarations.SingleOrDefault(item=>item.Path==path)??throw new ManagementException("NotFound","表がありません: "+path);
        if(declaration.Property.PropertyType.GetGenericTypeDefinition()!=typeof(Table<,>)){throw new ManagementException("Unsupported","主キー付きの表を指定してください。");}
        return declaration;
    }

    private static TDelegate RowHandler<TDelegate>(DatabaseTableDeclaration declaration,string method) where TDelegate:Delegate=>
        typeof(NativeRuntime).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static)!
            .MakeGenericMethod(declaration.Property.PropertyType.GenericTypeArguments).CreateDelegate<TDelegate>();

    private static TableRows ReadTypedRows<TKey,TRecord>(object source,TableRowsRequest request,long version,CancellationToken cancellation)
        where TKey:struct where TRecord:struct{
        var table=(Table<TKey,TRecord>)source;
        var rows=new List<TableRowSnapshot>();
        var columns=RuntimeValueJson.Columns(typeof(TRecord));
        var keyMembers=RuntimeValueJson.Columns(typeof(TKey));
        var bytes=0;
        if(request.Key is{} key){
            cancellation.ThrowIfCancellationRequested();
            if(table.TryGetValue((TKey)Materialize(key,typeof(TKey)),out var record)){Add(record);}
            return new(version,rows.ToArray(),false,columns){KeyMembers=keyMembers};
        }
        // ページ間にDBロックや列挙子を残さず、既読部分を照合して更新による重複・欠落を検出する。
        // 列挙元の再開カーソルがないため既読部分を再走査する。メモリ使用量は1ページに限定する。
        using var prefix=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        prefix.AppendData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{request.Database,request.Table,Version=version})));
        long offset=0;
        foreach(var record in table){
            cancellation.ThrowIfCancellationRequested();
            if(request.Cursor is{} continuation&&offset<continuation.Offset){
                prefix.AppendData(Encoding.ASCII.GetBytes(RowStamp(record)));offset++;
                if(offset==continuation.Offset&&Convert.ToHexString(prefix.GetCurrentHash())!=continuation.PrefixHash){
                    throw new ManagementException("RowConflict","読み込み済みの行が変更されています。編集内容を確認して再読込してください。");
                }
                continue;
            }
            if(rows.Count==request.Limit||!Add(record)){return Finish(true);}
            prefix.AppendData(Encoding.ASCII.GetBytes(rows[^1].Stamp));offset++;
        }
        if(request.Cursor is{} remaining&&offset<remaining.Offset){throw new ManagementException("RowConflict","読み込み済みの行が削除されています。編集内容を確認して再読込してください。");}
        return Finish(false);

        TableRows Finish(bool more){
            TableRowCursor? next=null;if(more){next=new(offset,Convert.ToHexString(prefix.GetCurrentHash()));}
            return new(version,rows.ToArray(),more,columns){KeyMembers=keyMembers,NextCursor=next};
        }

        bool Add(TRecord record){
            var key=RuntimeValueJson.Serialize(table.RecordKey(record));
            var values=RuntimeValueJson.Serialize(record);
            var size=(long)Encoding.UTF8.GetByteCount(key.GetRawText())+Encoding.UTF8.GetByteCount(values.GetRawText())+256;
            if(size>MaximumRowResponseBytes-bytes){
                if(rows.Count==0){throw new ManagementException("Limit","1行の表示データが応答上限を超えています。");}
                return false;
            }
            rows.Add(new(key,values,RowStamp(record)));
            bytes+=(int)size;
            return true;
        }
    }

    private static void ApplyTypedRows<TKey,TRecord>(object source,IEnumerable<TableRowChange> changes,CancellationToken cancellation)
        where TKey:struct where TRecord:struct{
        var table=(Table<TKey,TRecord>)source;
        var removals=new HashSet<TKey>(table.GatewayKeyComparer);
        var additions=new List<TRecord>();
        foreach(var change in changes){
            cancellation.ThrowIfCancellationRequested();
            switch(change.Kind){
                case TableRowChangeKind.Insert when change.Key is null&&change.ExpectedStamp is null&&change.Values is{} inserted:
                    additions.Add((TRecord)Materialize(inserted,typeof(TRecord)));
                    break;
                case TableRowChangeKind.Replace when change.Values is{} replacement:
                    RequireCurrent(change);
                    additions.Add((TRecord)Materialize(replacement,typeof(TRecord)));
                    break;
                case TableRowChangeKind.Delete when change.Values is null:
                    RequireCurrent(change);
                    break;
                default:throw new ManagementException("InvalidRequest","行変更の種類と値が一致しません: "+change.Table);
            }
        }
        foreach(var key in removals){
            cancellation.ThrowIfCancellationRequested();
            if(!table.Remove(key)){throw new ManagementException("Conflict","更新元の行がありません。");}
        }
        foreach(var record in additions){
            cancellation.ThrowIfCancellationRequested();
            if(!table.Add(record)){throw new ManagementException("Constraint","主キーが重複しています。");}
        }

        void RequireCurrent(TableRowChange change){
            if(change.Key is not{} input||change.ExpectedStamp is not{Length:64} stamp){
                throw new ManagementException("InvalidRequest","更新・削除には取得時の主キーと行識別値が必要です。");
            }
            var key=(TKey)Materialize(input,typeof(TKey));
            if(!removals.Add(key)){throw new ManagementException("InvalidRequest","同じ行が複数回指定されています。");}
            if(!table.TryGetValue(key,out var current)||RowStamp(current)!=stamp){
                throw new ManagementException("RowConflict","取得後に行が変更されました: "+change.Table);
            }
        }
    }

    private static string RowStamp<TRecord>(TRecord value)=>Convert.ToHexString(SHA256.HashData(CanonicalSerializer<TRecord>.Serialize(ref value)));
}
