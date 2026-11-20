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
            // 受付後に名前が別のDBへ付け替えられたなら、新しいDBへの権限より先に接続先の変更(Conflict)として扱う。
            var version=this.RowEditingVersion(request.Database,request.ExpectedVersion,admittedId);
            authorize?.Invoke();
            var database=this.Open(version).Database;
            var tables=DatabaseTableDeclaration.Read(database.GetType());
            if(!tables.Any(item=>item.Path==request.Table)&&DatabaseTableDeclaration.ReadDirectSets(database.GetType()).SingleOrDefault(item=>item.Path==request.Table) is{} direct){
                // 取引を通さない表は取引を開かずに読む(他のスレッドの書き込みと並んで動く)。
                var readSet=RowHandler<Func<object,TableRowsRequest,long,CancellationToken,TableRows>>(direct,DirectSetHandler(direct,nameof(ReadDirectSetRows),nameof(ReadDirectSetRowsKeyless)));
                var rows=readSet(direct.GetTable(database),request,version.Version,cancellation);authorize?.Invoke();return rows;
            }
            var declaration=FindRowTable(tables,request.Table);
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
            var directSets=DatabaseTableDeclaration.ReadDirectSets(database.GetType());
            var targets=request.Changes.Select(change=>change.Table).Distinct(StringComparer.Ordinal).ToArray();
            if(targets.Any(target=>directSets.Any(item=>item.Path==target))){
                if(!targets.All(target=>directSets.Any(item=>item.Path==target))){
                    throw new ManagementException("InvalidRequest","取引を通さない表の変更は、取引で書く表の変更と一緒には保存できません。分けて保存してください。");
                }
                // 取引を通さない表：先に全部の行を照合(値の比較)してから書く。書き始めた後の失敗は取り消せない。
                var writes=request.Changes.GroupBy(change=>change.Table,StringComparer.Ordinal).Select(group=>{
                    var direct=directSets.Single(item=>item.Path==group.Key);
                    var prepare=RowHandler<Func<object,IEnumerable<TableRowChange>,CancellationToken,Action>>(direct,DirectSetHandler(direct,nameof(PrepareDirectSetRows),nameof(PrepareDirectSetRowsKeyless)));
                    return prepare(direct.GetTable(database),group,cancellation);
                }).ToArray();
                cancellation.ThrowIfCancellationRequested();
                foreach(var write in writes){write();}
                return new(version.Version,request.OperationId,request.Changes.Length);
            }
            using var transaction=database.BeginPlannedRowTransaction(targets,cancellation);
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
        var table=(StoredTable<TKey,TRecord>)source;
        TRecord[]? keyed=null;
        if(request.Key is{} key){cancellation.ThrowIfCancellationRequested();keyed=table.TryGetValue((TKey)Materialize(key,typeof(TKey)),out var record)?[record]:[];}
        return ReadRows(table,record=>table.RecordKey(record),keyed,typeof(TKey),request,version,cancellation);
    }

    // 取引を通さない表の名前で、キー付き(Set<TKey,TRecord>)とキー無し(Set<TRecord>)の処理を選ぶ。
    private static string DirectSetHandler(DatabaseTableDeclaration declaration,string keyed,string keyless)=>
        declaration.Property.PropertyType.GetGenericTypeDefinition()==typeof(Set<,>)?keyed:keyless;

    private static TableRows ReadDirectSetRows<TKey,TRecord>(object source,TableRowsRequest request,long version,CancellationToken cancellation)
        where TRecord:global::Seamless.Collections.IRecord<TKey>{
        var set=(Set<TKey,TRecord>)source;
        TRecord[]? keyed=null;
        if(request.Key is{} key){cancellation.ThrowIfCancellationRequested();keyed=set.TryGetValue((TKey)Materialize(key,typeof(TKey)),out var record)?[record]:[];}
        return ReadRows(set,static record=>record.Key,keyed,typeof(TKey),request,version,cancellation) with{ValueComparisonOnly=true};
    }

    // キー無しの表は行そのものがキー。
    private static TableRows ReadDirectSetRowsKeyless<TRecord>(object source,TableRowsRequest request,long version,CancellationToken cancellation)
        where TRecord:notnull{
        var set=(Set<TRecord>)source;
        TRecord[]? keyed=null;
        if(request.Key is{} key){cancellation.ThrowIfCancellationRequested();var record=(TRecord)Materialize(key,typeof(TRecord));keyed=set.Contains(record)?[record]:[];}
        return ReadRows(set,static record=>record,keyed,typeof(TRecord),request,version,cancellation) with{ValueComparisonOnly=true};
    }

    internal static TableRows ReadRows<TRecord>(IEnumerable<TRecord> source,Func<TRecord,object?> recordKey,TRecord[]? keyed,Type keyType,TableRowsRequest request,long version,CancellationToken cancellation,Func<TRecord,string>? stamp=null){
        stamp??=RowStamp;
        var rows=new List<TableRowSnapshot>();
        var columns=RuntimeValueJson.Columns(typeof(TRecord));
        var keyMembers=RuntimeValueJson.Columns(keyType);
        var bytes=0;
        if(keyed is not null){
            foreach(var record in keyed){Add(record);}
            return new(version,rows.ToArray(),false,columns){KeyMembers=keyMembers};
        }
        // ページ間にDBロックや列挙子を残さず、既読部分を照合して更新による重複・欠落を検出する。
        // 列挙元の再開カーソルがないため既読部分を再走査する。メモリ使用量は1ページに限定する。
        using var prefix=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        prefix.AppendData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{request.Database,request.Table,Version=version})));
        long offset=0;
        foreach(var record in source){
            cancellation.ThrowIfCancellationRequested();
            if(request.Cursor is{} continuation&&offset<continuation.Offset){
                prefix.AppendData(Encoding.ASCII.GetBytes(stamp(record)));offset++;
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
            var key=RuntimeValueJson.Serialize(recordKey(record));
            var values=RuntimeValueJson.Serialize(record);
            var size=(long)Encoding.UTF8.GetByteCount(key.GetRawText())+Encoding.UTF8.GetByteCount(values.GetRawText())+256;
            if(size>MaximumRowResponseBytes-bytes){
                if(rows.Count==0){throw new ManagementException("Limit","1行の表示データが応答上限を超えています。");}
                return false;
            }
            rows.Add(new(key,values,stamp(record)));
            bytes+=(int)size;
            return true;
        }
    }

    private static void ApplyTypedRows<TKey,TRecord>(object source,IEnumerable<TableRowChange> changes,CancellationToken cancellation)
        where TKey:struct where TRecord:struct{
        var table=(StoredTable<TKey,TRecord>)source;
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

    // 取引を通さない表の変更を照合し、書き込む処理を返す。照合はすべて書き込みの前に行う(値の比較だけ。照合から書き込みまでの他者の書き込みは見つけられない)。
    private static Action PrepareDirectSetRows<TKey,TRecord>(object source,IEnumerable<TableRowChange> changes,CancellationToken cancellation)
        where TRecord:global::Seamless.Collections.IRecord<TKey>{
        var set=(Set<TKey,TRecord>)source;
        var removals=new HashSet<TKey>();
        var additions=new List<TRecord>();
        foreach(var change in changes){
            cancellation.ThrowIfCancellationRequested();
            switch(change.Kind){
                case TableRowChangeKind.Insert when change.Key is null&&change.ExpectedStamp is null&&change.Values is{} inserted:
                    additions.Add((TRecord)Materialize(inserted,typeof(TRecord)));break;
                case TableRowChangeKind.Replace when change.Values is{} replacement:
                    RequireCurrent(change);additions.Add((TRecord)Materialize(replacement,typeof(TRecord)));break;
                case TableRowChangeKind.Delete when change.Values is null:
                    RequireCurrent(change);break;
                default:throw new ManagementException("InvalidRequest","行変更の種類と値が一致しません: "+change.Table);
            }
        }
        var added=new HashSet<TKey>();
        foreach(var record in additions){
            if(!added.Add(record.Key)||!removals.Contains(record.Key)&&set.ContainsKey(record.Key)){throw new ManagementException("Constraint","主キーが重複しています。");}
        }
        return ()=>{
            foreach(var key in removals){if(!set.RemoveKey(key)){throw new ManagementException("Conflict","照合の後に他者が行を消しました。取引を通さない表なので、ここまでの書き込みは取り消せません。読み直してください。");}}
            foreach(var record in additions){if(!set.Add(record)){throw new ManagementException("Conflict","照合の後に他者が同じキーの行を足しました。取引を通さない表なので、ここまでの書き込みは取り消せません。読み直してください。");}}
        };

        void RequireCurrent(TableRowChange change){
            if(change.Key is not{} input||change.ExpectedStamp is not{Length:64} stamp){throw new ManagementException("InvalidRequest","更新・削除には取得時の主キーと行識別値が必要です。");}
            var key=(TKey)Materialize(input,typeof(TKey));
            if(!removals.Add(key)){throw new ManagementException("InvalidRequest","同じ行が複数回指定されています。");}
            if(!set.TryGetValue(key,out var current)||RowStamp(current)!=stamp){throw new ManagementException("RowConflict","取得後に行が変更されました: "+change.Table);}
        }
    }

    // キー無しの表は行そのものがキー。更新は古い行を消して新しい行を足す。
    private static Action PrepareDirectSetRowsKeyless<TRecord>(object source,IEnumerable<TableRowChange> changes,CancellationToken cancellation)
        where TRecord:notnull{
        var set=(Set<TRecord>)source;
        var removals=new HashSet<TRecord>();
        var additions=new List<TRecord>();
        foreach(var change in changes){
            cancellation.ThrowIfCancellationRequested();
            switch(change.Kind){
                case TableRowChangeKind.Insert when change.Key is null&&change.ExpectedStamp is null&&change.Values is{} inserted:
                    additions.Add((TRecord)Materialize(inserted,typeof(TRecord)));break;
                case TableRowChangeKind.Replace when change.Values is{} replacement:
                    RequireCurrent(change);additions.Add((TRecord)Materialize(replacement,typeof(TRecord)));break;
                case TableRowChangeKind.Delete when change.Values is null:
                    RequireCurrent(change);break;
                default:throw new ManagementException("InvalidRequest","行変更の種類と値が一致しません: "+change.Table);
            }
        }
        var added=new HashSet<TRecord>();
        foreach(var record in additions){
            if(!added.Add(record)||!removals.Contains(record)&&set.Contains(record)){throw new ManagementException("Constraint","同じ行がすでにあります。");}
        }
        return ()=>{
            foreach(var record in removals){if(!set.Remove(record)){throw new ManagementException("Conflict","照合の後に他者が行を消しました。取引を通さない表なので、ここまでの書き込みは取り消せません。読み直してください。");}}
            foreach(var record in additions){if(!set.Add(record)){throw new ManagementException("Conflict","照合の後に他者が同じ行を足しました。取引を通さない表なので、ここまでの書き込みは取り消せません。読み直してください。");}}
        };

        void RequireCurrent(TableRowChange change){
            if(change.Key is not{} input||change.ExpectedStamp is not{Length:64} stamp){throw new ManagementException("InvalidRequest","更新・削除には取得時の行と行識別値が必要です。");}
            var record=(TRecord)Materialize(input,typeof(TRecord));
            if(!removals.Add(record)){throw new ManagementException("InvalidRequest","同じ行が複数回指定されています。");}
            if(!set.Contains(record)||RowStamp(record)!=stamp){throw new ManagementException("RowConflict","取得後に行が変更されました: "+change.Table);}
        }
    }

    private static string RowStamp<TRecord>(TRecord value)=>Convert.ToHexString(SHA256.HashData(CanonicalSerializer<TRecord>.Serialize(ref value)));
}
