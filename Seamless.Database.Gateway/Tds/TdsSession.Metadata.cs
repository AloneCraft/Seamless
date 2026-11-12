using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Query;
using Seamless.Execution;
using Seamless.Security;

namespace Seamless.Database.Gateway.Tds;

public sealed partial class TdsSession{
    private SqlTable? FindProtectedMetadata(SqlObjectName name,AuthorizationLease authorization){
        if(!name.Schema.Equals("sys",StringComparison.OrdinalIgnoreCase)){return null;}
        var policy=this.Protection!.Policy;
        if(name.Name.Equals("databases",StringComparison.OrdinalIgnoreCase)){
            return new(name,[new("name",MetadataName,false,name)],_=>this.sqlEngine.Databases.Databases
                .Where(database=>policy.CanDiscover(authorization,database.Name)).Select(database=>new object?[]{database.Name}));
        }
        if(name.Name.Equals("schemas",StringComparison.OrdinalIgnoreCase)){
            return new(name,[new("name",MetadataName,false,name)],cancellation=>this.ReadProtectedSchemas(name.Database,authorization,cancellation));
        }
        return null;
    }

    private IEnumerable<object?[]> ReadProtectedSchemas(string database,AuthorizationLease authorization,CancellationToken cancellation){
        using var memory=this.CreateMetadataBudget(cancellation).ReserveMemory(128);
        var names=this.ReadMetadataDatabase(database,registration=>{
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ordered=new List<string>();
            var candidates=this.VisibleTables(registration,authorization).Select(table=>table.SchemaName)
                .Concat(this.VisibleViews(registration,authorization).Select(view=>view.Schema));
            foreach(var name in candidates){
                cancellation.ThrowIfCancellationRequested();
                if(name.Length==0||seen.Contains(name)){continue;}
                // 重複除去と順序保持に使うバッファを、追加する前に計上する。
                memory.Resize(checked(memory.Bytes+128));
                seen.Add(name);
                ordered.Add(name);
            }
            return ordered;
        });
        foreach(var name in names){
            cancellation.ThrowIfCancellationRequested();
            yield return [name];
        }
    }

    private QueryResult? ExecuteProtectedMetadataProcedure(SqlObjectName name,IEnumerable<(string Name,object? Value,bool Output)> arguments,AuthorizationLease authorization){
        var procedure=name.Name.ToLowerInvariant();
        if(procedure is not("sp_tables" or "sp_pkeys")||name.Schema.ToLowerInvariant() is not("" or "dbo" or "sys")){return null;}
        var isTables=procedure=="sp_tables";
        var values=BindNamedOrPositionalValues(isTables?TableArguments:KeyArguments,arguments);
        var tableName=Text("@table_name");
        var schema=Text("@table_owner");
        var qualifier=Text("@table_qualifier");
        var policy=this.Protection!.Policy;
        if(qualifier is not null&&!qualifier.Equals(name.Database,StringComparison.OrdinalIgnoreCase)){
            // 一覧取得の呼び出しで接続先データベースを切り替えない。
            return isTables?TableResult([]):KeyResult([]);
        }
        if(!policy.CanDiscover(authorization,name.Database)){throw new SqlQueryException("The identity is not permitted to discover this database.",229);}
        var types=isTables?Text("@table_type")?.Split(',').Select(static type=>type.Trim().Trim('\'','"').Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase):null;
        var cancellation=this.sessionContext.CancellationToken;
        var memory=this.CreateMetadataBudget(cancellation).ReserveMemory(64);
        try{
            var result=this.ReadMetadataDatabase(name.Database,registration=>{
                var rows=new List<object?[]>();
                if(isTables){
                    if(types is null||types.Contains("TABLE")){
                        foreach(var table in VisibleTables(registration,authorization)){
                            if(Matches(table.SchemaName,schema,registration)&&Matches(table.TableName,tableName,registration)){
                                ReserveRow(5);
                                rows.Add([registration.Name,table.SchemaName,table.TableName,"TABLE",null]);
                            }
                        }
                    }
                    if(types is null||types.Contains("VIEW")){
                        foreach(var view in VisibleViews(registration,authorization)){
                            if(Matches(view.Schema,schema,registration)&&Matches(view.Name,tableName,registration)){
                                ReserveRow(5);
                                rows.Add([registration.Name,view.Schema,view.Name,"VIEW",null]);
                            }
                        }
                    }
                    return TableResult(rows);
                }
                foreach(var table in VisibleTables(registration,authorization)){
                    if(!table.TableName.Equals(tableName,StringComparison.OrdinalIgnoreCase)
                        ||schema is not null&&!table.SchemaName.Equals(schema,StringComparison.OrdinalIgnoreCase)||table.PrimaryKey is not{} key){continue;}
                    for(var index=0;index<key.Columns.Count;index++){
                        ReserveRow(6);
                        rows.Add([registration.Name,table.SchemaName,table.TableName,key.Columns[index].Name,checked((short)(index+1)),key.Name]);
                    }
                }
                return KeyResult(rows);
            });
            this.sessionContext.RetainResults(memory);
            return result;
        }catch{
            memory.Dispose();
            throw;
        }
        void ReserveRow(int fields){
            cancellation.ThrowIfCancellationRequested();
            memory.Resize(checked(memory.Bytes+64L+fields*(IntPtr.Size+32L)));
        }
        string? Text(string key)=>values[key] switch{null=>null,string value when value.Length<=1024=>value,_=>throw ArgumentsDoNotMatch()};
    }

    private ResourceBudget CreateMetadataBudget(CancellationToken cancellation){
        var options=this.sqlEngine.Databases.GatewayTransactions?.QueryExecutionOptions(cancellation)??new ExecutionOptions{CancellationToken=cancellation};
        return options.ParentBudget?.CreateChild(options.MemoryLimitBytes,options.TemporaryStorageLimitBytes)
            ??new ResourceBudget(options.MemoryLimitBytes,options.TemporaryStorageLimitBytes);
    }

    private static bool Matches(string value,string? pattern,DatabaseRegistration registration)=>pattern is null||SqlScalarConversion.MatchesPattern(value,pattern,registration.DefaultCollation);
    private T ReadMetadataDatabase<T>(string database,Func<DatabaseRegistration,T> read)=>this.sessionContext.Run(()=>{
        using var context=DatabaseRegistry.EnterSqlSession(this.sessionContext);
        var registration=this.sqlEngine.Databases.Resolve(database);
        using var lease=DatabaseRegistry.ReadSqlDatabase(registration.Adapter,this.sessionContext);
        return read(registration);
    });
    private IEnumerable<TableMetadata> VisibleTables(DatabaseRegistration registration,AuthorizationLease authorization){
        var tables=registration.Adapter is InProcessDatabaseAdapter inProcess?inProcess.EnumerateTables():registration.Adapter.GetTables();
        return tables.Where(table=>{
            this.sessionContext.CancellationToken.ThrowIfCancellationRequested();
            return this.Protection!.Policy.CanAccessTable(authorization,registration.Name,registration.Adapter,table,SecurityPermission.Discover);
        });
    }
    private IEnumerable<DatabaseViewDefinition> VisibleViews(DatabaseRegistration registration,AuthorizationLease authorization)
        =>registration.Views.Where(view=>{
            this.sessionContext.CancellationToken.ThrowIfCancellationRequested();
            return this.Protection!.Policy.AccessSqlObject(authorization,registration.Adapter,
                new(registration.Name,view.Schema,view.Name),SecurityResourceKind.View,SecurityPermission.Discover) is not null;
        });

    private static QueryResult TableResult(IEnumerable<object?[]> rows)=>new([
        NameColumn("TABLE_QUALIFIER",0),NameColumn("TABLE_OWNER",1),NameColumn("TABLE_NAME",2),NameColumn("TABLE_TYPE",3),
        new("REMARKS",typeof(string),"nvarchar",true,false,4,254)],rows,-1){StatementKind=SqlCommandKind.Select};
    private static QueryResult KeyResult(IEnumerable<object?[]> rows)=>new([
        NameColumn("TABLE_QUALIFIER",0),NameColumn("TABLE_OWNER",1),NameColumn("TABLE_NAME",2),NameColumn("COLUMN_NAME",3),
        new("KEY_SEQ",typeof(short),"smallint",false,false,4),NameColumn("PK_NAME",5)],rows,-1){StatementKind=SqlCommandKind.Select};
    private static ColumnMetadata NameColumn(string name,int ordinal)=>new(name,typeof(string),"nvarchar",false,false,ordinal,128);
}
