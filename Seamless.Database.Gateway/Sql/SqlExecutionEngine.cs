using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Sql;

public sealed class SqlExecutionEngine{
    private readonly DatabaseRegistry registry;
    private readonly SqlQueryEngine queries;
    public SqlExecutionEngine(IDatabaseAdapter adapter):this(CreateRegistry(adapter)){}
    public SqlExecutionEngine(DatabaseRegistry registry){this.registry=registry;this.queries=new(registry);}
    public DatabaseRegistry Databases=>this.registry;
    public IReadOnlyList<QueryResult> ExecuteBatch(string sql,SessionContext session,IReadOnlyList<SqlParameter>? parameters=null,IReadOnlyDictionary<string,object?>? values=null){
        if(string.IsNullOrWhiteSpace(sql)){return [QueryResult.Empty];}
        // 既知のSSMSバッチを全トークンで照合し、利用者の複文はAST境界で分割する。
        var parser=new TSql160Parser(true);
        var script=parser.Parse(new StringReader(sql),out var errors) as TSqlScript;
        if(errors.Count>0){throw new SqlQueryException(errors[0].Message,102,errors[0].Line);}
        if(script is null){throw new SqlQueryException("SQL script was not parsed.",102);}
        if(SqlServerConnectionProbes.IsConnectionInitialization(script)&&SystemCatalogEmulator.TryEmulateBatch(sql,this.Resolve(session).Adapter,out var initialization)){return initialization;}
        if(SqlServerConnectionProbes.IsServerInformation(script)){
            return [QueryResult.Defer(()=>{
                if(!SystemCatalogEmulator.TryEmulate(sql,this.Resolve(session).Adapter,out var information)){throw new InvalidOperationException("Known server-information profile was not evaluated.");}
                session.ServerInformationCleanupAvailable=true;return information;
            })];
        }
        if(SqlServerConnectionProbes.TryContainedAuthentication(script,out var authenticationDatabase)){
            return [QueryResult.Defer(()=>{
                _=this.Execute("USE ["+authenticationDatabase.Replace("]","]]")+"]",session,parameters,values);
                // 現行ゲートウェイはサーバーログインのみを扱い、包含DB認証は提供しない。
                return new QueryResult([new ColumnMetadata("",typeof(int),"int",false,false,0)],[new object?[]{0}],1);
            })];
        }
        var results=new List<QueryResult>();
        foreach(var batch in script.Batches){
            foreach(var statement in batch.Statements){var text=sql.Substring(statement.StartOffset,statement.FragmentLength);results.Add(QueryResult.Defer(()=>this.Execute(text,session,parameters,values)));}
        }
        if(results.Count==0){results.Add(QueryResult.Empty);}
        return results;
    }
    public QueryResult Execute(string sql,SessionContext session,IReadOnlyList<SqlParameter>? parameters=null,IReadOnlyDictionary<string,object?>? values=null){
        return session.Run(()=>{using var context=DatabaseRegistry.EnterSqlSession(session);return this.ExecuteCore(sql,session,parameters,values);});
    }
    private QueryResult ExecuteCore(string sql,SessionContext session,IReadOnlyList<SqlParameter>? parameters,IReadOnlyDictionary<string,object?>? values){
        if(string.IsNullOrWhiteSpace(sql)){return QueryResult.Empty;}
        var parser=new TSql160Parser(true);
        var fragment=parser.Parse(new StringReader(sql),out var errors);
        if(errors.Count>0){throw new SqlQueryException(errors[0].Message,102,errors[0].Line);}
        if(fragment is not TSqlScript parsed||parsed.Batches.Sum(b=>b.Statements.Count)!=1){throw new SqlQueryException("Use ExecuteBatch for multiple statements.");}
        var statement=parsed.Batches.SelectMany(b=>b.Statements).Single();
        if(SqlServerSessionSettings.TryExecute(statement,session,out var settings)){return settings;}
        if(session.ServerInformationCleanupAvailable&&SqlServerConnectionProbes.IsServerInformationCleanup(statement)){session.ServerInformationCleanupAvailable=false;return QueryResult.Empty;}
        if(statement is UseStatement use){
            if(session.IsInTransaction){throw new SqlQueryException("Finish the transaction before changing databases.");}
            var database=this.registry.Resolve(use.DatabaseName.Value);
            session.DatabaseName=database.Name;
            return QueryResult.Empty;
        }
        var current=this.Resolve(session);
        if(statement is SelectStatement){
            if(TryDatabaseName(sql,session,out var databaseName)){return databaseName;}
            using var read=DatabaseRegistry.ReadSqlDatabase(current.Adapter,session);

            // 既存SSMS/SMO専用カタログを先に扱う。一般の表/ビューSELECTは共通エンジンへ渡す。
            if(!HasDatabaseCatalog(statement)&&!HasRelationalCatalog(statement)&&IsSystemQuery(statement)&&SystemCatalogEmulator.TryEmulate(sql,current.Adapter,out var system)){return system;}
            var result=this.queries.Prepare(sql,current.Name,parameters).Execute(values,new Seamless.Execution.ExecutionOptions{CancellationToken=session.CancellationToken});
            return new(result.Columns.Select((c,i)=>new ColumnMetadata(c.Name,c.Type.ClrType,c.Type.Name,c.IsNullable,false,i,c.Type.Length,c.Type.Precision,c.Type.Scale,c.Type.Collation,sourceDatabase:c.Source?.Database??session.DatabaseName,sourceSchema:c.Source?.Schema,sourceTable:c.Source?.Name)).ToArray(),session.Enumerate(this.registry.EnumerateSql(session,result.Rows)),-1);
        }
        if(statement is PrintStatement print){
            using var read=DatabaseRegistry.ReadSqlDatabase(current.Adapter,session);
            var expression=sql.Substring(print.Expression.StartOffset,print.Expression.FragmentLength);
            var original=this.queries.Prepare("SELECT "+expression,current.Name,parameters);var type=original.Columns.Single().Type;
            var target=type.Name.ToLowerInvariant() switch{"nvarchar" or "nchar" or "ntext"=>"nvarchar(4000)","varchar" or "char" or "text" or "tinyint" or "smallint" or "int" or "bigint" or "decimal" or "numeric"=>"varchar(8000)",_=>null};
            if(target is null&&print.Expression is NullLiteral){target="nvarchar(4000)";}
            if(target is null){throw new SqlQueryException("PRINT supports character, integer, decimal, and NULL expressions only.",40515);}
            var evaluated=this.queries.Prepare("SELECT CAST(("+expression+") AS "+target+")",current.Name,parameters).Execute(values,new Seamless.Execution.ExecutionOptions{CancellationToken=session.CancellationToken});
            using var iterator=session.Enumerate(evaluated.Rows).GetEnumerator();if(!iterator.MoveNext()){throw new SqlQueryException("PRINT expression produced no value.",40515);}
            var value=iterator.Current[0];if(iterator.MoveNext()){throw new SqlQueryException("PRINT expression produced multiple values.",40515);}
            if(value is not null&&value is not string){throw new SqlQueryException("PRINT expression is not a string.",40515);}
            var message=(string?)value;if(string.IsNullOrEmpty(message)){message=" ";}return QueryResult.Message(message);
        }
        if(statement is SetRowCountStatement rowCount){
            // このprofileは常に行数制限なし。未対応の制限値を黙って無視しない。
            if(rowCount.NumberRows is IntegerLiteral limit&&int.TryParse(limit.Value,global::System.Globalization.NumberStyles.None,global::System.Globalization.CultureInfo.InvariantCulture,out var count)&&count==0){return QueryResult.Empty;}
            throw new SqlQueryException("SET ROWCOUNT supports only the unlimited value 0. Use TOP to limit query results.",40515);
        }
        if(statement is SetOnOffStatement or PredicateSetStatement or SetTransactionIsolationLevelStatement){return QueryResult.Empty;}
        if(statement is BeginTransactionStatement){this.EnsureWritable(current);current.Adapter.BeginTransaction(session);session.LastTransactionOperation=8;return QueryResult.NonQuery(0);}
        if(statement is CommitTransactionStatement){current.Adapter.CommitTransaction(session);session.LastTransactionOperation=9;return QueryResult.NonQuery(0);}
        if(statement is RollbackTransactionStatement){current.Adapter.RollbackTransaction(session);session.LastTransactionOperation=10;return QueryResult.NonQuery(0);}
        if(statement is CreateTableStatement or AlterTableStatement or DropTableStatement){
            this.EnsureWritable(current);
            if(current.Adapter is not InProcessDatabaseAdapter adapter||adapter.Database is not RuntimeSqlDatabaseContainer){throw new SqlQueryException("Runtime SQL DDL is not enabled for this database.",40515);}
            var ddl=SqlDdlParser.Parse(sql);var implicitTransaction=!session.IsInTransaction;
            if(implicitTransaction){adapter.BeginTransaction(session);}
            try{adapter.ExecuteDdl(ddl);if(implicitTransaction){adapter.CommitTransaction(session);}return QueryResult.NonQuery(0);}
            catch{if(implicitTransaction&&session.IsInTransaction){adapter.RollbackTransaction(session);}throw;}
        }
        if(statement is InsertStatement or UpdateStatement or DeleteStatement){
            this.EnsureWritable(current);
            var implicitTransaction=!session.IsInTransaction;var executing=false;
            if(implicitTransaction){current.Adapter.BeginTransaction(session);}
            try{
                var command=ScriptDomParser.Parse(sql,current.Adapter,values);executing=true;
                var count=0;if(statement is InsertStatement){foreach(var row in command.InsertRows){count=checked(count+current.Adapter.ExecuteInsert(command.TableName,row,session));}}
                else if(statement is UpdateStatement){count=current.Adapter.ExecuteUpdateExpressions(command.TableName,command.UpdateValues,command.Predicate,session);}
                else{count=current.Adapter.ExecuteDelete(command.TableName,command.Predicate,session);}
                if(implicitTransaction){current.Adapter.CommitTransaction(session);}return QueryResult.NonQuery(count);
            }catch{if(implicitTransaction&&session.IsInTransaction){current.Adapter.RollbackTransaction(session);}else if(executing&&session.IsInTransaction&&current.Adapter is InProcessDatabaseAdapter failed){failed.MarkTransactionFailed();}throw;}

        }
        throw new SqlQueryException($"SQL statement '{statement?.GetType().Name??"unknown"}' is not supported.",40515);
    }
    private DatabaseRegistration Resolve(SessionContext session)=>this.registry.Resolve(session.DatabaseName);
    private void EnsureWritable(DatabaseRegistration database){if(database.IsReadOnly){throw new SqlQueryException($"Database '{database.Name}' is read only.",3906);}}
    private QueryResult DatabaseList(){
        var rows=new List<object?[]>{new object?[]{"master",1,"Japanese_CI_AS",(byte)0,"ONLINE",(byte)160,false,(byte)0,"MULTI_USER",true,(byte)1,"FULL"}};
        var id=5;
        foreach(var db in this.registry.Databases){rows.Add([db.Name,id++,"Japanese_CI_AS",(byte)0,"ONLINE",(byte)160,db.IsReadOnly,(byte)0,"MULTI_USER",true,(byte)1,"FULL"]);}
        var reference=this.registry.Databases.First();
        if(!SystemCatalogEmulator.TryEmulate("SELECT * FROM sys.databases",reference.Adapter,out var shape)){throw new InvalidOperationException("System catalog shape is unavailable.");}
        return new(shape.Columns,rows,rows.Count);
    }
    private static bool TryDatabaseName(string sql,SessionContext session,out QueryResult result){
        if(string.Equals(sql.Trim().TrimEnd(';').Trim(),"SELECT DB_NAME()",StringComparison.OrdinalIgnoreCase)){
            result=new([new ColumnMetadata("",typeof(string),"nvarchar",false,false,0)],[new object?[]{session.DatabaseName}],1);return true;
        }
        result=QueryResult.Empty;return false;
    }
    private static bool HasRelationalCatalog(TSqlStatement statement){var visitor=new RelationalCatalogVisitor();statement.Accept(visitor);return visitor.Found;}
    private sealed class RelationalCatalogVisitor:TSqlFragmentVisitor{
        public bool Found{get;private set;}
        public override void ExplicitVisit(NamedTableReference node){
            if(string.Equals(node.SchemaObject.SchemaIdentifier?.Value,"sys",StringComparison.OrdinalIgnoreCase)&&node.SchemaObject.BaseIdentifier.Value.ToLowerInvariant() is "schemas" or "tables" or "views" or "objects" or "columns" or "types" or "indexes" or "index_columns" or "procedures" or "sql_modules" or "parameters" or "all_objects" or "all_views" or "all_columns"){this.Found=true;}
        }

    }
    private static bool HasDatabaseCatalog(TSqlStatement statement){var visitor=new DatabaseCatalogVisitor();statement.Accept(visitor);return visitor.Found;}
    private sealed class DatabaseCatalogVisitor:TSqlFragmentVisitor{public bool Found{get;private set;}public override void ExplicitVisit(NamedTableReference node){if(string.Equals(node.SchemaObject.SchemaIdentifier?.Value,"sys",StringComparison.OrdinalIgnoreCase)&&string.Equals(node.SchemaObject.BaseIdentifier.Value,"databases",StringComparison.OrdinalIgnoreCase)){this.Found=true;}}}
    private static bool IsSystemQuery(TSqlStatement statement){var visitor=new SystemVisitor();statement.Accept(visitor);return visitor.Found;}
    private static DatabaseRegistry CreateRegistry(IDatabaseAdapter adapter){var registry=new DatabaseRegistry();registry.Register(adapter.DatabaseName,adapter);return registry;}
    private sealed class SystemVisitor:TSqlFragmentVisitor{
        public bool Found{get;private set;}
        public override void ExplicitVisit(NamedTableReference node){
            if(string.Equals(node.SchemaObject.SchemaIdentifier?.Value,"sys",StringComparison.OrdinalIgnoreCase)||node.SchemaObject.BaseIdentifier.Value.StartsWith("#",StringComparison.Ordinal)){this.Found=true;}
        }
        public override void ExplicitVisit(FunctionCall node){
            if(node.FunctionName.Value.ToUpperInvariant() is "SERVERPROPERTY" or "CONNECTIONPROPERTY" or "DATABASEPROPERTYEX" or "SUSER_SNAME" or "SUSER_SID"){this.Found=true;}
            base.ExplicitVisit(node);
        }
    }
}
