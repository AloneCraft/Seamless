using Seamless.Database.Rdbms;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Hosting;

/// <summary>公開中の世代に属するDBを実SQL名で管理する。</summary>
public sealed partial class DatabaseRegistry:ISqlCatalog{
    private readonly Dictionary<string,DatabaseRegistration> databases=new(StringComparer.OrdinalIgnoreCase);
    public DatabaseRegistry(long generation=0){this.Generation=generation;}
    public long Generation{get;}
    public string DefaultDatabaseName{get;private set;}="master";
    public IReadOnlyCollection<DatabaseRegistration> Databases=>this.databases.Values;
    public void Register(string name,IDatabaseAdapter adapter,bool readOnly=false,string schemaFingerprint="",IReadOnlyList<DatabaseViewDefinition>? views=null,IReadOnlyList<DatabaseRoutineDefinition>? routines=null,string? defaultCollation=null){
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(adapter);
        if(!this.databases.TryAdd(name,new(name,adapter,readOnly,schemaFingerprint,views??[],routines??[],defaultCollation))){throw new InvalidOperationException($"Database '{name}' is already registered.");}
        if(adapter is InProcessDatabaseAdapter inProcess){inProcess.SetViews(views??[]);inProcess.SetRoutines(routines??[]);}
        if(this.databases.Count==1){this.DefaultDatabaseName=name;}
    }
    public string? GetDatabaseCollation(string database)=>this.Resolve(database).DefaultCollation;
    public DatabaseRegistration Resolve(string name){
        if(this.databases.TryGetValue(name,out var database)){return database;}
        if(name.Equals("master",StringComparison.OrdinalIgnoreCase)){return new("master",new MasterAdapter(),true,"",[],[]);}
        throw new SqlQueryException($"Cannot open database '{name}'.",4060);
    }
    public SqlTable? FindTable(SqlObjectName name){
        this.RejectForeignDatabaseTransaction(name.Database);
        if(name.Schema.Equals("sys",StringComparison.OrdinalIgnoreCase)&&name.Name.Equals("databases",StringComparison.OrdinalIgnoreCase)){
            var reference=this.databases.Values.FirstOrDefault();if(reference is null){return null;}if(!SystemCatalogEmulator.TryEmulate("SELECT * FROM sys.databases",reference.Adapter,out var shape)){return null;}
            return new(name,shape.Columns.Select(c=>new SqlColumn(c.Name,new SqlType(c.SqlTypeName,c.ClrType,c.MaximumLength,c.Precision,c.Scale,c.Collation),c.IsNullable,name,c.Ordinal)).ToArray(),_=>DatabaseRows());
        }
        if(!this.databases.TryGetValue(name.Database,out var registration)){return null;}
        using var read=ReadSqlDatabase(registration.Adapter);
        if((name.Schema.Equals("sys",StringComparison.OrdinalIgnoreCase)||name.Schema.Equals("seamless",StringComparison.OrdinalIgnoreCase)&&name.Name.Equals("generated_methods",StringComparison.OrdinalIgnoreCase))&&SystemCatalogEmulator.TryCatalog(name.Name,registration.Adapter,registration.Views,registration.Routines,out var catalog,registration.Name)){
            return new(name,catalog.Columns.Select(c=>new SqlColumn(c.Name,new SqlType(c.SqlTypeName,c.ClrType,c.MaximumLength,c.Precision,c.Scale,c.Collation),c.IsNullable,name,c.Ordinal)).ToArray(),_=>catalog.Rows);
        }
        var view=registration.Views.FirstOrDefault(v=>string.Equals(v.Schema,name.Schema,StringComparison.OrdinalIgnoreCase)&&string.Equals(v.Name,name.Name,StringComparison.OrdinalIgnoreCase));
        if(view is not null){return new(new(registration.Name,view.Schema,view.Name),view.Columns,null,view.Definition);}
        var identifier=new TableMetadata(name.Schema,name.Name,[]).IdentifierKey;
        var table=registration.Adapter.FindTable(registration.Adapter is InProcessDatabaseAdapter?identifier:name.Schema+"."+name.Name);
        if(table is null){return null;}
        var columns=table.Columns.Select(c=>new SqlColumn(c.Name,new SqlType(c.SqlTypeName,c.ClrType,c.MaximumLength,c.Precision,c.Scale,c.Collation),c.IsNullable,name,c.Ordinal)).ToArray();
        return new(new(registration.Name,table.SchemaName,table.TableName),columns,Read);
        IEnumerable<object?[]> Read(CancellationToken cancellation){
            using var session=new SessionContext(0,registration.Name){CancellationToken=cancellation,LockTimeoutMilliseconds=sqlSession.Value?.LockTimeoutMilliseconds??-1};
            using var read=ReadSqlDatabase(registration.Adapter,session);
            foreach(var row in registration.Adapter.ExecuteSelect(registration.Adapter is InProcessDatabaseAdapter?table.IdentifierKey:table.FullName,null,null,session).Rows){cancellation.ThrowIfCancellationRequested();yield return row;}
        }
    }
    private IEnumerable<object?[]> DatabaseRows(){
        yield return ["master",1,"Japanese_CI_AS",(byte)0,"ONLINE",(byte)160,false,(byte)0,"MULTI_USER",true,(byte)1,"FULL",0,"DATABASE_DEFAULT",(byte)0,"NONE",false,null];
        var id=5;foreach(var database in this.databases.Values){yield return [database.Name,id++,database.DefaultCollation??"Japanese_CI_AS",(byte)0,"ONLINE",(byte)160,database.IsReadOnly,(byte)0,"MULTI_USER",true,(byte)1,"FULL",0,"DATABASE_DEFAULT",(byte)0,"NONE",false,null];}
    }
    public SqlRoutine? FindRoutine(SqlObjectName name){
        this.RejectForeignDatabaseTransaction(name.Database);
        if(name.Schema.Equals("sys",StringComparison.OrdinalIgnoreCase)&&name.Name.Equals("HAS_DBACCESS",StringComparison.OrdinalIgnoreCase)){
            return new(name,null,new([new("@database",new("nvarchar",typeof(string),128),true)],SqlType.Int,true,arguments=>{
                if(arguments[0] is null){return null;}var database=(string)arguments[0]!;if(database.Equals("master",StringComparison.OrdinalIgnoreCase)||this.databases.ContainsKey(database)){return 1;}
                // msdb管理機能はこの互換profileでは利用不可。SSMSの能力照会へ0を返す。
                if(database.Equals("msdb",StringComparison.OrdinalIgnoreCase)){return 0;}return null;
            }));
        }
        if(!this.databases.TryGetValue(name.Database,out var registration)){return null;}
        using var read=ReadSqlDatabase(registration.Adapter);
        if(name.Schema.Equals("sys",StringComparison.OrdinalIgnoreCase)&&name.Name.ToUpperInvariant() is "SCHEMA_NAME" or "SCHEMA_ID" or "OBJECT_NAME" or "OBJECT_ID"){
            var function=name.Name.ToUpperInvariant();var schemaFunction=function.StartsWith("SCHEMA_",StringComparison.Ordinal);
            SystemCatalogEmulator.TryCatalog(schemaFunction?"schemas":"objects",registration.Adapter,registration.Views,registration.Routines,out var catalog);
            var inputId=function.EndsWith("_NAME",StringComparison.Ordinal);
            var clauses=new List<string>();
            foreach(var row in catalog.Rows){
                var objectName=(string)row[0]!;var id=(int)row[1]!;
                if(schemaFunction){clauses.Add(inputId?$"WHEN {id} THEN {Literal(objectName)}":$"WHEN {Literal(objectName)} THEN {id}");}
                else if(inputId){clauses.Add($"WHEN {id} THEN {Literal(objectName)}");}
                else{
                    var schemaId=(int)row[2]!;SystemCatalogEmulator.TryCatalog("schemas",registration.Adapter,registration.Views,registration.Routines,out var schemaCatalog);
                    var schema=(string)schemaCatalog.Rows.First(r=>Equals(r[1],schemaId))[0]!;
                    clauses.Add($"WHEN {Literal(schema+"."+objectName)} THEN {id}");
                    if(schema.Equals("dbo",StringComparison.OrdinalIgnoreCase)){clauses.Add($"WHEN {Literal(objectName)} THEN {id}");}
                }
            }
            var argument=inputId?"int":"nvarchar(776)";var output=inputId?"nvarchar(128)":"int";
            var expression=clauses.Count==0?"NULL":"CASE @value "+string.Join(" ",clauses)+" ELSE NULL END";
            return new(name,$"CREATE FUNCTION sys.{function}(@value {argument}) RETURNS {output} AS BEGIN RETURN {expression}; END");
        }
        static string Literal(string value)=>"N'"+value.Replace("'","''",StringComparison.Ordinal)+"'";
        var routine=registration.Routines.FirstOrDefault(r=>string.Equals(r.Schema,name.Schema,StringComparison.OrdinalIgnoreCase)&&string.Equals(r.Name,name.Name,StringComparison.OrdinalIgnoreCase));
        if(routine is null){return null;}
        if(routine.GeneratedMethod is{}method&&routine.Native is null){
            if(method.IsStub){throw new SqlQueryException($"Generated routine '{name.Schema}.{name.Name}' is a stub and cannot execute.",40515);}
            throw new SqlQueryException($"Generated routine '{name.Schema}.{name.Name}' is not bound to a database instance.",40515);
        }
        if(routine.Definition is null&&routine.Native is null){throw new SqlQueryException($"SQL definition is unavailable for '{name.Schema}.{name.Name}'.",40515);}
        return new(name,routine.Definition,routine.Native);
    }
    private sealed class MasterAdapter:IDatabaseAdapter{
        public string DatabaseName=>"master";
        public IReadOnlyList<TableMetadata> GetTables()=>[];
        public TableMetadata? FindTable(string name)=>null;
        public QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columns,Func<object,bool>? predicate,SessionContext session)=>throw new InvalidOperationException("The relation does not exist in master.");
        public int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?> values,SessionContext session)=>throw new InvalidOperationException("master is read only.");
        public int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?> values,Func<object,bool>? predicate,SessionContext session)=>throw new InvalidOperationException("master is read only.");
        public int ExecuteDelete(string tableName,Func<object,bool>? predicate,SessionContext session)=>throw new InvalidOperationException("master is read only.");
        public void BeginTransaction(SessionContext session)=>throw new InvalidOperationException("master is read only.");
        public void CommitTransaction(SessionContext session)=>throw new InvalidOperationException("No transaction is active.");
        public void RollbackTransaction(SessionContext session)=>throw new InvalidOperationException("No transaction is active.");
    }

}
public sealed record DatabaseRegistration(string Name,IDatabaseAdapter Adapter,bool IsReadOnly,string SchemaFingerprint,IReadOnlyList<DatabaseViewDefinition> Views,IReadOnlyList<DatabaseRoutineDefinition> Routines,string? DefaultCollation=null){
    private string originalFingerprint=SchemaFingerprint;
    public string SchemaFingerprint{get=>DatabaseRegistry.EffectiveSchemaFingerprint(this.Adapter,this.originalFingerprint);init=>this.originalFingerprint=value;}
}
public sealed record DatabaseViewDefinition(string Schema,string Name,string Definition,IReadOnlyList<SqlColumn> Columns,IReadOnlyList<ColumnCatalog>? SourceColumns=null);
public sealed record DatabaseRoutineDefinition(string Schema,string Name,string? Definition,RoutineCatalog? Catalog=null,GeneratedRoutineBinding? GeneratedMethod=null,SqlNativeRoutine? Native=null);
