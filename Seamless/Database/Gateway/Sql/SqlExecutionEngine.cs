using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Http;
using Seamless.Database.Gateway.Tds;
using Seamless.Database.Query;
using Seamless.Execution;

namespace Seamless.Database.Gateway.Sql;

public sealed partial class SqlExecutionEngine{
    private const int MaximumProcedureStatements=64;
    private readonly DatabaseRegistry registry;
    private readonly SqlQueryEngine queries;
    public SqlExecutionEngine(IDatabaseAdapter adapter,SqlSchemaResolution resolution=SqlSchemaResolution.SqlServer):this(CreateRegistry(adapter,resolution)){}
    public SqlExecutionEngine(DatabaseRegistry registry){this.registry=registry;this.queries=new(registry);}
    public DatabaseRegistry Databases=>this.registry;
    private Seamless.Execution.ExecutionOptions QueryOptions(SessionContext session)=>this.registry.GatewayTransactions?.QueryExecutionOptions(session.CancellationToken,session.StatementTimestamp)??new(){CancellationToken=session.CancellationToken,StatementTimestamp=session.StatementTimestamp};
    public IReadOnlyList<QueryResult> ExecuteBatch(string sql,SessionContext session,IReadOnlyList<SqlParameter>? parameters=null,IReadOnlyDictionary<string,object?>? values=null){
        if(string.IsNullOrWhiteSpace(sql)){return [QueryResult.Empty];}
        // 既知のSSMSバッチを全トークンで照合し、利用者の複文はAST境界で分割する。
        var parser=new TSql160Parser(true);
        var script=parser.Parse(new StringReader(sql),out var errors) as TSqlScript;
        if(errors.Count>0){throw new SqlQueryException(errors[0].Message,102,errors[0].Line);}
        if(script is null){throw new SqlQueryException("SQL script was not parsed.",102);}
        if(SqlServerFileTableProfile.TryBatch(script,sql,out var databaseUse,out var fileTableSelect)){
            return [QueryResult.Defer(()=>{if(databaseUse is not null){_=this.Execute(databaseUse,session,parameters,values);}return this.Execute(fileTableSelect,session,parameters,values);})];
        }
        if(SqlServerDatabaseAvailabilityProfile.IsPrefix(script)){
            return [QueryResult.Defer(()=>{session.DatabaseAvailabilityAvailable=true;session.DatabaseAvailabilityCleanupOrdinal=0;return QueryResult.Empty;})];
        }
        if(SqlServerDatabaseAvailabilityProfile.TryBatch(script,sql,out var availabilitySelect)){
            return [QueryResult.Defer(()=>this.Execute(availabilitySelect,session,parameters,values))];
        }
        if(SqlServerDatabaseQueryProfile.IsPrefix(script)){
            return [QueryResult.Defer(()=>{session.DatabaseOptionRetentionAvailable=true;return QueryResult.Empty;})];
        }
        if(SqlServerDatabaseQueryProfile.TryBatch(script,sql,out var optionSelect)){
            return [QueryResult.Defer(()=>this.Execute(optionSelect,session,parameters,values))];
        }
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
        // 変数・制御文・一時表を含むバッチは、文ごとに順に実行する。
        if(SqlBatchInterpreter.Needs(script)){return new SqlBatchInterpreter(this,sql,session,parameters,values).Run(script);}
        var results=new List<QueryResult>();
        foreach(var batch in script.Batches){
            foreach(var statement in batch.Statements){var text=sql.Substring(statement.StartOffset,statement.FragmentLength);results.Add(QueryResult.Defer(()=>this.Execute(text,session,parameters,values)));}
        }
        if(results.Count==0){results.Add(QueryResult.Empty);}
        return results;
    }
    public QueryResult Execute(string sql,SessionContext session,IReadOnlyList<SqlParameter>? parameters=null,IReadOnlyDictionary<string,object?>? values=null){
        return session.Run(()=>{using var context=DatabaseRegistry.EnterSqlSession(session);if(this.registry.GatewayTransactions is{IsVersioned:true} manager&&session.SnapshotTransaction is null&&IsReadStatement(sql)){return manager.ExecuteReadStatement(session,()=>this.ExecuteCore(sql,session,parameters,values));}return this.ExecuteCore(sql,session,parameters,values);});
    }
    internal static TSqlStatement ParseAuthorizedStatement(string sql)=>ParseAuthorizedStatement(sql,false,out _);
    internal static TSqlStatement ParseAuthorizedStatement(string sql,bool allowMetadataBatch,out bool metadataOnly)
        =>ParseAuthorizedStatement(sql,allowMetadataBatch,out metadataOnly,false,out _);
    internal static TSqlStatement ParseAuthorizedStatement(string sql,bool allowMetadataBatch,out bool metadataOnly,bool allowLocalProcedureBatch,out IList<TSqlStatement>? localBatch){
        if(sql.Length>1048576){throw new SqlQueryException("SQL text exceeds the query limit.",103);}
        var parsed=new TSql170Parser(true).Parse(new StringReader(sql),out var errors) as TSqlScript;
        if(errors.Count>0){throw new SqlQueryException(errors[0].Message,102,errors[0].Line);}
        metadataOnly=false;
        localBatch=null;
        if(parsed?.Batches is [{Statements:[var statement]}]){return statement;}
        if(allowMetadataBatch&&parsed?.Batches is [{Statements:[PredicateSetStatement{IsOn:true,Options:SetOptions.FmtOnly},SelectStatement select,PredicateSetStatement{IsOn:false,Options:SetOptions.FmtOnly}]}]){
            metadataOnly=true;
            return select;
        }
        if(allowLocalProcedureBatch&&parsed?.Batches is [{Statements:{Count:>=2 and <=MaximumProcedureStatements} statements}]
            &&statements[0] is DeclareVariableStatement){
            localBatch=statements;
            return statements[0];
        }
        throw new SqlQueryException("Exactly one SQL statement is required.",102);
    }
    internal QueryResult ExecuteAuthorizedStatement(string sql,TSqlStatement statement,SessionContext session,ISqlCatalog catalog,Action<string,IDatabaseAdapter,TableMetadata,SqlCommandKind> demandMutation,
        IReadOnlyList<SqlParameter>? parameters=null,IReadOnlyDictionary<string,object?>? values=null,bool metadataOnly=false){
        if(metadataOnly&&statement is not SelectStatement){throw new SqlQueryException("Result metadata requires a SELECT statement.",40515);}
        var protectedQueries=new SqlQueryEngine(catalog,cacheCapacity:0);
        return session.Run(()=>{
            using var context=DatabaseRegistry.EnterSqlSession(session);
            if(statement is InsertStatement or UpdateStatement or DeleteStatement){
                return this.ExecuteMutation(statement,sql,session,this.Resolve(session),parameters,values,protectedQueries,demandMutation);
            }
            if(statement is not SelectStatement){throw new SqlQueryException("This statement is unavailable through the protected SQL endpoint.",40515);}
            if(this.registry.GatewayTransactions is{IsVersioned:true} manager&&session.SnapshotTransaction is null){return manager.ExecuteReadStatement(session,Select,metadataOnly);}
            return Select();
        });
        QueryResult Select(){
            var plan=protectedQueries.Prepare(sql,session.DatabaseName,parameters);
            if(metadataOnly){return new(ResultColumns(plan.Columns,session.DatabaseName),[],0);}
            var result=plan.Execute(values,this.QueryOptions(session));
            return this.SelectResult(result,session);
        }
    }

    internal IReadOnlyList<QueryResult> ExecuteAuthorizedProcedure(SqlObjectName name,SessionContext session,HttpAuthorizedSqlCatalog catalog,
        Func<IReadOnlyList<SqlParameter>,IReadOnlySet<string>?,IReadOnlyDictionary<string,object?>> bindArguments,
        Action<IReadOnlyList<SqlParameter>,IReadOnlyDictionary<string,object?>>? publishOutputs=null){
        return session.Run<IReadOnlyList<QueryResult>>(()=>{
            using var context=DatabaseRegistry.EnterSqlSession(session);
            var generation=catalog.Generation;
            using var scope=catalog.EnterProcedure(name,out var routine);
            var definition=routine.Definition!;
            if(ParseAuthorizedStatement(definition) is not ProcedureStatementBody{Options.Count:0,IsForReplication:false,StatementList:{} list} procedure){
                throw UnsupportedProcedure();
            }
            var statements=list.Statements;
            if(statements is [BeginEndBlockStatement{StatementList:{} block}]){statements=block.Statements;}
            if(statements.Count is <1 or >MaximumProcedureStatements){throw UnsupportedProcedure();}
            var mutation=statements.Any(static statement=>statement is InsertStatement or UpdateStatement or DeleteStatement);
            var assignments=statements.Any(IsOutputAssignment);
            if(statements.Any(static statement=>statement is not(SelectStatement or InsertStatement or UpdateStatement or DeleteStatement)&&!IsOutputAssignment(statement))
                ||mutation!=routine.Catalog!.HasMutation){throw UnsupportedProcedure();}
            var catalogParameters=routine.Catalog!.Parameters.OrderBy(parameter=>parameter.Ordinal).ToArray();
            if(catalogParameters.Length!=procedure.Parameters.Count||catalogParameters.Any(parameter=>parameter.IsTableValued)){
                throw UnsupportedProcedure();
            }
            var parameters=new SqlParameter[catalogParameters.Length];
            HashSet<string>? outputNames=null;
            for(var i=0;i<parameters.Length;i++){
                var parameter=catalogParameters[i];
                if(procedure.Parameters[i].Modifier!=(parameter.IsOutput?ParameterModifier.Output:ParameterModifier.None)
                    ||!string.Equals(procedure.Parameters[i].VariableName.Value,parameter.Name,StringComparison.OrdinalIgnoreCase)){throw UnsupportedProcedure();}
                var type=ComparisonHost.ToSqlType(parameter.Type);
                if(parameter.IsOutput){
                    if(!assignments||outputNames?.Count>=MaximumProcedureStatements||!SupportsOutput(type)){throw UnsupportedProcedure();}
                    if(type.ClrType==typeof(string)){type=type with{Collation=type.Collation??catalog.GetDatabaseCollation(session.DatabaseName)};}
                    (outputNames??=new(StringComparer.OrdinalIgnoreCase)).Add(parameter.Name);
                }
                parameters[i]=new(parameter.Name,type,parameter.Type.IsNullable||parameter.IsOutput);
            }
            if(assignments&&outputNames is null){throw UnsupportedProcedure();}
            var values=bindArguments(parameters,outputNames);
            var queries=new SqlQueryEngine(catalog,cacheCapacity:0);
            if(mutation){
                using var identity=session.EnterIdentityScope(new());
                var registration=this.Resolve(session);
                if(statements.Count==1){
                    return [this.ExecuteMutation(statements[0],StatementText(statements[0]),session,registration,parameters,values,queries,catalog.DemandMutation,RequireCurrent)];
                }
                return ExecuteMutationBatch(registration);
            }
            var manager=this.registry.GatewayTransactions;
            if(assignments){
                return manager is not null&&session.SnapshotTransaction is null
                    ?manager.ExecuteReadResults(session,()=>RunBatch(null),materialized:true):RunBatch(null);
            }
            if(statements.Count>1){
                if(manager is not null&&session.SnapshotTransaction is null){return manager.ExecuteReadResults(session,()=>{RequireCurrent();return SelectAll();});}
                return SelectAll();
            }
            if(manager?.IsVersioned==true&&session.SnapshotTransaction is null){
                return [manager.ExecuteReadStatement(session,()=>{RequireCurrent();return Select(statements[0],values);})];
            }
            return [Select(statements[0],values)];

            void AssignOutput(SetVariableStatement statement,Dictionary<string,object?> assigned){
                var target=parameters.FirstOrDefault(parameter=>string.Equals(parameter.Name,statement.Variable.Name,StringComparison.OrdinalIgnoreCase));
                if(target is null||outputNames?.Contains(target.Name)!=true){throw UnsupportedProcedure();}
                var plan=queries.Prepare("SELECT "+definition.Substring(statement.Expression.StartOffset,statement.Expression.FragmentLength),session.DatabaseName,parameters);
                if(catalog.Generation!=generation){throw ObsoleteProcedure();}
                using var row=plan.Execute(assigned,this.QueryOptions(session)).Rows.GetEnumerator();
                if(!row.MoveNext()){throw UnsupportedProcedure();}
                assigned[target.Name]=SqlScalarConversion.Assign(row.Current[0],target.Type);
            }

            QueryResult[] SelectAll()=>statements.Select(statement=>Select(statement,values)).ToArray();
            QueryResult Select(TSqlStatement statement,IReadOnlyDictionary<string,object?> currentValues){
                var plan=queries.Prepare(StatementText(statement),session.DatabaseName,parameters);
                if(catalog.Generation!=generation){throw ObsoleteProcedure();}
                return this.SelectResult(plan.Execute(currentValues,this.QueryOptions(session)),session);
            }
            void RequireCurrent(){
                using var currentScope=catalog.EnterProcedure(name,out var current);
                if(!ReferenceEquals(routine,current)){throw ObsoleteProcedure();}
            }
            string StatementText(TSqlStatement statement)=>definition.Substring(statement.StartOffset,statement.FragmentLength);
            IReadOnlyList<QueryResult> ExecuteMutationBatch(DatabaseRegistration registration){
                this.EnsureWritable(registration);
                var ownsTransaction=!session.IsInTransaction;
                string? savepoint=null;
                if(ownsTransaction){
                    registration.Adapter.BeginTransaction(session);
                }else{
                    savepoint="procedure/"+Guid.NewGuid().ToString("N");
                    session.CreateSavepoint(savepoint);
                }
                try{
                    var results=RunBatch(registration);
                    session.CancellationToken.ThrowIfCancellationRequested();
                    if(ownsTransaction){registration.Adapter.CommitTransaction(session);}
                    else{session.ReleaseSavepoint(savepoint!);}
                    return results;
                }catch{
                    if(ownsTransaction&&session.IsInTransaction){registration.Adapter.RollbackTransaction(session);}
                    else if(savepoint is not null&&session.TransactionState>0){
                        session.RollbackToSavepoint(savepoint);
                        session.ReleaseSavepoint(savepoint);
                    }
                    throw;
                }
            }
            IReadOnlyList<QueryResult> RunBatch(DatabaseRegistration? registration){
                RequireCurrent();
                ExecutionScope? resultStorage=null;
                if(statements.Any(static statement=>statement is SelectStatement)){
                    resultStorage=new(this.QueryOptions(session));
                    session.RetainResults(resultStorage);
                }
                Dictionary<string,object?>? assigned=null;
                IReadOnlyDictionary<string,object?> currentValues=values;
                if(assignments){
                    assigned=new(StringComparer.OrdinalIgnoreCase);
                    foreach(var parameter in parameters){assigned.Add(parameter.Name,SqlScalarConversion.Assign(values[parameter.Name],parameter.Type));}
                    currentValues=assigned;
                }
                var results=new List<QueryResult>(statements.Count);
                foreach(var statement in statements){
                    session.CancellationToken.ThrowIfCancellationRequested();
                    if(statement is SetVariableStatement assignment){AssignOutput(assignment,assigned!);}
                    else if(statement is SelectStatement){
                        var result=Select(statement,currentValues);
                        TdsPacketWriter.ValidateMetadata(result.Columns);
                        results.Add(new(result.Columns,SqlQueryResult.StoreRows(resultStorage!,ValidateRows(result)),result.RowsAffected){StatementKind=SqlCommandKind.Select});
                    }else{
                        results.Add(this.ExecuteMutation(statement,StatementText(statement),session,registration!,parameters,currentValues,queries,catalog.DemandMutation));
                    }
                }
                if(assigned is not null){publishOutputs?.Invoke(parameters,assigned);}
                return results.Count==0?[QueryResult.Empty]:results;
            }
            static IEnumerable<object?[]> ValidateRows(QueryResult result){
                foreach(var row in result.Rows){
                    TdsPacketWriter.ValidateRow(row,result.Columns.Count);
                    yield return row;
                }
            }
        });
        static SqlQueryException UnsupportedProcedure()=>new("This procedure body or parameter declaration is unavailable in protected TDS.",40515);
        static SqlQueryException ObsoleteProcedure()=>new("Catalog generation changed while binding the procedure.",3961);
    }
    private static bool IsOutputAssignment(TSqlStatement statement)=>statement is SetVariableStatement{
        AssignmentKind:AssignmentKind.Equals,FunctionCallExists:false,Expression:not null,Variable:not null};

    private static ColumnMetadata[] ResultColumns(IReadOnlyList<SqlColumn> columns,string database)
        =>columns.Select((c,i)=>new ColumnMetadata(c.Name,c.Type.ClrType,c.Type.Name,c.IsNullable,false,i,c.Type.Length,c.Type.Precision,c.Type.Scale,c.Type.Collation,sourceDatabase:c.Source?.Database??database,sourceSchema:c.Source?.Schema,sourceTable:c.Source?.Name)).ToArray();
    internal static bool SupportsOutput(SqlType type)=>type.Name.ToLowerInvariant() switch{
        "bit" or "tinyint" or "smallint" or "int" or "bigint" or "decimal" or "numeric" or "real" or "float" or "uniqueidentifier"=>true,
        "nvarchar" or "nchar" or "varchar" or "char" or "binary" or "varbinary"=>type.Length is >0 and <=4000,
        _=>false
    };
    private QueryResult SelectResult(SqlQueryResult result,SessionContext session)=>new(
        ResultColumns(result.Columns,session.DatabaseName),
        session.Enumerate(this.registry.EnumerateSql(session,result.Rows)),-1);

    private static bool IsReadStatement(string sql){var script=new TSql160Parser(true).Parse(new StringReader(sql),out var errors) as TSqlScript;return errors.Count==0&&script is not null&&script.Batches.Sum(static batch=>batch.Statements.Count)==1&&script.Batches.SelectMany(static batch=>batch.Statements).Single() is SelectStatement or PrintStatement;}
    private QueryResult ExecuteCore(string sql,SessionContext session,IReadOnlyList<SqlParameter>? parameters,IReadOnlyDictionary<string,object?>? values){
        if(string.IsNullOrWhiteSpace(sql)){return QueryResult.Empty;}
        var parser=new TSql160Parser(true);
        var fragment=parser.Parse(new StringReader(sql),out var errors);
        if(errors.Count>0){throw new SqlQueryException(errors[0].Message,102,errors[0].Line);}
        if(fragment is not TSqlScript parsed||parsed.Batches.Sum(b=>b.Statements.Count)!=1){throw new SqlQueryException("Use ExecuteBatch for multiple statements.");}
        var statement=parsed.Batches.SelectMany(b=>b.Statements).Single();
        if(SqlTransactionExpressions.TryRewrite(statement,sql,session,out var transactionSql,name=>this.IdentityCurrent(session,name),values,()=>this.registry.GatewayTransactions?.CurrentRowVersion(session,session.DatabaseName)??throw new SqlQueryException("@@DBTS requires the typed Gateway profile.",40515))){return this.ExecuteCore(transactionSql,session,parameters,values);}
        if(SqlServerSessionSettings.TryExecute(statement,session,out var settings)){return settings;}
        if(session.ServerInformationCleanupAvailable&&SqlServerConnectionProbes.IsServerInformationCleanup(statement)){session.ServerInformationCleanupAvailable=false;return QueryResult.Empty;}
        if(session.DatabaseOptionRetentionAvailable&&SqlServerDatabaseQueryProfile.IsCleanup(statement)){session.DatabaseOptionRetentionAvailable=false;return QueryResult.Empty;}
        if(session.DatabaseAvailabilityAvailable&&SqlServerDatabaseAvailabilityProfile.IsCleanup(statement,session.DatabaseAvailabilityCleanupOrdinal)){
            session.DatabaseAvailabilityCleanupOrdinal++;if(session.DatabaseAvailabilityCleanupOrdinal==4){session.DatabaseAvailabilityAvailable=false;session.DatabaseAvailabilityCleanupOrdinal=0;}return QueryResult.Empty;
        }
        if(statement is SelectStatement availability&&session.DatabaseAvailabilityAvailable&&SqlServerDatabaseAvailabilityProfile.TrySelect(availability,sql,out var withoutTemporaryStates)){return this.ExecuteCore(withoutTemporaryStates,session,parameters,values);}
        if(statement is SelectStatement profile&&SqlServerDatabaseQueryProfile.TrySelect(profile,sql,session.DatabaseOptionRetentionAvailable,out var normalized)){return this.ExecuteCore(normalized,session,parameters,values);}
        if(statement is SelectStatement properties&&SqlServerCatalogProperties.TryRewrite(properties,sql,out var literalProperties)){return this.ExecuteCore(literalProperties,session,parameters,values);}
        if(statement is UseStatement use){
            if(session.IsInTransaction&&session.Transaction?.Manager is null){throw new SqlQueryException("Finish the transaction before changing databases.");}
            var database=this.registry.Resolve(use.DatabaseName.Value);
            session.DatabaseName=database.Name;
            return QueryResult.Empty;
        }
        var current=this.Resolve(session);
        if(statement is SetIdentityInsertStatement identityInsert){
            if(this.registry.GatewayTransactions is not{TypedDdlEnabled:true} manager){throw new SqlQueryException("IDENTITY_INSERT requires the typed Gateway profile.",40515);}if(identityInsert.Table.ServerIdentifier is not null){throw new SqlQueryException("IDENTITY_INSERT cannot target another server.",40515);}var registration=this.registry.Resolve(identityInsert.Table.DatabaseIdentifier?.Value??session.DatabaseName);if(registration.Adapter is not InProcessDatabaseAdapter adapter){throw new SqlQueryException("The identity table is unavailable.",208);}using var read=manager.AcquireRead(session,adapter);var table=this.FindTable(adapter,identityInsert.Table)??throw new SqlQueryException("The table does not exist.",208);if(!table.Columns.Any(static column=>column.IsIdentity)){throw new SqlQueryException("The table has no identity column.",8106);}var selected=(adapter.Database.Options.GatewayProfile!.DatabaseId,table.StableObjectId??throw new InvalidDataException("Missing table identity."));if(identityInsert.IsOn){if(session.IdentityInsertTarget is{} previous&&previous!=selected){throw new SqlQueryException("IDENTITY_INSERT is already ON for another table.",8107);}session.IdentityInsertTarget=selected;}else if(session.IdentityInsertTarget==selected){session.IdentityInsertTarget=null;}return QueryResult.NonQuery(0);
        }
        if(statement is SelectStatement){
            if(TryDatabaseName(sql,session,out var databaseName)){return databaseName;}

            // 既存SSMS/SMO専用カタログを先に扱う。一般の表/ビューSELECTは共通エンジンへ渡す。
            var emulated=!HasDatabaseCatalog(statement)&&!HasRelationalCatalog(statement)&&IsSystemQuery(statement);
            // 文ごとに実行するバッチ(SqlBatchInterpreter)の中では、SQL 本文の文字列一致で答える旧来の模擬は使わず、共通エンジンだけで実行する。
            // 模擬に戻すと、未対応の構文が無関係な模擬の答えで隠れてしまう。
            if(emulated&&!session.PreferQueryEngine){using var read=DatabaseRegistry.ReadSqlDatabase(current.Adapter,session);if(SystemCatalogEmulator.TryEmulate(sql,current.Adapter,out var system)){return system;}}
            var result=this.queries.Prepare(sql,current.Name,parameters).Execute(values,this.QueryOptions(session));
            return this.SelectResult(result,session);
        }
        if(statement is PrintStatement print){
            var expression=sql.Substring(print.Expression.StartOffset,print.Expression.FragmentLength);
            var original=this.queries.Prepare("SELECT "+expression,current.Name,parameters);var type=original.Columns.Single().Type;
            var target=type.Name.ToLowerInvariant() switch{"nvarchar" or "nchar" or "ntext"=>"nvarchar(4000)","varchar" or "char" or "text" or "tinyint" or "smallint" or "int" or "bigint" or "decimal" or "numeric"=>"varchar(8000)",_=>null};
            if(target is null&&print.Expression is NullLiteral){target="nvarchar(4000)";}
            if(target is null){throw new SqlQueryException("PRINT supports character, integer, decimal, and NULL expressions only.",40515);}
            var evaluated=this.queries.Prepare("SELECT CAST(("+expression+") AS "+target+")",current.Name,parameters).Execute(values,this.QueryOptions(session));
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
        if(statement is BeginTransactionStatement begin){
            if(begin.Distributed||begin.MarkDefined){throw new SqlQueryException("Distributed and marked transactions are not supported.",40515);}
            var name=TransactionName(begin.Name,values);var outer=!session.IsInTransaction;
            if(this.registry.GatewayTransactions is{} manager){manager.Begin(session);}else{this.EnsureWritable(current);current.Adapter.BeginTransaction(session);}
            if(outer&&session.Transaction is not null){session.Transaction.Name=name;}
            session.LastTransactionOperation=8;return QueryResult.NonQuery(0);
        }
        if(statement is SaveTransactionStatement save){session.CreateSavepoint(TransactionName(save.Name,values)??throw new SqlQueryException("A savepoint name is required.",102));return QueryResult.NonQuery(0);}
        if(statement is CommitTransactionStatement){if(session.Transaction?.Manager is{} manager){manager.Commit(session);}else{current.Adapter.CommitTransaction(session);}session.LastTransactionOperation=9;return QueryResult.NonQuery(0);}
        if(statement is RollbackTransactionStatement rollback){
            var name=TransactionName(rollback.Name,values);
            if(name is not null&&!string.Equals(name,session.Transaction?.Name,StringComparison.Ordinal)){session.RollbackToSavepoint(name);return QueryResult.NonQuery(0);}
            if(session.Transaction?.Manager is{} manager){manager.Rollback(session);}else{current.Adapter.RollbackTransaction(session);}session.LastTransactionOperation=10;return QueryResult.NonQuery(0);
        }
        if(statement is CreateTableStatement or AlterTableStatement or DropTableStatement or FunctionStatementBody or DropFunctionStatement){
            if(this.registry.GatewayTransactions is{TypedDdlEnabled:true}){return this.ExecuteTypedDdl(statement,sql,session);}
            this.EnsureWritable(current);
            if(current.Adapter is not InProcessDatabaseAdapter adapter||adapter.Database is not RuntimeSqlDatabaseContainer){throw new SqlQueryException("Runtime SQL DDL is not enabled for this database.",40515);}
            var ddl=SqlDdlParser.Parse(sql);var implicitTransaction=!session.IsInTransaction;
            if(implicitTransaction){adapter.BeginTransaction(session);}
            try{adapter.PrepareGatewayWrite(session);adapter.ExecuteDdl(ddl);if(implicitTransaction){adapter.CommitTransaction(session);}return QueryResult.NonQuery(0);}
            catch{if(implicitTransaction&&session.IsInTransaction){adapter.RollbackTransaction(session);}throw;}
        }
        if(statement is InsertStatement or UpdateStatement or DeleteStatement){
            return this.ExecuteMutation(statement,sql,session,current,parameters,values);
        }
        throw new SqlQueryException($"SQL statement '{statement?.GetType().Name??"unknown"}' is not supported.",40515);
    }

    private QueryResult ExecuteMutation(TSqlStatement statement,string sql,SessionContext session,DatabaseRegistration current,IReadOnlyList<SqlParameter>? parameters,IReadOnlyDictionary<string,object?>? values,SqlQueryEngine? protectedQueries=null,Action<string,IDatabaseAdapter,TableMetadata,SqlCommandKind>? demandMutation=null,Action? requireCurrentProcedure=null){
            using var statementClock=session.BeginStatement();
            session.ConstraintStatementDepth++;try{
            if(demandMutation is not null&&statement is StatementWithCtesAndXmlNamespaces{WithCtesAndXmlNamespaces:not null}){throw new SqlQueryException("A CTE mutation is unavailable through the protected SQL endpoint.",40515);}
            if(demandMutation is not null&&MutationTarget(statement) is NamedTableReference{SchemaObject.DatabaseIdentifier:{} database}
                &&!database.Value.Equals(current.Name,StringComparison.OrdinalIgnoreCase)){
                throw new SqlQueryException("A protected SQL mutation must target its selected database.",40515);
            }
            current=this.ResolveDmlTarget(statement,current);
            if(session.IsInTransaction&&session.Transaction?.Manager is null&&!string.Equals(current.Name,session.DatabaseName,StringComparison.OrdinalIgnoreCase)){throw new SqlQueryException("A standalone SQL transaction cannot write another database.",40515);}
            this.EnsureWritable(current);
            var implicitTransaction=!session.IsInTransaction;var executing=false;
            if(implicitTransaction){current.Adapter.BeginTransaction(session);}
            string? statementSavepoint=null;
            if(!implicitTransaction&&current.Adapter is InProcessDatabaseAdapter){statementSavepoint="statement/"+Guid.NewGuid().ToString("N");session.CreateSavepoint(statementSavepoint);}
            try{
                requireCurrentProcedure?.Invoke();
                if(current.Adapter is InProcessDatabaseAdapter gateway){gateway.PrepareGatewayWrite(session);}
                var kind=statement switch{InsertStatement=>SqlCommandKind.Insert,UpdateStatement=>SqlCommandKind.Update,_=>SqlCommandKind.Delete};
                Action<TableMetadata>? demand=null;
                if(demandMutation is not null){
                    demand=table=>demandMutation(current.Name,current.Adapter,table,kind);
                }
                var command=ScriptDomParser.Parse(sql,current.Adapter,values,current.Name,
                    (expression,table,allowColumn)=>this.CompileDmlScalar(expression,sql,current,table,allowColumn,session,parameters,values,()=>executing=true,protectedQueries),
                    (condition,table)=>this.CompileDmlPredicate(condition,sql,current,table,session,parameters,values,()=>executing=true,protectedQueries),demand,this.registry.SchemaResolution);executing=true;
                var count=0;IReadOnlyDictionary<string,Func<object,object?>>? pointUpdates=null;if(statement is UpdateStatement){pointUpdates=command.UpdateValues;}
                if(current.Adapter is InProcessDatabaseAdapter point&&this.registry.GatewayTransactions?.IsVersioned==true&&TryIntegerPoint(statement,values,out var keyColumn,out var key)&&point.TryExecutePointMutation(command.TableName,keyColumn,key,pointUpdates,command.Predicate,session,out var changed)){count=changed;}
                else if(statement is InsertStatement){foreach(var row in command.InsertRows){count=checked(count+current.Adapter.ExecuteInsert(command.TableName,row,session));}}
                else if(statement is UpdateStatement){count=current.Adapter.ExecuteUpdateExpressions(command.TableName,command.UpdateValues,command.Predicate,session);}
                else{count=current.Adapter.ExecuteDelete(command.TableName,command.Predicate,session);}
                if(session.ConstraintStatementDepth==1&&session.Transaction?.Manager is{TypedDdlEnabled:true} constraints){constraints.ValidateConstraints(session,current.Name);}
                if(implicitTransaction){if(demandMutation is not null){session.CancellationToken.ThrowIfCancellationRequested();}current.Adapter.CommitTransaction(session);}
                return demandMutation is null?QueryResult.NonQuery(count):QueryResult.NonQuery(count,kind);
            }catch(Exception error){
                if(implicitTransaction&&session.IsInTransaction){current.Adapter.RollbackTransaction(session);}
                else if(session.IsInTransaction&&current.Adapter is InProcessDatabaseAdapter failed){
                    if(session.XactAbort&&(executing||error is OperationCanceledException||error is SqlQueryException{Number:1222})){
                        if(session.TryCatchDepth>0){if(statementSavepoint is not null&&session.TransactionState>0){session.RollbackToSavepoint(statementSavepoint);}failed.MarkTransactionFailed(session);}else{current.Adapter.RollbackTransaction(session);session.LastTransactionOperation=10;}
                    }else if(statementSavepoint is not null&&session.TransactionState>0){
                        try{session.RollbackToSavepoint(statementSavepoint);}catch{failed.MarkTransactionFailed(session);throw;}
                    }
                }
                throw;
            }finally{
                if(statementSavepoint is not null&&session.IsInTransaction){
                    try{session.ReleaseSavepoint(statementSavepoint);}catch{if(current.Adapter is InProcessDatabaseAdapter failed){failed.MarkTransactionFailed(session);}throw;}
                }
            }
            }finally{session.ConstraintStatementDepth--;}

    }
    private static TableReference? MutationTarget(TSqlStatement statement)=>statement switch{InsertStatement insert=>insert.InsertSpecification.Target,UpdateStatement update=>update.UpdateSpecification.Target,DeleteStatement delete=>delete.DeleteSpecification.Target,_=>null};
    private DatabaseRegistration ResolveDmlTarget(TSqlStatement statement,DatabaseRegistration current){
        if(MutationTarget(statement) is NamedTableReference named){
            if(named.SchemaObject.ServerIdentifier is not null){throw new SqlQueryException("Remote-server writes are not supported.",40515);}
            if(named.SchemaObject.DatabaseIdentifier is{} database){return this.registry.Resolve(database.Value);}
        }
        return current;
    }
    private static string? TransactionName(IdentifierOrValueExpression? expression,IReadOnlyDictionary<string,object?>? values){
        if(expression is null){return null;}
        string? name;
        if(expression.ValueExpression is VariableReference variable){
            if(values is null||!values.TryGetValue(variable.Name,out var value)||value is not string text){throw new SqlQueryException("A transaction or savepoint name must be a character value.",155);}
            name=text;
        }else{name=expression.Value;}
        if(string.IsNullOrEmpty(name)){throw new SqlQueryException("A transaction or savepoint name is required.",102);}
        if(name.Length>32){return name[..32];}
        return name;
    }
    private DatabaseRegistration Resolve(SessionContext session)=>this.registry.Resolve(session.DatabaseName);
    private void EnsureWritable(DatabaseRegistration database){if(database.IsReadOnly){throw new SqlQueryException($"Database '{database.Name}' is read only.",3906);}}
    private QueryResult DatabaseList(){
        var rows=new List<object?[]>{new object?[]{"master",1,"Japanese_CI_AS",(byte)0,"ONLINE",SqlServerVersion.CompatibilityLevel,false,(byte)0,"MULTI_USER",true,(byte)1,"FULL"}};
        var id=5;
        foreach(var db in this.registry.Databases){rows.Add([db.Name,id++,"Japanese_CI_AS",(byte)0,"ONLINE",SqlServerVersion.CompatibilityLevel,db.IsReadOnly,(byte)0,"MULTI_USER",true,(byte)1,"FULL"]);}
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
            if(string.Equals(node.SchemaObject.SchemaIdentifier?.Value,"sys",StringComparison.OrdinalIgnoreCase)&&node.SchemaObject.BaseIdentifier.Value.ToLowerInvariant() is "schemas" or "tables" or "views" or "objects" or "columns" or "types" or "indexes" or "index_columns" or "procedures" or "sql_modules" or "parameters" or "all_objects" or "all_views" or "all_columns" or "all_sql_modules" or "system_sql_modules" or "extended_properties" or "data_spaces" or "identity_columns" or "computed_columns" or "check_constraints" or "default_constraints" or "key_constraints" or "foreign_keys" or "foreign_key_columns"){this.Found=true;}
        }

    }
    private static bool HasDatabaseCatalog(TSqlStatement statement){var visitor=new DatabaseCatalogVisitor();statement.Accept(visitor);return visitor.Found;}
    private sealed class DatabaseCatalogVisitor:TSqlFragmentVisitor{public bool Found{get;private set;}public override void ExplicitVisit(NamedTableReference node){if(string.Equals(node.SchemaObject.SchemaIdentifier?.Value,"sys",StringComparison.OrdinalIgnoreCase)&&string.Equals(node.SchemaObject.BaseIdentifier.Value,"databases",StringComparison.OrdinalIgnoreCase)){this.Found=true;}}}
    private static bool IsSystemQuery(TSqlStatement statement){var visitor=new SystemVisitor();statement.Accept(visitor);return visitor.Found;}
    private static DatabaseRegistry CreateRegistry(IDatabaseAdapter adapter,SqlSchemaResolution resolution){var registry=new DatabaseRegistry(){SchemaResolution=resolution};registry.Register(adapter.DatabaseName,adapter);return registry;}
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
