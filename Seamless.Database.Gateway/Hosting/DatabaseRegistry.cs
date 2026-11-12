using Seamless.Database.Rdbms;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Hosting;

/// <summary>公開中の世代に属するDBを実SQL名で管理する。</summary>
public sealed partial class DatabaseRegistry:ISqlCatalog{
    private Dictionary<string,DatabaseRegistration> databases=new(StringComparer.OrdinalIgnoreCase);
    private readonly object publicationGate=new();
    private readonly List<WeakReference<SqlQueryPlan>>plans=[];
    public void TrackPlan(SqlQueryPlan plan){lock(this.publicationGate){if(this.plans.Count>256){this.plans.RemoveAll(static entry=>!entry.TryGetTarget(out _));}this.plans.Add(new(plan));}}
    internal void ReleaseObsoletePlans(){WeakReference<SqlQueryPlan>[] snapshot;lock(this.publicationGate){snapshot=this.plans.ToArray();}foreach(var weak in snapshot){if(weak.TryGetTarget(out var plan)){plan.ReleaseIfObsolete();}}}
    public DatabaseRegistry(long generation=0,GatewayTransactionManager? transactions=null){this.Generation=generation;this.GatewayTransactions=transactions;}
    public GatewayTransactionManager? GatewayTransactions{get;}
    public long Generation{get;}
    public string DefaultDatabaseName{get;private set;}="master";
    public IReadOnlyCollection<DatabaseRegistration> Databases=>this.databases.Values;
    public void Register(string name,IDatabaseAdapter adapter,bool readOnly=false,string schemaFingerprint="",IReadOnlyList<DatabaseViewDefinition>? views=null,IReadOnlyList<DatabaseRoutineDefinition>? routines=null,string? defaultCollation=null){
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(adapter);
        lock(this.publicationGate){
        if(this.databases.ContainsKey(name)){throw new InvalidOperationException($"Database '{name}' is already registered.");}
        if(adapter is InProcessDatabaseAdapter candidate){
            if(this.GatewayTransactions is not null){if(candidate.Database.Options.GatewayProfile is null){throw new InvalidOperationException("All registered Gateway databases must use the common storage profile.");}this.GatewayTransactions.Register(name,candidate);}
            else if(candidate.Database.Options.GatewayProfile is not null){throw new InvalidOperationException("The database requires its owning Gateway transaction manager.");}
        }
        var next=new Dictionary<string,DatabaseRegistration>(this.databases,StringComparer.OrdinalIgnoreCase){{name,new(name,adapter,readOnly,schemaFingerprint,views??[],routines??[],defaultCollation)}};
        if(adapter is InProcessDatabaseAdapter inProcess){inProcess.SetViews(views??[]);inProcess.SetRoutines(routines??[]);}
        if(next.Count==1){this.DefaultDatabaseName=name;}Volatile.Write(ref this.databases,next);
        }
    }
    public string? GetDatabaseCollation(string database){
        // msdb などの値を持たないシステムデータベースは、サーバーの既定の照合順序を使う。
        if(!this.databases.ContainsKey(database)&&Sql.SqlServerSystemShape.IsSystemDatabase(database)){return "Japanese_CI_AS";}
        return this.Resolve(database).DefaultCollation;
    }
    public DatabaseRegistration Resolve(string name){
        if(this.databases.TryGetValue(name,out var database)){return this.GatewayTransactions?.ResolveGeneration(database)??database;}
        if(name.Equals("master",StringComparison.OrdinalIgnoreCase)){return new("master",new MasterAdapter(),true,"",[],[]);}
        throw new SqlQueryException($"Cannot open database '{name}'.",4060);
    }
    internal void Publish(IReadOnlyList<DatabaseRegistration> replacements){
        lock(this.publicationGate){var next=new Dictionary<string,DatabaseRegistration>(this.databases,StringComparer.OrdinalIgnoreCase);foreach(var replacement in replacements){if(!next.ContainsKey(replacement.Name)){throw new InvalidOperationException("A generation publication cannot add a database.");}next[replacement.Name]=replacement;}Volatile.Write(ref this.databases,next);}if(this.GatewayTransactions?.IsVersioned!=true){this.ReleaseObsoletePlans();}
    }
    public SqlTable? FindTable(SqlObjectName name){
        if(name.Name.StartsWith('#')){
            // 一時表は現在の接続のものだけを見る。行は実行のたびにその時点の内容を返す。
            var temporary=sqlSession.Value?.TemporaryTables.GetValueOrDefault(name.Name);
            if(temporary is null){return null;}
            return new(name,temporary.Columns,_=>temporary.Rows.ToArray());
        }
        this.RejectForeignDatabaseTransaction(name.Database);
        if(name.Schema.Equals("sys",StringComparison.OrdinalIgnoreCase)&&name.Name.Equals("databases",StringComparison.OrdinalIgnoreCase)){
            var reference=this.databases.Values.FirstOrDefault();if(reference is null){return null;}if(!SystemCatalogEmulator.TryEmulate("SELECT * FROM sys.databases",reference.Adapter,out var shape)){return null;}
            return new(name,shape.Columns.Select(c=>new SqlColumn(c.Name,new SqlType(c.SqlTypeName,c.ClrType,c.MaximumLength,c.Precision,c.Scale,c.Collation),c.IsNullable,name,c.Ordinal)).ToArray(),_=>DatabaseRows());
        }
        if(name.Database.Equals("master",StringComparison.OrdinalIgnoreCase)&&name.Schema.Equals("sys",StringComparison.OrdinalIgnoreCase)&&name.Name.Equals("database_mirroring",StringComparison.OrdinalIgnoreCase)){
            var mirroring=SqlServerDatabaseOptionCatalog.Mirroring();
            return new(name,mirroring.Columns.Select(c=>new SqlColumn(c.Name,new SqlType(c.SqlTypeName,c.ClrType,c.MaximumLength,c.Precision,c.Scale,c.Collation),c.IsNullable,name,c.Ordinal)).ToArray(),_=>mirroring.Rows);
        }
        if(!this.databases.ContainsKey(name.Database)&&SqlServerSystemShape.IsSystemDatabase(name.Database)){
            // master・msdb などのシステムデータベースは値を持たないので、本物と同じ形の0行を返す。
            var systemObject=SqlServerSystemShape.Find(name);
            if(systemObject is not null){return SqlServerSystemShape.Empty(name,systemObject);}
        }
        if(!this.databases.TryGetValue(name.Database,out var registration)){return null;}
        registration=this.GatewayTransactions?.ResolveGeneration(registration)??registration;
        using var read=ReadSqlDatabase(registration.Adapter);
        var isSys=name.Schema.Equals("sys",StringComparison.OrdinalIgnoreCase);
        if((isSys||name.Schema.Equals("seamless",StringComparison.OrdinalIgnoreCase)&&name.Name.Equals("generated_methods",StringComparison.OrdinalIgnoreCase))&&SystemCatalogEmulator.TryCatalog(name.Name,registration.Adapter,registration.Views,registration.Routines,out var catalog,registration.Name)){
            if(isSys){
                // Seamless が値を持つ列だけを入れ、本物の列は全部そろえる。
                var shaped=SqlServerSystemShape.Find(name);
                if(shaped is not null){return SqlServerSystemShape.Widen(name,shaped,catalog);}
            }
            return new(name,catalog.Columns.Select(c=>new SqlColumn(c.Name,new SqlType(c.SqlTypeName,c.ClrType,c.MaximumLength,c.Precision,c.Scale,c.Collation),c.IsNullable,name,c.Ordinal)).ToArray(),_=>catalog.Rows);
        }
        if(isSys){
            // Seamless が持たない機能のシステムビューは0行で返す。
            var missing=SqlServerSystemShape.Find(name);
            if(missing is not null){return SqlServerSystemShape.Empty(name,missing);}
        }
        var view=FindView(registration,name);
        if(view is not null){return BindView(registration,view);}
        return FindPhysicalTable(registration,name);
    }

    internal SqlTable? FindAuthorizedTable(SqlObjectName name,Action<string,IDatabaseAdapter,TableMetadata> demand,Action<IDatabaseAdapter,SqlObjectName> demandView){
        if(name.Name.StartsWith('#')||name.Schema.Equals("sys",StringComparison.OrdinalIgnoreCase)
            ||name.Schema.Equals("seamless",StringComparison.OrdinalIgnoreCase)&&name.Name.Equals("generated_methods",StringComparison.OrdinalIgnoreCase)){
            return null;
        }
        this.RejectForeignDatabaseTransaction(name.Database);
        if(!this.databases.TryGetValue(name.Database,out var registration)){return null;}
        registration=this.GatewayTransactions?.ResolveGeneration(registration)??registration;
        using var read=ReadSqlDatabase(registration.Adapter);
        if(FindView(registration,name) is{} view){
            var table=BindView(registration,view);demandView(registration.Adapter,table.Name);return table;
        }
        return FindPhysicalTable(registration,name,demand);
    }

    private static DatabaseViewDefinition? FindView(DatabaseRegistration registration,SqlObjectName name)=>registration.Views.FirstOrDefault(view=>string.Equals(view.Schema,name.Schema,StringComparison.OrdinalIgnoreCase)&&string.Equals(view.Name,name.Name,StringComparison.OrdinalIgnoreCase));
    private static SqlTable BindView(DatabaseRegistration registration,DatabaseViewDefinition view)=>new(new(registration.Name,view.Schema,view.Name),view.Columns,null,view.Definition);

    internal SqlRoutine? FindAuthorizedScalarFunction(SqlObjectName name,Action<IDatabaseAdapter,SqlObjectName> demand){
        var routine=this.FindAuthorizedSqlRoutine(name,RoutineKind.ScalarFunction,demand);
        if(routine is null){return null;}
        return new(routine.Value.Name,routine.Value.Definition.Definition);
    }

    internal (SqlObjectName Name,DatabaseRoutineDefinition Definition)? FindAuthorizedSqlRoutine(SqlObjectName name,RoutineKind kind,Action<IDatabaseAdapter,SqlObjectName> demand){
        if(name.Schema.Equals("sys",StringComparison.OrdinalIgnoreCase)){return null;}
        this.RejectForeignDatabaseTransaction(name.Database);
        if(!this.databases.TryGetValue(name.Database,out var registration)){return null;}
        registration=this.GatewayTransactions?.ResolveGeneration(registration)??registration;
        using var read=ReadSqlDatabase(registration.Adapter);
        if(FindRegisteredRoutine(registration,name) is not{Catalog:{} catalog,Definition:not null,Native:null,GeneratedMethod:null} routine
            ||catalog.Kind!=kind||catalog.HasMutation&&kind!=RoutineKind.StoredProcedure){return null;}
        var resolved=new SqlObjectName(registration.Name,routine.Schema,routine.Name);demand(registration.Adapter,resolved);
        return(resolved,routine);
    }

    private static DatabaseRoutineDefinition? FindRegisteredRoutine(DatabaseRegistration registration,SqlObjectName name)=>registration.Routines.FirstOrDefault(routine=>string.Equals(routine.Schema,name.Schema,StringComparison.OrdinalIgnoreCase)&&string.Equals(routine.Name,name.Name,StringComparison.OrdinalIgnoreCase));

    private static SqlTable? FindPhysicalTable(DatabaseRegistration registration,SqlObjectName name,Action<string,IDatabaseAdapter,TableMetadata>? demand=null){
        var identifier=new TableMetadata(name.Schema,name.Name,[]).IdentifierKey;
        var table=registration.Adapter.FindTable(registration.Adapter is InProcessDatabaseAdapter?identifier:name.Schema+"."+name.Name);
        if(table is null){return null;}
        demand?.Invoke(registration.Name,registration.Adapter,table);
        var columns=table.Columns.Select(c=>new SqlColumn(c.Name,new SqlType(c.SqlTypeName,c.ClrType,c.MaximumLength,c.Precision,c.Scale,c.Collation),c.IsNullable,name,c.Ordinal)).ToArray();
        return new(new(registration.Name,table.SchemaName,table.TableName),columns,Read);
        IEnumerable<object?[]> Read(CancellationToken cancellation){
            SessionContext? ownedSession=null;var session=sqlSession.Value;
            if(session is null){ownedSession=new(0,registration.Name){CancellationToken=cancellation};session=ownedSession;}
            using var owned=ownedSession;
            using var read=ReadSqlDatabase(registration.Adapter,session);
            foreach(var row in registration.Adapter.ExecuteSelect(registration.Adapter is InProcessDatabaseAdapter?table.IdentifierKey:table.FullName,null,null,session).Rows){cancellation.ThrowIfCancellationRequested();yield return row;}
        }
    }
    private IEnumerable<object?[]> DatabaseRows(){
        yield return SqlServerDatabaseOptionCatalog.Expand(["master",1,"Japanese_CI_AS",(byte)0,"ONLINE",SqlServerVersion.CompatibilityLevel,false,(byte)0,"MULTI_USER",true,(byte)1,"FULL",0,"DATABASE_DEFAULT",(byte)0,"NONE",false,null]);
        var id=5;foreach(var database in this.databases.Values){yield return SqlServerDatabaseOptionCatalog.Expand([database.Name,id++,database.DefaultCollation??"Japanese_CI_AS",(byte)0,"ONLINE",SqlServerVersion.CompatibilityLevel,database.IsReadOnly,(byte)0,"MULTI_USER",true,(byte)1,"FULL",0,"DATABASE_DEFAULT",(byte)0,"NONE",false,null]);}
    }
    public SqlRoutine? FindRoutine(SqlObjectName name){
        this.RejectForeignDatabaseTransaction(name.Database);
        if(name.Schema.Equals("sys",StringComparison.OrdinalIgnoreCase)&&name.Name.Equals("SUSER_SNAME",StringComparison.OrdinalIgnoreCase)){
            return new(name,null,new([new("@sid",new("varbinary",typeof(byte[]),85),true)],new("nvarchar",typeof(string),128),true,arguments=>{
                if(arguments[0] is byte[] sid&&sid.AsSpan().SequenceEqual(new byte[]{1})){return "sa";}return null;
            }));
        }
        if(name.Schema.Equals("sys",StringComparison.OrdinalIgnoreCase)&&name.Name.ToUpperInvariant() is "SUSER_SID" or "SID_BINARY"){
            // Windows のアカウントやグループを解決する機能は持たないので、本物で該当が無いときと同じく NULL を返す。
            IReadOnlyList<SqlParameter> parameters=[new("@login",new("nvarchar",typeof(string),128),true)];
            if(name.Name.Equals("SUSER_SID",StringComparison.OrdinalIgnoreCase)){parameters=[..parameters,new("@param",SqlType.Int,true)];}
            return new(name,null,new(parameters,new("varbinary",typeof(byte[]),85),true,_=>null));
        }
        if(name.Schema.Equals("sys",StringComparison.OrdinalIgnoreCase)&&name.Name.Equals("USER_NAME",StringComparison.OrdinalIgnoreCase)){
            // 利用者は所有者(dbo, principal_id 1)だけを持つ。sys.database_principals と同じ番号で引く。
            return new(name,null,new([new("@id",SqlType.Int,true)],new("nvarchar",typeof(string),128),true,arguments=>{
                if(arguments[0] is not int id){return null;}
                if(id==1){return "dbo";}
                return null;
            }));
        }
        if(name.Schema.Equals("sys",StringComparison.OrdinalIgnoreCase)&&name.Name.Equals("LOGINPROPERTY",StringComparison.OrdinalIgnoreCase)){
            // パスワードの方針(期限・ロック)は持たないので、本物で対象外のときと同じく NULL を返す。
            return new(name,null,new([new("@login",new("nvarchar",typeof(string),128),true),new("@property",new("nvarchar",typeof(string),128),true)],SqlType.Int,true,_=>null));
        }
        if(name.Schema.Equals("sys",StringComparison.OrdinalIgnoreCase)&&name.Name.Equals("HAS_PERMS_BY_NAME",StringComparison.OrdinalIgnoreCase)){
            // 接続は sa だけなので、本物の sysadmin と同じく権限ありの 1 を返す。対象名が NULL なら NULL。
            return new(name,null,new([new("@securable",new("nvarchar",typeof(string),-1),true),new("@class",new("nvarchar",typeof(string),60),true),new("@permission",new("nvarchar",typeof(string),60),true)],SqlType.Int,true,arguments=>{
                if(arguments[1] is null||arguments[2] is null){return null;}
                return 1;
            }));
        }
        if(name.Database.Equals("msdb",StringComparison.OrdinalIgnoreCase)&&name.Schema.Equals("dbo",StringComparison.OrdinalIgnoreCase)&&name.Name.Equals("fn_syspolicy_is_automation_enabled",StringComparison.OrdinalIgnoreCase)){
            // ポリシー管理は提供しないので、自動化は無効の 0。
            return new(name,null,new([],SqlType.Int,true,_=>0));
        }
        if(name.Schema.Equals("sys",StringComparison.OrdinalIgnoreCase)&&name.Name.Equals("HAS_DBACCESS",StringComparison.OrdinalIgnoreCase)){
            return new(name,null,new([new("@database",new("nvarchar",typeof(string),128),true)],SqlType.Int,true,arguments=>{
                if(arguments[0] is null){return null;}var database=(string)arguments[0]!;if(database.Equals("master",StringComparison.OrdinalIgnoreCase)||this.databases.ContainsKey(database)){return 1;}
                // SSMSは接続時にmsdbが1でないと「ポリシーの正常性状態を表示できません」を出す。msdb管理機能自体は未提供だが、能力照会は1を返して通知を避ける。
                if(database.Equals("msdb",StringComparison.OrdinalIgnoreCase)){return 1;}return null;
            }));
        }
        if(name.Schema.Equals("sys",StringComparison.OrdinalIgnoreCase)&&name.Name.Equals("COLLATIONPROPERTY",StringComparison.OrdinalIgnoreCase)){
            return new(name,null,new([new("@collation",new("nvarchar",typeof(string),128),true),new("@property",new("nvarchar",typeof(string),128),true)],SqlType.Int,true,arguments=>{
                if(arguments[0] is not string collation||arguments[1] is not string property){return null;}
                var shape=collation.ToUpperInvariant() switch{
                    "JAPANESE_CI_AS"=>(Lcid:1041,Style:196609),
                    "SQL_LATIN1_GENERAL_CP1_CI_AS" or "LATIN1_GENERAL_100_CI_AS"=>(Lcid:1033,Style:196609),
                    "LATIN1_GENERAL_BIN2" or "LATIN1_GENERAL_100_BIN2" or "LATIN1_GENERAL_100_BIN2_UTF8"=>(Lcid:1033,Style:0),
                    _=>throw new SqlQueryException("Unsupported collation: "+collation,40515)};
                return property.ToUpperInvariant() switch{"LCID"=>shape.Lcid,"COMPARISONSTYLE"=>shape.Style,_=>throw new SqlQueryException("Unsupported COLLATIONPROPERTY property: "+property,40515)};
            }));
        }
        if(name.Schema.Equals("sys",StringComparison.OrdinalIgnoreCase)&&name.Name.Equals("QUOTENAME",StringComparison.OrdinalIgnoreCase)){
            var textType=new SqlType("nvarchar",typeof(string),-1);
            return new(name,null,new([new("@value",textType,true),new("@delimiter",textType,true)],new("nvarchar",typeof(string),258),true,arguments=>{
                if(arguments[0] is not string value||arguments[1] is not string delimiter||value.Length>128||delimiter.Length!=1){return null;}
                char left;char right;
                switch(delimiter[0]){
                    case '[':case ']':left='[';right=']';break;
                    case '(':case ')':left='(';right=')';break;
                    case '{':case '}':left='{';right='}';break;
                    case '<':case '>':left='<';right='>';break;
                    case '\'':left='\'';right='\'';break;
                    case '"':left='"';right='"';break;
                    case '`':left='`';right='`';break;
                    default:return null;
                }
                return left+value.Replace(right.ToString(),new string(right,2),StringComparison.Ordinal)+right;
            }));
        }
        if(!this.databases.TryGetValue(name.Database,out var registration)){return null;}
        registration=this.GatewayTransactions?.ResolveGeneration(registration)??registration;
        using var read=ReadSqlDatabase(registration.Adapter);
        if(name.Schema.Equals("sys",StringComparison.OrdinalIgnoreCase)&&(name.Name.Equals("OBJECTPROPERTYEX",StringComparison.OrdinalIgnoreCase)||name.Name.Equals("OBJECTPROPERTY",StringComparison.OrdinalIgnoreCase))){
            return new(name,null,new([new("@object_id",SqlType.Int,true),new("@property",new("nvarchar",typeof(string),-1),true)],SqlType.Int,true,arguments=>{
                if(arguments[0] is not int id||arguments[1] is not string property){return null;}
                // 本物のSQL Serverは、未知または対象外のプロパティ名にはNULLを返す(エラーにしない)。実機で確認済み。
                if(!property.Equals("IsSchemaBound",StringComparison.OrdinalIgnoreCase)&&!property.Equals("OwnerId",StringComparison.OrdinalIgnoreCase)){return null;}
                using var catalogRead=ReadSqlDatabase(registration.Adapter);
                SystemCatalogEmulator.TryCatalog("objects",registration.Adapter,registration.Views,registration.Routines,out var objects);
                var found=objects.Rows.FirstOrDefault(row=>Equals(row[1],id));if(found is null){return null;}
                if(property.Equals("OwnerId",StringComparison.OrdinalIgnoreCase)){return found[15];}
                SystemCatalogEmulator.TryCatalog("sql_modules",registration.Adapter,registration.Views,registration.Routines,out var modules);
                var ordinal=Array.FindIndex(modules.Columns.ToArray(),column=>column.Name.Equals("is_schema_bound",StringComparison.OrdinalIgnoreCase));
                if(ordinal<0){throw new SqlQueryException("sys.sql_modules.is_schema_bound is unavailable.",40515);}
                var module=modules.Rows.FirstOrDefault(row=>Equals(row[0],id));
                if(module is null){return 0;}
                if(module[ordinal] is null){return null;}
                return Convert.ToInt32(module[ordinal],global::System.Globalization.CultureInfo.InvariantCulture);
            }));
        }
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
        var routine=FindRegisteredRoutine(registration,name);
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
public sealed record DatabaseViewDefinition(string Schema,string Name,string Definition,IReadOnlyList<SqlColumn> Columns,IReadOnlyList<ColumnCatalog>? SourceColumns=null){public string? OwnerName{get;init;}}
public sealed record DatabaseRoutineDefinition(string Schema,string Name,string? Definition,RoutineCatalog? Catalog=null,GeneratedRoutineBinding? GeneratedMethod=null,SqlNativeRoutine? Native=null);
