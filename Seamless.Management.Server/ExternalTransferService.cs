using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Seamless.Database.Rdbms;
using Seamless.Database.SqlServer;
using Seamless.Database.Transfer;

namespace Seamless.Management.Server;

/// <summary>
/// 外部DB(今は SQL Server だけ。決定1)からの取り込み。接続・下見・取り込み(裏で動かし、進み具合を見に行く)。
/// 取り込みは、生成した C# を DB 定義にして公開し、その新しい世代へ SQL Server の行を入れる(<see cref="ManagementService.ApplyExternalImport"/>)。
/// 最後に、生成した C# を DB のソースのプロジェクトとして保存先に書く(決定3)。
/// 設計：Document/refactoring-designs/20261117-外部DBとファイルの取り込みと書き出し.md の「Manager の取り込み」。
/// </summary>
internal sealed class ExternalTransferService(ManagementService service,SourceProjectService? sourceProjects,IExternalCredentialStore credentials){
    internal const string SqlServerProvider="SqlServer";
    private readonly ConcurrentDictionary<Guid,ExternalTransferStatus> operations=new();

    public async Task<ExternalConnectionInfo> ConnectAsync(ExternalConnectionRequest request,CancellationToken cancellationToken){
        var (connection,builder)=this.Resolve(request);
        await using var sql=new SqlConnection(new SqlConnectionStringBuilder(connection){InitialCatalog="master"}.ConnectionString);
        await sql.OpenAsync(cancellationToken).ConfigureAwait(false);
        var databases=new List<string>();
        await using(var command=new SqlCommand("SELECT name FROM sys.databases WHERE database_id>4 AND state=0 AND HAS_DBACCESS(name)=1 ORDER BY name;",sql))
        await using(var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false)){
            while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false)){databases.Add(reader.GetString(0));}
        }
        // 接続できてから保存する(打ち間違えたパスワードを残さない)。
        if(request.SavePassword&&!builder.IntegratedSecurity&&request.Password is{} password){credentials.Write(Target(builder),builder.UserID,password);}
        var saved=!builder.IntegratedSecurity&&credentials.Read(Target(builder)) is not null;
        return new(SqlServerProvider,builder.DataSource,builder.IntegratedSecurity?null:builder.UserID,builder.IntegratedSecurity,databases.ToArray(),saved);
    }

    public async Task<ExternalImportPreview> PreviewAsync(UserAccount user,ExternalImportPreviewRequest request,CancellationToken cancellationToken){
        var prepared=await this.PrepareAsync(request,cancellationToken).ConfigureAwait(false);
        var existing=service.FindDatabaseDefinition(user,request.TargetDatabase);
        return prepared.Preview(existing is not null);
    }

    public ExternalTransferStatus Start(UserAccount user,ExternalImportRequest request){
        ArgumentNullException.ThrowIfNull(request);
        if(request.OperationId==Guid.Empty){throw new ManagementException("InvalidRequest","操作IDを指定してください。");}
        if(sourceProjects is null){throw new ManagementException("Unsupported","この接続では DB のソースのプロジェクトを書けません。");}
        if(string.IsNullOrWhiteSpace(request.ProjectDirectory)){throw new ManagementException("InvalidRequest","生成した C# を置くプロジェクトの保存先を指定してください。");}
        var directory=Path.GetFullPath(request.ProjectDirectory);
        if(Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory).Any()){throw new ManagementException("Conflict","空のプロジェクト保存先を指定してください。");}
        // 接続とパスワードを、裏で動かす前に確かめる(パスワードは要求の中だけに持ち、記録に残さない)。
        _=this.Resolve(request.Preview.Connection);
        var status=new ExternalTransferStatus(request.OperationId,"Running","Reading",null,0,0,null,null);
        if(!this.operations.TryAdd(request.OperationId,status)){throw new ManagementException("Conflict","操作IDは別の要求に使用されています。");}
        _=Task.Run(()=>this.RunImportAsync(user,request,directory));
        return status;
    }

    public ExternalTransferStatus Status(Guid operationId)=>
        this.operations.TryGetValue(operationId,out var status)?status:throw new ManagementException("NotFound","取り込みの操作がありません(Manager を起動し直すと進み具合は消えます。DB の履歴を確かめてください)。");

    private async Task RunImportAsync(UserAccount user,ExternalImportRequest request,string directory){
        var id=request.OperationId;
        void Set(Func<ExternalTransferStatus,ExternalTransferStatus> change){this.operations.AddOrUpdate(id,_=>throw new InvalidOperationException(),(_,current)=>change(current));}
        try{
            var prepared=await this.PrepareAsync(request.Preview,CancellationToken.None).ConfigureAwait(false);
            if(prepared.Generated.SchemaFingerprint!=request.PreviewToken){throw new ManagementException("Conflict","下見の後に元の DB の形が変わりました。下見をやり直してください。");}
            var existing=service.FindDatabaseDefinition(user,request.Preview.TargetDatabase);
            if(existing is not null&&!request.ReplaceExisting){throw new ManagementException("Conflict","同じ名前の DB があります。置き換えるかどうかを選んでください。");}
            var definition=new Definition(existing?.Definition.Id??"db-"+Guid.NewGuid().ToString("N"),existing?.Definition.Database??request.Preview.TargetDatabase,DefinitionKind.Database,
                $"{prepared.Generated.TypeNamespace}.{prepared.Generated.DatabaseName}","",
                new SourceSnapshot("import-"+prepared.Generated.SchemaFingerprint[..16].ToLowerInvariant(),prepared.Generated.Files.Select(static file=>new SourceFile(file.RelativePath,file.Content)).ToArray(),[]));
            var total=prepared.Tables.Sum(static table=>table.Rows);
            Set(current=>current with{Phase="Building",TotalRows=total});
            var counts=new Dictionary<string,long>(StringComparer.Ordinal);
            var progress=new Progress(update=>Set(current=>current with{Phase="Loading",Table=update.Table,Rows=update.Rows}));
            var operation=service.ApplyExternalImport(user,definition,id,existing is not null,(instance,token)=>{
                using var source=new SqlServerImportSource(prepared.ConnectionString,prepared.Generated.Tables,consistentRead:true);
                var loaded=GeneratedDatabaseLoader.Load(instance.Database,prepared.Generated.Tables,source,progress,token);
                source.Complete();
                // 入れた件数を、新しい世代の表で数え直して確かめてから公開する(決定7)。
                foreach(var (name,count) in loaded){
                    var stored=CountRows(instance.Database,name);
                    if(stored!=count||source.RowsReadByTable.GetValueOrDefault(name,-1)!=count){throw new InvalidDataException($"表 {name} の件数が合いません。読んだ {source.RowsReadByTable.GetValueOrDefault(name,-1)} 件、入れた {count} 件、数え直し {stored} 件。");}
                    counts[name]=count;
                }
                Set(current=>current with{Phase="Publishing",Table=null});
            });
            if(operation.State!=OperationState.Succeeded){throw new ManagementException("ImportFailed",operation.Error??"取り込みが公開されませんでした。");}
            Set(current=>current with{Phase="WritingProject"});
            var published=service.GetDefinition(user,definition.Id);
            var project=await sourceProjects!.CreateSourceProjectAsync(new(directory,published.Definition.Database,new DefinitionExport(published.Definition,published.Version,0)),CancellationToken.None).ConfigureAwait(false);
            var tables=prepared.Generated.Tables.Select(table=>new ExternalTableSummary(table.SourceSchema,table.SourceTable,counts.GetValueOrDefault(table.ObjectName))).ToArray();
            var report=new ExternalImportReport(published.Definition.Database,published.Definition.Id,published.Version,tables,prepared.Views,prepared.Issues,operation.BackupPath,project.ProjectPath);
            Set(current=>current with{State="Succeeded",Phase="Done",Table=null,Report=report});
        }catch(Exception error){
            var state=error is OperationCanceledException?"Cancelled":"Failed";
            Set(current=>current with{State=state,Error=error.GetBaseException().Message});
        }
    }

    private static long CountRows(Seamless.Database.DatabaseContainer database,string path){
        object current=database;
        foreach(var segment in path.Split('.')){current=current.GetType().GetProperty(segment,BindingFlags.Instance|BindingFlags.Public)!.GetValue(current)!;}
        var property=current.GetType().GetProperty("LongCount",BindingFlags.Instance|BindingFlags.Public)??throw new MissingMemberException(current.GetType().FullName,"LongCount");
        return Convert.ToInt64(property.GetValue(current),System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class Progress(Action<GeneratedTableLoadProgress> report):IProgress<GeneratedTableLoadProgress>{
        public void Report(GeneratedTableLoadProgress value)=>report(value);
    }

    private sealed record Prepared(string ConnectionString,GeneratedDatabase Generated,ExternalImportPreviewRequest Request,ExternalTableSummary[] Tables,ExternalViewSummary[] Views,ExternalCompatibilityIssue[] Issues){
        public ExternalImportPreview Preview(bool targetExists)=>new(this.Generated.SchemaFingerprint,this.Request.SourceDatabase,this.Request.TargetDatabase,targetExists,this.Tables,this.Views,this.Issues,
            this.Generated.Files.Count,this.Generated.Files.Sum(static file=>(long)file.Content.Length));
    }

    // 元の DB を読み、ビューを訳す指定で C# を生成する。行数は SQL Server の目安(sys.partitions)。件数は取り込みで数え直す。
    private async Task<Prepared> PrepareAsync(ExternalImportPreviewRequest request,CancellationToken cancellationToken){
        ArgumentNullException.ThrowIfNull(request);
        if(string.IsNullOrWhiteSpace(request.SourceDatabase)){throw new ManagementException("InvalidRequest","取り込む元の DB を選んでください。");}
        if(string.IsNullOrWhiteSpace(request.TargetDatabase)||request.TargetDatabase.Length>128||request.TargetDatabase.Any(char.IsControl)){throw new ManagementException("InvalidRequest","取り込み先の DB の名前が不正です。");}
        var (connection,_)=this.Resolve(request.Connection);
        var source=new SqlConnectionStringBuilder(connection){InitialCatalog=request.SourceDatabase}.ConnectionString;
        var catalog=await new SqlServerCatalogReader(source).ReadAsync(cancellationToken).ConfigureAwait(false);
        var snapshots=(request.SnapshotViews??[]).ToArray();
        var known=catalog.Schemas.SelectMany(static schema=>schema.Views.Select(view=>schema.Name+"."+view.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if(snapshots.FirstOrDefault(name=>!known.Contains(name)) is{} unknown){throw new ManagementException("InvalidRequest","写しにするビューが元の DB にありません: "+unknown);}
        var generated=CSharpDatabaseGenerator.Generate(catalog,new ViewImportOptions(new SqlServerViewTranslator()){SnapshotViews=snapshots});
        try{SourceEditor.Validate(new SourceSnapshot("preview",generated.Files.Select(static file=>new SourceFile(file.RelativePath,file.Content)).ToArray(),[]));}
        catch(ManagementException error){throw new ManagementException("Limit","生成した C# が DB 定義の上限(256ファイル・4MB)を超えます: "+generated.Files.Count+" ファイル。"+error.Message);}
        var rows=new Dictionary<(string,string),long>();
        await using(var sql=new SqlConnection(source)){
            await sql.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command=new SqlCommand("SELECT s.name,o.name,SUM(p.rows) FROM sys.objects AS o INNER JOIN sys.schemas AS s ON s.schema_id=o.schema_id INNER JOIN sys.partitions AS p ON p.object_id=o.object_id AND p.index_id IN (0,1) WHERE o.type IN ('U','V') GROUP BY s.name,o.name;",sql);
            await using var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false)){rows[(reader.GetString(0),reader.GetString(1))]=reader.GetInt64(2);}
        }
        var tables=generated.Tables.Select(table=>new ExternalTableSummary(table.SourceSchema,table.SourceTable,rows.GetValueOrDefault((table.SourceSchema,table.SourceTable)))).ToArray();
        var translated=generated.Views.Select(static view=>(view.SourceSchema,view.SourceName)).ToHashSet();
        var snapshotSet=snapshots.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var views=catalog.Schemas.SelectMany(schema=>schema.Views.Select(view=>new ExternalViewSummary(schema.Name,view.Name,
            snapshotSet.Contains(schema.Name+"."+view.Name)?"Snapshot":translated.Contains((schema.Name,view.Name))?"Translated":"NotImported"))).ToArray();
        var issues=generated.Compatibility.Select(static issue=>new ExternalCompatibilityIssue(issue.Kind.ToString(),issue.Schema,issue.Name,issue.Column,issue.Code,issue.Difference,
            issue.Workarounds.Select(static workaround=>new ExternalWorkaround(workaround.Code,workaround.Description,workaround.Draft)).ToArray())).ToArray();
        return new(source,generated,request,tables,views,issues);
    }

    // 接続文字列を確かめ、パスワードを足す。パスワードは接続文字列に入れさせない(履歴に残るため。決定2)。
    private (string ConnectionString,SqlConnectionStringBuilder Builder) Resolve(ExternalConnectionRequest request){
        ArgumentNullException.ThrowIfNull(request);
        if(!string.Equals(request.Provider,SqlServerProvider,StringComparison.Ordinal)){throw new ManagementException("Unsupported","対応している製品は SQL Server だけです。");}
        SqlConnectionStringBuilder builder;
        try{builder=new(request.ConnectionString);}catch(Exception error) when(error is ArgumentException or FormatException or KeyNotFoundException){throw new ManagementException("InvalidRequest","接続文字列を読めません: "+error.Message);}
        if(builder.Password.Length>0){throw new ManagementException("InvalidRequest","パスワードは接続文字列に入れず、パスワードの欄に入れてください。");}
        if(string.IsNullOrWhiteSpace(builder.DataSource)){throw new ManagementException("InvalidRequest","接続文字列にサーバー(Server)がありません。");}
        if(builder.IntegratedSecurity){return (builder.ConnectionString,builder);}
        if(string.IsNullOrEmpty(builder.UserID)){throw new ManagementException("InvalidRequest","接続文字列に利用者(User ID)がないか、Windows 認証(Integrated Security=True)を指定してください。");}
        var password=request.Password??credentials.Read(Target(builder))??throw new ManagementException("Credential","パスワードを入れてください(資格情報マネージャーに保存がありません)。");
        var withPassword=new SqlConnectionStringBuilder(builder.ConnectionString){Password=password};
        return (withPassword.ConnectionString,builder);
    }

    private static string Target(SqlConnectionStringBuilder builder)=>"Seamless.Manager/SqlServer/"+builder.DataSource+"/"+builder.UserID;
}
