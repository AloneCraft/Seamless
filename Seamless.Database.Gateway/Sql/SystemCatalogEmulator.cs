using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Rdbms;

namespace Seamless.Database.Gateway.Sql;

public static class SystemCatalogEmulator{
    internal const int ProfileEngineEdition=3;
    internal const int ProfileHadrEnabled=0;
    internal static string PropertySqlLiteral(string name){
        var property=EmulateServerPropertyQuery("SELECT SERVERPROPERTY(N'"+name.Replace("'","''",StringComparison.Ordinal)+"')");
        var column=property.Columns[0];var value=property.Rows.Single()[0];var type=column.SqlTypeName;
        if(column.ClrType==typeof(string)){type+="("+(column.MaximumLength??128).ToString(global::System.Globalization.CultureInfo.InvariantCulture)+")";}
        var literal="NULL";
        if(value is string text){literal="N'"+text.Replace("'","''",StringComparison.Ordinal)+"'";}
        else if(value is int number){literal=number.ToString(global::System.Globalization.CultureInfo.InvariantCulture);}
        else if(value is not null){throw new NotSupportedException("Unsupported catalog server-property value.");}
        return "CAST("+literal+" AS "+type+")";
    }
    public static bool TryEmulateBatch(string sql,IDatabaseAdapter adapter,out IReadOnlyList<QueryResult> results){
        var normalized=NormalizeSql(sql);

        // SSMS の接続時バッチの検出:
        // DECLARE @edition sysname; ... SELECT ... DatabaseEngineType ... select host_platform from sys.dm_os_host_info ... CONNECTIONPROPERTY ...
        if(SqlServerConnectionProbes.IsConnectionInitializationSql(sql)){
            var resList=new List<QueryResult>();

            // 1. サーバープロパティの SELECT 結果
            resList.Add(EmulateServerPropertyQuery(sql));

            // 2. sys.dm_os_host_info の SELECT 結果
            var hostCols=new[]{
                new ColumnMetadata("host_platform",typeof(string),"nvarchar",false,false,0),
                new ColumnMetadata("host_distribution",typeof(string),"nvarchar",false,false,1),
                new ColumnMetadata("host_release",typeof(string),"nvarchar",false,false,2),
                new ColumnMetadata("host_service_pack_level",typeof(string),"nvarchar",false,false,3),
                new ColumnMetadata("os_architecture",typeof(int),"int",false,false,4),
                new ColumnMetadata("host_sku",typeof(int),"int",false,false,5),
                new ColumnMetadata("os_language_version",typeof(int),"int",false,false,6)
            };
            var hostRows=new[]{
                new object?[]{"Windows","Microsoft Windows 11 Pro","10.0.22631","",64,48,1041}
            };
            resList.Add(new QueryResult(hostCols,hostRows,1));

            // 3. CONNECTIONPROPERTY('net_transport') の SELECT 結果
            var connCols=new[]{
                new ColumnMetadata("ConnectionProtocol",typeof(string),"nvarchar",false,false,0)
            };
            var connRows=new[]{
                new object?[]{"TCP"}
            };
            resList.Add(new QueryResult(connCols,connRows,1));

            results=resList;
            return true;
        }else{
        }

        if(TryEmulate(sql,adapter,out var singleResult)){
            results=new[]{singleResult};
            return true;
        }else{
        }

        results=Array.Empty<QueryResult>();
        return false;
    }

    public static bool TryEmulate(string sql,IDatabaseAdapter adapter,out QueryResult result){
        var normalized=NormalizeSql(sql);

        if(normalized.Contains("#SVer",StringComparison.OrdinalIgnoreCase)&&SqlServerConnectionProbes.IsServerInformationSql(sql)){
            // SSMS の SMO によるサーバー情報取得クエリ
            var cols=new[]{
                new ColumnMetadata("Server_Name",typeof(string),"nvarchar",false,false,0),
                new ColumnMetadata("Server_Urn",typeof(string),"nvarchar",false,false,1),
                new ColumnMetadata("Server_ServerType",typeof(int),"int",true,false,2),
                new ColumnMetadata("Server_Status",typeof(int),"int",false,false,3),
                new ColumnMetadata("Server_IsContainedAuthentication",typeof(int),"int",false,false,4),
                new ColumnMetadata("VersionMajor",typeof(int),"int",false,false,5),
                new ColumnMetadata("VersionMinor",typeof(int),"int",false,false,6),
                new ColumnMetadata("BuildNumber",typeof(int),"int",false,false,7),
                new ColumnMetadata("IsSingleUser",typeof(bool),"bit",false,false,8),
                new ColumnMetadata("Edition",typeof(string),"nvarchar",false,false,9),
                new ColumnMetadata("EngineEdition",typeof(int),"int",false,false,10),
                new ColumnMetadata("IsXTPSupported",typeof(bool),"bit",false,false,11),
                new ColumnMetadata("VersionString",typeof(string),"nvarchar",false,false,12),
                new ColumnMetadata("HostPlatform",typeof(string),"nvarchar",false,false,13),
                new ColumnMetadata("IsFullTextInstalled",typeof(bool),"bit",false,false,14)
            };
            var rows=new[]{
                new object?[]{
                    "127.0.0.1,14333",
                    "Server[@Name='127.0.0.1,14333']",
                    null, // CAST(NULL AS int) in the captured management query
                    1,
                    0,
                    16,
                    0,
                    1000,
                    false,
                    "Enterprise Edition (64-bit)",
                    3,
                    false,
                    "16.0.1000.6",
                    "Windows",
                    false
                }
            };
            result=new QueryResult(cols,rows,1);
            return true;
        }else{
        }

        if(normalized.StartsWith("SET ",StringComparison.OrdinalIgnoreCase)){
            result=QueryResult.NonQuery(0);
            return true;
        }else{
        }

        if(normalized.StartsWith("USE ",StringComparison.OrdinalIgnoreCase)){
            result=QueryResult.NonQuery(0);
            return true;
        }else{
        }

        if(normalized.Contains("DB_NAME()",StringComparison.OrdinalIgnoreCase)){
            var cols=new[]{
                new ColumnMetadata("",typeof(string),"nvarchar",false,false,0)
            };
            var rows=new[]{
                new object?[]{adapter.DatabaseName}
            };
            result=new QueryResult(cols,rows,1);
            return true;
        }else{
        }

        if(normalized.Contains("sysdac_instances",StringComparison.OrdinalIgnoreCase)){
            var cols=new[]{
                new ColumnMetadata("",typeof(int),"int",false,false,0)
            };
            var rows=new[]{
                new object?[]{0}
            };
            result=new QueryResult(cols,rows,1);
            return true;
        }else{
        }

        if(normalized.Contains("SERVERPROPERTY",StringComparison.OrdinalIgnoreCase)||normalized.Contains("DatabaseEngineType",StringComparison.OrdinalIgnoreCase)){
            result=EmulateServerPropertyQuery(sql);
            return true;
        }else{
        }

        if(normalized.Contains("@@VERSION",StringComparison.OrdinalIgnoreCase)){
            var cols=new[]{new ColumnMetadata("",typeof(string),"nvarchar",false,false,0)};
            var rows=new[]{new object?[]{"Microsoft SQL Server 2022 (RTM) - 16.0.1000.6 (X64) - SeamlessDB Server 1.0"}};
            result=new QueryResult(cols,rows,1);
            return true;
        }else{
        }

        if(normalized.Contains("@@MAX_PRECISION",StringComparison.OrdinalIgnoreCase)){
            var cols=new[]{new ColumnMetadata("",typeof(byte),"tinyint",false,false,0)};
            var rows=new[]{new object?[]{(byte)38}};
            result=new QueryResult(cols,rows,1);
            return true;
        }else{
        }

        if(normalized.Contains("@@OPTIONS",StringComparison.OrdinalIgnoreCase)){
            var cols=new[]{new ColumnMetadata("",typeof(int),"int",false,false,0)};
            var rows=new[]{new object?[]{5496}};
            result=new QueryResult(cols,rows,1);
            return true;
        }else{
        }

        if(normalized.Contains("sys.dm_os_host_info",StringComparison.OrdinalIgnoreCase)){
            var cols=new[]{
                new ColumnMetadata("host_platform",typeof(string),"nvarchar",false,false,0),
                new ColumnMetadata("host_distribution",typeof(string),"nvarchar",false,false,1),
                new ColumnMetadata("host_release",typeof(string),"nvarchar",false,false,2),
                new ColumnMetadata("host_service_pack_level",typeof(string),"nvarchar",false,false,3),
                new ColumnMetadata("os_architecture",typeof(int),"int",false,false,4),
                new ColumnMetadata("host_sku",typeof(int),"int",false,false,5),
                new ColumnMetadata("os_language_version",typeof(int),"int",false,false,6)
            };
            var rows=new[]{
                new object?[]{"Windows","Microsoft Windows 11 Pro","10.0.22631","",64,48,1041}
            };
            result=new QueryResult(cols,rows,1);
            return true;
        }else{
        }

        if(normalized.Contains("has_dbaccess",StringComparison.OrdinalIgnoreCase)){
            var cols=new[]{new ColumnMetadata("",typeof(int),"int",false,false,0)};
            var rows=new[]{new object?[]{1}};
            result=new QueryResult(cols,rows,1);
            return true;
        }else{
        }

        if(normalized.Contains("suser_sname",StringComparison.OrdinalIgnoreCase)||normalized.Contains("suser_name",StringComparison.OrdinalIgnoreCase)||normalized.Contains("user_name()",StringComparison.OrdinalIgnoreCase)){
            var cols=new[]{new ColumnMetadata("",typeof(string),"nvarchar",false,false,0)};
            var rows=new[]{new object?[]{"sa"}};
            result=new QueryResult(cols,rows,1);
            return true;
        }else{
        }

        if(normalized.Contains("is_srvrolemember",StringComparison.OrdinalIgnoreCase)||normalized.Contains("is_rolemember",StringComparison.OrdinalIgnoreCase)){
            var cols=new[]{new ColumnMetadata("",typeof(int),"int",false,false,0)};
            var rows=new[]{new object?[]{1}};
            result=new QueryResult(cols,rows,1);
            return true;
        }else{
        }

        if(normalized.Contains("databasepropertyex",StringComparison.OrdinalIgnoreCase)){
            var val="ONLINE";
            if(normalized.Contains("collation",StringComparison.OrdinalIgnoreCase)){
                val="Japanese_CI_AS";
            }else if(normalized.Contains("version",StringComparison.OrdinalIgnoreCase)){
                val="904";
            }else{
            }
            var cols=new[]{new ColumnMetadata("",typeof(string),"nvarchar",false,false,0)};
            var rows=new[]{new object?[]{val}};
            result=new QueryResult(cols,rows,1);
            return true;
        }else{
        }

        if(normalized.Contains("fn_listextendedproperty",StringComparison.OrdinalIgnoreCase)){
            var cols=new[]{
                new ColumnMetadata("objtype",typeof(string),"nvarchar",false,false,0),
                new ColumnMetadata("objname",typeof(string),"nvarchar",false,false,1),
                new ColumnMetadata("name",typeof(string),"nvarchar",false,false,2),
                new ColumnMetadata("value",typeof(string),"nvarchar",false,false,3)
            };
            result=new QueryResult(cols,Array.Empty<object?[]>(),0);
            return true;
        }else{
        }

        if(normalized.Contains("fn_my_permissions",StringComparison.OrdinalIgnoreCase)){
            var cols=new[]{
                new ColumnMetadata("entity_name",typeof(string),"nvarchar",false,false,0),
                new ColumnMetadata("subentity_name",typeof(string),"nvarchar",false,false,1),
                new ColumnMetadata("permission_name",typeof(string),"nvarchar",false,false,2)
            };
            var rows=new[]{
                new object?[]{"server","","CONTROL SERVER"}
            };
            result=new QueryResult(cols,rows,1);
            return true;
        }else{
        }

        if(normalized.Contains("sys.configurations",StringComparison.OrdinalIgnoreCase)){
            var cols=new[]{
                new ColumnMetadata("configuration_id",typeof(int),"int",false,false,0),
                new ColumnMetadata("name",typeof(string),"nvarchar",false,false,1),
                new ColumnMetadata("value",typeof(int),"int",false,false,2),
                new ColumnMetadata("minimum",typeof(int),"int",false,false,3),
                new ColumnMetadata("maximum",typeof(int),"int",false,false,4),
                new ColumnMetadata("value_in_use",typeof(int),"int",false,false,5),
                new ColumnMetadata("description",typeof(string),"nvarchar",false,false,6),
                new ColumnMetadata("is_dynamic",typeof(bool),"bit",false,false,7),
                new ColumnMetadata("is_advanced",typeof(bool),"bit",false,false,8)
            };
            result=new QueryResult(cols,Array.Empty<object?[]>(),0);
            return true;
        }else{
        }

        if(normalized.Contains("CONNECTIONPROPERTY",StringComparison.OrdinalIgnoreCase)){
            var cols=new[]{
                new ColumnMetadata("ConnectionProtocol",typeof(string),"nvarchar",false,false,0)
            };
            var rows=new[]{
                new object?[]{"TCP"}
            };
            result=new QueryResult(cols,rows,1);
            return true;
        }else{
        }

        if(normalized.StartsWith("USE ",StringComparison.OrdinalIgnoreCase)){
            result=QueryResult.NonQuery(0);
            return true;
        }else{
        }

        if(normalized.Contains("sys.databases",StringComparison.OrdinalIgnoreCase)){
            ColumnMetadata[] cols=[
                new ColumnMetadata("name",typeof(string),"nvarchar",false,false,0),
                new ColumnMetadata("database_id",typeof(int),"int",false,false,1),
                new ColumnMetadata("collation_name",typeof(string),"nvarchar",true,false,2),
                new ColumnMetadata("state",typeof(byte),"tinyint",false,false,3),
                new ColumnMetadata("state_desc",typeof(string),"nvarchar",false,false,4),
                new ColumnMetadata("compatibility_level",typeof(byte),"tinyint",false,false,5),
                new ColumnMetadata("is_read_only",typeof(bool),"bit",false,false,6),
                new ColumnMetadata("user_access",typeof(byte),"tinyint",false,false,7),
                new ColumnMetadata("user_access_desc",typeof(string),"nvarchar",false,false,8),
                new ColumnMetadata("is_cleanly_shutdown",typeof(bool),"bit",false,false,9),
                new ColumnMetadata("recovery_model",typeof(byte),"tinyint",false,false,10),
                new ColumnMetadata("recovery_model_desc",typeof(string),"nvarchar",false,false,11),
                new ColumnMetadata("catalog_collation_type",typeof(int),"int",true,false,12),
                new ColumnMetadata("catalog_collation_type_desc",typeof(string),"nvarchar",true,false,13,60),
                new ColumnMetadata("containment",typeof(byte),"tinyint",false,false,14),
                new ColumnMetadata("containment_desc",typeof(string),"nvarchar",false,false,15,60),
                new ColumnMetadata("is_distributor",typeof(bool),"bit",false,false,16),
                new ColumnMetadata("source_database_id",typeof(int),"int",true,false,17),
                ..SqlServerDatabaseOptionCatalog.Columns,
                new ColumnMetadata("is_fulltext_enabled",typeof(bool),"bit",false,false,43),
                new ColumnMetadata("is_ledger_on",typeof(bool),"bit",false,false,44)
            ];
            var rows=new List<object?[]>{
                new object?[]{"master",1,"Japanese_CI_AS",(byte)0,"ONLINE",(byte)160,false,(byte)0,"MULTI_USER",true,(byte)1,"FULL",0,"DATABASE_DEFAULT",(byte)0,"NONE",false,null},
                new object?[]{"tempdb",2,"Japanese_CI_AS",(byte)0,"ONLINE",(byte)160,false,(byte)0,"MULTI_USER",true,(byte)1,"FULL",0,"DATABASE_DEFAULT",(byte)0,"NONE",false,null},
                new object?[]{adapter.DatabaseName,5,"Japanese_CI_AS",(byte)0,"ONLINE",(byte)160,false,(byte)0,"MULTI_USER",true,(byte)1,"FULL",0,"DATABASE_DEFAULT",(byte)0,"NONE",false,null}
            };
            result=new QueryResult(cols,rows.Select(SqlServerDatabaseOptionCatalog.Expand),rows.Count);
            return true;
        }else{
        }

        if(normalized.Contains("sys.database_principals",StringComparison.OrdinalIgnoreCase)){
            var cols=new[]{
                new ColumnMetadata("name",typeof(string),"nvarchar",false,false,0),
                new ColumnMetadata("principal_id",typeof(int),"int",false,false,1),
                new ColumnMetadata("type",typeof(string),"char",false,false,2),
                new ColumnMetadata("type_desc",typeof(string),"nvarchar",false,false,3),
                new ColumnMetadata("default_schema_name",typeof(string),"nvarchar",true,false,4)
            };
            var rows=new List<object?[]>{
                new object?[]{"dbo",1,"S ","SQL_USER","dbo"},
                new object?[]{"guest",2,"S ","SQL_USER","guest"}
            };
            result=new QueryResult(cols,rows,rows.Count);
            return true;
        }else{
        }

        if(normalized.Contains("sys.database_role_members",StringComparison.OrdinalIgnoreCase)){
            var cols=new[]{
                new ColumnMetadata("role_principal_id",typeof(int),"int",false,false,0),
                new ColumnMetadata("member_principal_id",typeof(int),"int",false,false,1)
            };
            result=new QueryResult(cols,Array.Empty<object?[]>(),0);
            return true;
        }else{
        }

        foreach(var catalogName in new[]{"schemas","tables","columns","types","indexes","index_columns","views","objects","procedures","sql_modules","parameters"}){
            if(normalized.Contains("sys."+catalogName,StringComparison.OrdinalIgnoreCase)&&TryCatalog(catalogName,adapter,adapter.GetViews(),out result)){return true;}
        }
        if(normalized.Contains("sys.systypes",StringComparison.OrdinalIgnoreCase)&&TryCatalog("types",adapter,adapter.GetViews(),out result)){return true;}
        if(normalized.Contains("INFORMATION_SCHEMA.TABLES",StringComparison.OrdinalIgnoreCase)&&TryCatalog("tables",adapter,adapter.GetViews(),out result)){return true;}
        if(normalized.Contains("INFORMATION_SCHEMA.COLUMNS",StringComparison.OrdinalIgnoreCase)&&TryCatalog("columns",adapter,adapter.GetViews(),out result)){return true;}
        if(normalized.Contains("sys.all_views",StringComparison.OrdinalIgnoreCase)&&TryCatalog("views",adapter,adapter.GetViews(),out result)){return true;}
        if(normalized.Contains("sys.all_objects",StringComparison.OrdinalIgnoreCase)&&TryCatalog("objects",adapter,adapter.GetViews(),out result)){return true;}
        if(normalized.Contains("sys.all_columns",StringComparison.OrdinalIgnoreCase)&&TryCatalog("columns",adapter,adapter.GetViews(),out result)){return true;}
        if(normalized.Contains("sys.triggers",StringComparison.OrdinalIgnoreCase)||
           normalized.Contains("sys.foreign_keys",StringComparison.OrdinalIgnoreCase)||normalized.Contains("sys.foreign_key_columns",StringComparison.OrdinalIgnoreCase)||
           normalized.Contains("sys.key_constraints",StringComparison.OrdinalIgnoreCase)||normalized.Contains("sys.default_constraints",StringComparison.OrdinalIgnoreCase)||
           normalized.Contains("sys.check_constraints",StringComparison.OrdinalIgnoreCase)||normalized.Contains("sys.synonyms",StringComparison.OrdinalIgnoreCase)){
            var cols=new[]{new ColumnMetadata("name",typeof(string),"nvarchar",false,false,0),new ColumnMetadata("object_id",typeof(int),"int",false,false,1),new ColumnMetadata("schema_id",typeof(int),"int",false,false,2),new ColumnMetadata("type",typeof(string),"char",false,false,3),new ColumnMetadata("type_desc",typeof(string),"nvarchar",false,false,4)};
            result=new QueryResult(cols,Array.Empty<object?[]>(),0);return true;
        }

        result=QueryResult.Empty;
        return false;
    }

    // SERVERPROPERTY normally returns sql_variant; this SSMS compatibility path exposes its base type until variant wire support exists.
    private readonly record struct PropertyValue(Type Type,string SqlType,object? Value,int? Length=null,bool UntypedNull=false);

    private static QueryResult EmulateServerPropertyQuery(string sql){
        var parser=new TSql160Parser(true);
        var fragment=parser.Parse(new StringReader(sql),out var errors);
        if(errors.Count>0){throw new NotSupportedException("Unsupported server property SQL syntax.");}
        if(fragment is not TSqlScript script){throw new NotSupportedException("Server property SQL was not parsed.");}
        var evaluator=new PropertyExpressionEvaluator();QuerySpecification? select=null;
        foreach(var statement in script.Batches.SelectMany(batch=>batch.Statements)){
            if(statement is DeclareVariableStatement declaration){evaluator.Declare(declaration);continue;}
            if(statement is SetVariableStatement assignment){evaluator.Assign(assignment);continue;}
            if(statement is SelectStatement{QueryExpression:QuerySpecification query}){select=query;break;}
            throw new NotSupportedException("Unsupported server property setup: "+statement.GetType().Name);
        }
        if(select is null||select.FromClause is not null||select.WhereClause is not null){throw new NotSupportedException("Unsupported server property SELECT.");}
        var columns=new List<ColumnMetadata>();var values=new List<object?>();
        foreach(var element in select.SelectElements){
            if(element is not SelectScalarExpression item){throw new NotSupportedException("Unsupported server property projection.");}
            var value=evaluator.EvaluatePropertyExpression(item.Expression);var name=item.ColumnName?.Value??"";
            columns.Add(new ColumnMetadata(name,value.Type,value.SqlType,true,false,columns.Count,value.Length));
            values.Add(value.Value);
        }
        if(columns.Count==0){throw new NotSupportedException("Empty server property SELECT.");}
        return new QueryResult(columns,[values.ToArray()],1);
    }

    private sealed class PropertyExpressionEvaluator{
        private readonly Dictionary<string,PropertyValue>variables=new(StringComparer.OrdinalIgnoreCase);
        internal void Declare(DeclareVariableStatement statement){
            foreach(var declaration in statement.Declarations){
                if(declaration.Value is not null){throw new NotSupportedException("Variable initializers are not supported in server property setup.");}
                var type=ConvertProperty(new(typeof(string),"nvarchar",null,128),declaration.DataType);
                if(!this.variables.TryAdd(declaration.VariableName.Value,type)){throw new NotSupportedException("Duplicate server property variable.");}
            }
        }
        internal void Assign(SetVariableStatement statement){
            if(statement.AssignmentKind!=AssignmentKind.Equals||statement.FunctionCallExists||!this.variables.TryGetValue(statement.Variable.Name,out var type)){throw new NotSupportedException("Unsupported server property variable assignment.");}
            this.variables[statement.Variable.Name]=ConvertPropertyValue(this.EvaluatePropertyExpression(statement.Expression),type);
        }
    internal PropertyValue EvaluatePropertyExpression(ScalarExpression expression){
        switch(expression){
            case ParenthesisExpression parenthesis:return EvaluatePropertyExpression(parenthesis.Expression);
            case NullLiteral:return new(typeof(string),"nvarchar",null,128,true);
            case StringLiteral text:return new(typeof(string),text.IsNational?"nvarchar":"varchar",text.Value,Math.Max(1,text.Value.Length));
            case IntegerLiteral integer:return new(typeof(int),"int",int.Parse(integer.Value,global::System.Globalization.CultureInfo.InvariantCulture));
            case VariableReference variable:return this.variables.TryGetValue(variable.Name,out var value)?value:throw new NotSupportedException("Unknown server property variable.");
            case GlobalVariableExpression global when string.Equals(global.Name,"@@MICROSOFTVERSION",StringComparison.OrdinalIgnoreCase):return new(typeof(int),"int",0x100003E8);
            case GlobalVariableExpression global when string.Equals(global.Name,"@@VERSION",StringComparison.OrdinalIgnoreCase):return new(typeof(string),"nvarchar","Microsoft SQL Server 2022 (RTM) - 16.0.1000.6 (X64) - SeamlessDB Server 1.0",128);
            case FunctionCall function when string.Equals(function.FunctionName.Value,"SERVERPROPERTY",StringComparison.OrdinalIgnoreCase):
                if(function.Parameters.Count!=1||function.Parameters[0] is not StringLiteral property){throw new NotSupportedException("SERVERPROPERTY requires a literal property name.");}
                return Property(property.Value);
            case CastCall cast:return ConvertProperty(EvaluatePropertyExpression(cast.Parameter),cast.DataType);
            case ConvertCall convert when convert.Style is null:return ConvertProperty(EvaluatePropertyExpression(convert.Parameter),convert.DataType);
            case SearchedCaseExpression conditional:{
                var candidates=conditional.WhenClauses.Select(item=>EvaluatePropertyExpression(item.ThenExpression)).ToList();
                if(conditional.ElseExpression is not null){candidates.Add(EvaluatePropertyExpression(conditional.ElseExpression));}
                var type=PropertyCaseType(candidates);
                for(var i=0;i<conditional.WhenClauses.Count;i++){if(EvaluatePropertyPredicate(conditional.WhenClauses[i].WhenExpression)==true){return ConvertPropertyValue(candidates[i],type);}}
                if(conditional.ElseExpression is not null){return ConvertPropertyValue(candidates[^1],type);}
                return type with{Value=null};
            }
            case SimpleCaseExpression conditional:{
                var input=EvaluatePropertyExpression(conditional.InputExpression);var candidates=conditional.WhenClauses.Select(item=>EvaluatePropertyExpression(item.ThenExpression)).ToList();
                if(conditional.ElseExpression is not null){candidates.Add(EvaluatePropertyExpression(conditional.ElseExpression));}
                var type=PropertyCaseType(candidates);
                for(var i=0;i<conditional.WhenClauses.Count;i++){if(PropertyEqual(input,EvaluatePropertyExpression(conditional.WhenClauses[i].WhenExpression))==true){return ConvertPropertyValue(candidates[i],type);}}
                if(conditional.ElseExpression is not null){return ConvertPropertyValue(candidates[^1],type);}
                return type with{Value=null};
            }
            default:throw new NotSupportedException("Unsupported server property expression: "+expression.GetType().Name);
        }
    }
    private static PropertyValue Property(string name)=>name.ToUpperInvariant() switch{
        "ENGINEEDITION"=>new(typeof(int),"int",ProfileEngineEdition),
        "PRODUCTVERSION"=>new(typeof(string),"nvarchar","16.0.1000.6",128),
        "PRODUCTMAJORVERSION"=>new(typeof(string),"nvarchar","16",128),
        "PRODUCTMINORVERSION"=>new(typeof(string),"nvarchar","0",128),
        "PRODUCTBUILD"=>new(typeof(string),"nvarchar","1000",128),
        "PRODUCTLEVEL"=>new(typeof(string),"nvarchar","RTM",128),
        "EDITION"=>new(typeof(string),"nvarchar","Enterprise Edition (64-bit)",128),
        "INSTANCENAME"=>new(typeof(string),"nvarchar",null,128),
        "SERVERNAME" or "MACHINENAME"=>new(typeof(string),"nvarchar","127.0.0.1,14333",128),
        "ISHADRENABLED"=>new(typeof(int),"int",ProfileHadrEnabled),
        "ISCLUSTERED" or "ISSINGLEUSER" or "ISFULLTEXTINSTALLED" or "ISXTPSUPPORTED" or "ISPOLYBASEINSTALLED"=>new(typeof(int),"int",0),
        "COLLATION"=>new(typeof(string),"nvarchar","Japanese_CI_AS",128),
        _=>new(typeof(string),"nvarchar",null,128)
    };
    private static PropertyValue ConvertProperty(PropertyValue input,DataTypeReference dataType){
        if(dataType is UserDataTypeReference user&&string.Equals(user.Name.BaseIdentifier.Value,"sysname",StringComparison.OrdinalIgnoreCase)&&user.Name.Identifiers.Count==1){return ConvertPropertyValue(input,new(typeof(string),"nvarchar",null,128));}
        if(dataType is not SqlDataTypeReference sql){throw new NotSupportedException("Unsupported server property CAST target.");}
        var type=sql.SqlDataTypeOption switch{
            SqlDataTypeOption.Bit=>new PropertyValue(typeof(bool),"bit",null),
            SqlDataTypeOption.Int=>new PropertyValue(typeof(int),"int",null),
            SqlDataTypeOption.BigInt=>new PropertyValue(typeof(long),"bigint",null),
            SqlDataTypeOption.NVarChar or SqlDataTypeOption.NChar=>new PropertyValue(typeof(string),sql.SqlDataTypeOption==SqlDataTypeOption.NChar?"nchar":"nvarchar",null,PropertyLength(sql)),
            SqlDataTypeOption.VarChar or SqlDataTypeOption.Char=>new PropertyValue(typeof(string),sql.SqlDataTypeOption==SqlDataTypeOption.Char?"char":"varchar",null,PropertyLength(sql)),
            _=>throw new NotSupportedException("Unsupported server property CAST target: "+sql.SqlDataTypeOption)
        };
        return ConvertPropertyValue(input,type);
    }
    private static int PropertyLength(SqlDataTypeReference type){
        if(type.Parameters.Count==0){return 30;}
        if(type.Parameters.Count!=1||type.Parameters[0] is not IntegerLiteral length||!int.TryParse(length.Value,out var result)||result<1||result>4000){throw new NotSupportedException("Unsupported server property string length.");}
        return result;
    }
    private static PropertyValue ConvertPropertyValue(PropertyValue value,PropertyValue target){
        if(value.Value is null){return target with{Value=null};}
        object converted=target.Type==typeof(bool)?Convert.ToBoolean(value.Value,global::System.Globalization.CultureInfo.InvariantCulture):
            target.Type==typeof(int)?Convert.ToInt32(value.Value,global::System.Globalization.CultureInfo.InvariantCulture):
            target.Type==typeof(long)?Convert.ToInt64(value.Value,global::System.Globalization.CultureInfo.InvariantCulture):
            Convert.ToString(value.Value,global::System.Globalization.CultureInfo.InvariantCulture)!;
        if(converted is string text&&target.Length is int length&&text.Length>length){converted=text[..length];}
        return target with{Value=converted};
    }
    private static PropertyValue PropertyCaseType(IReadOnlyList<PropertyValue> values){
        var nonNull=values.Where(value=>!value.UntypedNull).ToArray();
        if(nonNull.Length==0){throw new NotSupportedException("CASE requires a typed result.");}
        if(nonNull.Any(value=>value.Type==typeof(long))){return new(typeof(long),"bigint",null);}
        if(nonNull.Any(value=>value.Type==typeof(int))){return new(typeof(int),"int",null);}
        if(nonNull.Any(value=>value.Type==typeof(bool))){return new(typeof(bool),"bit",null);}
        var sqlType=nonNull.Any(value=>value.SqlType is "nvarchar" or "nchar")?"nvarchar":"varchar";
        return new(typeof(string),sqlType,null,nonNull.Max(value=>value.Length??128));
    }
    private static bool? PropertyEqual(PropertyValue left,PropertyValue right){
        if(left.Value is null||right.Value is null){return null;}
        if(left.Type==typeof(string)&&right.Type==typeof(string)){return string.Equals((string)left.Value,(string)right.Value,StringComparison.OrdinalIgnoreCase);}
        return Convert.ToInt64(left.Value,global::System.Globalization.CultureInfo.InvariantCulture)==Convert.ToInt64(right.Value,global::System.Globalization.CultureInfo.InvariantCulture);
    }
    private bool? EvaluatePropertyPredicate(BooleanExpression expression){
        switch(expression){
            case BooleanParenthesisExpression p:return EvaluatePropertyPredicate(p.Expression);
            case BooleanNotExpression n:return !EvaluatePropertyPredicate(n.Expression);
            case BooleanIsNullExpression n:return (EvaluatePropertyExpression(n.Expression).Value is null)!=n.IsNot;
            case BooleanBinaryExpression b:{var left=EvaluatePropertyPredicate(b.FirstExpression);var right=EvaluatePropertyPredicate(b.SecondExpression);return b.BinaryExpressionType==BooleanBinaryExpressionType.And?left&right:left|right;}
            case LikePredicate like:{
                if(like.EscapeExpression is not null){throw new NotSupportedException("Unsupported server property LIKE escape.");}
                var text=EvaluatePropertyExpression(like.FirstExpression).Value as string;
                var pattern=EvaluatePropertyExpression(like.SecondExpression).Value as string;
                if(text is null||pattern is null){return null;}
                if(!pattern.EndsWith('%')||pattern[..^1].Contains('%')||pattern.Contains('_')||pattern.Contains('[')){throw new NotSupportedException("Unsupported server property LIKE pattern.");}
                var matches=text.StartsWith(pattern[..^1],StringComparison.OrdinalIgnoreCase);return like.NotDefined?!matches:matches;
            }
            case BooleanComparisonExpression c:{
                if(c.ComparisonType is not (BooleanComparisonType.Equals or BooleanComparisonType.NotEqualToBrackets or BooleanComparisonType.NotEqualToExclamation)){throw new NotSupportedException("Unsupported server property comparison.");}
                var equal=PropertyEqual(EvaluatePropertyExpression(c.FirstExpression),EvaluatePropertyExpression(c.SecondExpression));
                return c.ComparisonType==BooleanComparisonType.Equals?equal:!equal;
            }
            default:throw new NotSupportedException("Unsupported server property predicate: "+expression.GetType().Name);
        }
    }

    }

    private static string NormalizeSql(string sql){
        if(string.IsNullOrWhiteSpace(sql)){
            return string.Empty;
        }else{
            return sql.Trim();
        }
    }

    public static QueryResult EmulateScalarSelect(string sql,IDatabaseAdapter adapter){
        var parser=new TSql160Parser(true);
        using var reader=new StringReader(sql);
        var fragment=parser.Parse(reader,out _);

        var cols=new List<ColumnMetadata>();
        var rowValues=new List<object?>();

        if(fragment is TSqlScript script){
            foreach(var batch in script.Batches){
                foreach(var statement in batch.Statements){
                    if(statement is SelectStatement sel&&sel.QueryExpression is QuerySpecification qs){
                        var idx=0;
                        foreach(var se in qs.SelectElements){
                            var colName=$"Col_{idx}";
                            object? val=1;
                            if(se is SelectScalarExpression sse){
                                if(sse.ColumnName is not null){
                                    colName=sse.ColumnName.Value;
                                }else if(sse.Expression is ColumnReferenceExpression cre){
                                    colName=cre.MultiPartIdentifier.Identifiers.Last().Value;
                                }else{
                                }
                                val=EvaluateScalarExpression(sse.Expression,adapter);
                            }else{
                            }
                            var clrType=val?.GetType()??typeof(int);
                            var sqlTypeName=clrType==typeof(string)?"nvarchar":(clrType==typeof(bool)?"bit":"int");
                            cols.Add(new ColumnMetadata(colName,clrType,sqlTypeName,false,false,idx));
                            rowValues.Add(val);
                            idx++;
                        }
                        if(cols.Count>0){
                            break;
                        }else{
                        }
                    }else{
                    }
                }
                if(cols.Count>0){
                    break;
                }else{
                }
            }
        }else{
        }

        if(cols.Count==0){
            cols.Add(new ColumnMetadata("",typeof(int),"int",false,false,0));
            rowValues.Add(1);
        }else{
        }

        return new QueryResult(cols,new List<object?[]>{rowValues.ToArray()},1);
    }

    private static object? EvaluateScalarExpression(ScalarExpression expr,IDatabaseAdapter adapter){
        if(expr is IntegerLiteral il){
            return int.TryParse(il.Value,out var v)?v:1;
        }else if(expr is StringLiteral sl){
            return sl.Value;
        }else if(expr is FunctionCall fc){
            var funcName=fc.FunctionName.Value;
            if(string.Equals(funcName,"has_dbaccess",StringComparison.OrdinalIgnoreCase)){
                return 1;
            }else if(string.Equals(funcName,"suser_sname",StringComparison.OrdinalIgnoreCase)||string.Equals(funcName,"suser_name",StringComparison.OrdinalIgnoreCase)){
                return "sa";
            }else if(string.Equals(funcName,"db_name",StringComparison.OrdinalIgnoreCase)){
                return adapter.DatabaseName;
            }else if(string.Equals(funcName,"is_srvrolemember",StringComparison.OrdinalIgnoreCase)||string.Equals(funcName,"is_rolemember",StringComparison.OrdinalIgnoreCase)){
                return 1;
            }else{
                return 1;
            }
        }else if(expr is GlobalVariableExpression gve){
            var varName=gve.Name;
            if(string.Equals(varName,"@@version",StringComparison.OrdinalIgnoreCase)){
                return "Microsoft SQL Server 2022 (RTM) - 16.0.1000.6 (X64) - SeamlessDB Server 1.0";
            }else if(string.Equals(varName,"@@options",StringComparison.OrdinalIgnoreCase)){
                return 5496;
            }else{
                return 1;
            }
        }else{
            return 1;
        }
    }

    // 各カタログで同じ順序から ID を作り、世代内の参照を一致させる。
    public static bool TryCatalog(string name,IDatabaseAdapter adapter,IReadOnlyList<DatabaseViewDefinition>views,out QueryResult result)=>TryCatalog(name,adapter,views,adapter.GetRoutines(),out result,adapter.DatabaseName);
    public static bool TryCatalog(string name,IDatabaseAdapter adapter,IReadOnlyList<DatabaseViewDefinition>views,IReadOnlyList<DatabaseRoutineDefinition>routines,out QueryResult result,string? sourceDatabase=null){
        if(name.Equals("database_mirroring",StringComparison.OrdinalIgnoreCase)){result=SqlServerDatabaseOptionCatalog.Mirroring();return true;}
        var tables=adapter.GetTables();
        var typedTableCount=tables.Count(table=>table.StableObjectId is null);
        var schemas=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase){{"dbo",1},{"sys",4}};
        foreach(var schema in tables.Select(t=>t.SchemaName).Concat(views.Select(v=>v.Schema)).Concat(routines.Select(r=>r.Schema))){
            var actual=string.IsNullOrEmpty(schema)?"dbo":schema;
            if(!schemas.ContainsKey(actual)){schemas.Add(actual,schemas.Count+3);}
        }
        var rows=new List<object?[]>();
        ColumnMetadata[] columns;
        var catalogName=name.ToLowerInvariant() switch{"all_objects"=>"objects","all_views"=>"views","all_columns"=>"columns","all_sql_modules"=>"sql_modules",_=>name.ToLowerInvariant()};
        switch(catalogName){
            case "schemas":
                columns=[new ColumnMetadata("name",typeof(string),"nvarchar",false,false,0,128),Col("schema_id",typeof(int),"int",1)];
                foreach(var schema in schemas){rows.Add([schema.Key,schema.Value]);}break;
            case "tables":case "views":case "objects":
                columns=[new ColumnMetadata("name",typeof(string),"nvarchar",false,false,0,128),Col("object_id",typeof(int),"int",1),Col("schema_id",typeof(int),"int",2),new ColumnMetadata("type",typeof(string),"char",true,false,3,2),new ColumnMetadata("type_desc",typeof(string),"nvarchar",true,false,4,60),Col("is_ms_shipped",typeof(bool),"bit",5),Col("is_memory_optimized",typeof(bool),"bit",6),Col("is_filetable",typeof(bool),"bit",7),Col("temporal_type",typeof(byte),"tinyint",8),Col("ledger_type",typeof(byte),"tinyint",9),Col("is_external",typeof(bool),"bit",10),Col("is_node",typeof(bool),"bit",11),Col("is_edge",typeof(bool),"bit",12),Col("ledger_view_type",typeof(byte),"tinyint",13)];
                if(catalogName!="views"){for(var i=0;i<tables.Count;i++){var t=tables[i];rows.Add([t.TableName,TableId(i),schemas[string.IsNullOrEmpty(t.SchemaName)?"dbo":t.SchemaName],"U ","USER_TABLE",false,false,false,(byte)0,(byte)0,false,false,false,(byte)0]);}}
                if(catalogName!="tables"){for(var i=0;i<views.Count;i++){var v=views[i];rows.Add([v.Name,ViewId(i),schemas[string.IsNullOrEmpty(v.Schema)?"dbo":v.Schema],"V ","VIEW",false,false,false,(byte)0,(byte)0,false,false,false,(byte)0]);}}
                if(catalogName=="objects"){for(var i=0;i<routines.Count;i++){var r=routines[i];var (type,description)=RoutineType(r);rows.Add([r.Name,RoutineId(i),schemas[string.IsNullOrEmpty(r.Schema)?"dbo":r.Schema],type,description,false,false,false,(byte)0,(byte)0,false,false,false,(byte)0]);}}break;
            case "procedures":
                columns=[new ColumnMetadata("name",typeof(string),"nvarchar",false,false,0,128),Col("object_id",typeof(int),"int",1),Col("schema_id",typeof(int),"int",2),new ColumnMetadata("type",typeof(string),"char",true,false,3,2),new ColumnMetadata("type_desc",typeof(string),"nvarchar",true,false,4,60),Col("is_ms_shipped",typeof(bool),"bit",5)];
                for(var i=0;i<routines.Count;i++){var r=routines[i];if((r.Catalog?.Kind??r.GeneratedMethod?.Kind)==RoutineKind.StoredProcedure){var (type,description)=RoutineType(r);rows.Add([r.Name,RoutineId(i),schemas[string.IsNullOrEmpty(r.Schema)?"dbo":r.Schema],type,description,false]);}}break;
            case "sql_modules":
                columns=[Col("object_id",typeof(int),"int",0),new ColumnMetadata("definition",typeof(string),"nvarchar",true,false,1,-1),new ColumnMetadata("is_schema_bound",typeof(bool),"bit",true,false,2),Col("uses_native_compilation",typeof(bool),"bit",3)];
                for(var i=0;i<views.Count;i++){rows.Add([ViewId(i),views[i].Definition,SqlServerObjectCatalogProfile.IsSchemaBound(views[i].Definition),false]);}
                for(var i=0;i<routines.Count;i++){rows.Add([RoutineId(i),routines[i].Definition,null,false]);}break;
            case "system_sql_modules":
                columns=[Col("object_id",typeof(int),"int",0),new ColumnMetadata("definition",typeof(string),"nvarchar",true,false,1,-1)];break;
            case "extended_properties":
                columns=[Col("major_id",typeof(int),"int",0),Col("minor_id",typeof(int),"int",1),Col("class",typeof(byte),"tinyint",2),new ColumnMetadata("name",typeof(string),"nvarchar",false,false,3,128)];break;
            case "data_spaces":
                columns=[new ColumnMetadata("type",typeof(string),"char",false,false,0,2),Col("data_space_id",typeof(int),"int",1)];break;
            case "parameters":
                columns=[Col("object_id",typeof(int),"int",0),new ColumnMetadata("name",typeof(string),"nvarchar",true,false,1,128),Col("parameter_id",typeof(int),"int",2),new ColumnMetadata("system_type_id",typeof(byte),"tinyint",true,false,3),new ColumnMetadata("user_type_id",typeof(int),"int",true,false,4),new ColumnMetadata("max_length",typeof(short),"smallint",true,false,5),new ColumnMetadata("precision",typeof(byte),"tinyint",true,false,6),new ColumnMetadata("scale",typeof(byte),"tinyint",true,false,7),Col("is_output",typeof(bool),"bit",8)];
                for(var i=0;i<routines.Count;i++){
                    var routine=routines[i].Catalog;if(routine is null){continue;}
                    if(routine.Kind==RoutineKind.ScalarFunction&&routine.ReturnType is not null){AddParameter(RoutineId(i),"",0,routine.ReturnType,false);}
                    foreach(var parameter in routine.Parameters){AddParameter(RoutineId(i),parameter.Name,parameter.Ordinal,parameter.Type,parameter.IsOutput);}
                }break;
            case "generated_methods":
                columns=[Col("object_id",typeof(int),"int",0),Col("source_database_name",typeof(string),"nvarchar",1),Col("source_schema_name",typeof(string),"nvarchar",2),Col("source_name",typeof(string),"nvarchar",3),new ColumnMetadata("routine_kind",typeof(string),"nvarchar",true,false,4),new ColumnMetadata("declaring_type_name",typeof(string),"nvarchar",true,false,5),new ColumnMetadata("method_name",typeof(string),"nvarchar",true,false,6),new ColumnMetadata("source_file",typeof(string),"nvarchar",true,false,7),new ColumnMetadata("csharp_source",typeof(string),"nvarchar",true,false,8,-1),new ColumnMetadata("is_stub",typeof(bool),"bit",true,false,9)];
                for(var i=0;i<routines.Count;i++){var r=routines[i];var method=r.GeneratedMethod;rows.Add([RoutineId(i),sourceDatabase??adapter.DatabaseName,r.Schema,r.Name,(r.Catalog?.Kind??method?.Kind)?.ToString(),method?.DeclaringTypeName,method?.MethodName,method?.SourceFile,method?.CSharpSource,method?.IsStub]);}break;
            case "columns":
                columns=[Col("object_id",typeof(int),"int",0),Col("name",typeof(string),"nvarchar",1),Col("column_id",typeof(int),"int",2),Col("system_type_id",typeof(byte),"tinyint",3),Col("max_length",typeof(short),"smallint",4),Col("precision",typeof(byte),"tinyint",5),Col("scale",typeof(byte),"tinyint",6),Col("is_nullable",typeof(bool),"bit",7),Col("user_type_id",typeof(int),"int",8),new ColumnMetadata("collation_name",typeof(string),"nvarchar",true,false,9),Col("is_sparse",typeof(bool),"bit",10)];
                for(var i=0;i<tables.Count;i++){foreach(var c in tables[i].Columns){AddColumn(TableId(i),c);}}
                for(var i=0;i<views.Count;i++){foreach(var c in views[i].Columns){var source=views[i].SourceColumns?.FirstOrDefault(x=>x.Ordinal==c.Ordinal)?.Type;AddColumn(ViewId(i),new(c.Name,c.Type.ClrType,c.Type.Name,c.IsNullable,false,c.Ordinal,c.Type.Length,c.Type.Precision,c.Type.Scale,c.Type.Collation,source?.SourceMaximumLength,source?.SourcePrecision,source?.SourceScale,sourceSqlTypeName:source?.SqlTypeName));}}
                for(var i=0;i<routines.Count;i++){
                    var routine=routines[i].Catalog;if(routine?.Kind!=RoutineKind.TableFunction){continue;}
                    foreach(var c in routine.ResultColumns){var type=ComparisonHost.ToSqlType(c.Type);AddColumn(RoutineId(i),new(c.Name,type.ClrType,type.Name,c.Type.IsNullable,false,c.Ordinal,type.Length,type.Precision,type.Scale,type.Collation,c.Type.SourceMaximumLength,c.Type.SourcePrecision,c.Type.SourceScale,sourceSqlTypeName:c.Type.SqlTypeName));}
                }break;
            case "types":
                columns=[Col("name",typeof(string),"nvarchar",0),Col("system_type_id",typeof(byte),"tinyint",1),Col("user_type_id",typeof(int),"int",2),Col("schema_id",typeof(int),"int",3),Col("max_length",typeof(short),"smallint",4),Col("precision",typeof(byte),"tinyint",5),Col("scale",typeof(byte),"tinyint",6),Col("is_nullable",typeof(bool),"bit",7),Col("is_user_defined",typeof(bool),"bit",8),Col("is_assembly_type",typeof(bool),"bit",9)];
                foreach(var type in TypeShapes){rows.Add([type.Name,type.Id,UserTypeId(type.Name,type.Id),4,type.Length,type.Precision,type.Scale,true,false,type.Id==240]);}
                rows.Add(["sysname",(byte)231,256,4,(short)256,(byte)0,(byte)0,false,false,false]);break;
            case "indexes":
                columns=[Col("object_id",typeof(int),"int",0),new ColumnMetadata("name",typeof(string),"nvarchar",true,false,1,128),Col("index_id",typeof(int),"int",2),Col("type",typeof(byte),"tinyint",3),Col("type_desc",typeof(string),"nvarchar",4),Col("is_unique",typeof(bool),"bit",5),Col("is_primary_key",typeof(bool),"bit",6),Col("data_space_id",typeof(int),"int",7),Col("is_unique_constraint",typeof(bool),"bit",8),Col("is_hypothetical",typeof(bool),"bit",9)];
                for(var i=0;i<tables.Count;i++){var key=tables[i].PrimaryKey;if(key is not null){var type=key.IndexType;string? description=type switch{1=>"CLUSTERED",2=>"NONCLUSTERED",_=>null};rows.Add([TableId(i),key.Name,1,type,description,true,true,0,false,false]);}else{rows.Add([TableId(i),null,0,(byte)0,"HEAP",false,false,0,false,false]);}}break;
            case "index_columns":
                columns=[Col("object_id",typeof(int),"int",0),Col("index_id",typeof(int),"int",1),Col("index_column_id",typeof(int),"int",2),Col("column_id",typeof(int),"int",3),Col("key_ordinal",typeof(byte),"tinyint",4),Col("is_descending_key",typeof(bool),"bit",5)];
                for(var i=0;i<tables.Count;i++){var table=tables[i];var key=table.PrimaryKey;if(key is null){continue;}for(var j=0;j<key.Columns.Count;j++){var column=table.Columns.FirstOrDefault(c=>string.Equals(c.Name,key.Columns[j].Name,StringComparison.OrdinalIgnoreCase))??throw new InvalidDataException($"Primary key column missing: {key.Columns[j].Name}.");rows.Add([TableId(i),1,j+1,column.Ordinal+1,(byte)(j+1),key.Columns[j].IsDescending]);}}break;
            default:result=QueryResult.Empty;return false;
        }
        result=new(columns,rows,rows.Count);return true;
        int TableId(int index)=>tables[index].StableObjectId??(100+index);
        int ViewId(int index)=>100+typedTableCount+index;
        int RoutineId(int index)=>100+typedTableCount+views.Count+index;
        void AddParameter(int objectId,string parameterName,int ordinal,DatabaseType dataType,bool output){
            var shape=TypeShapes.FirstOrDefault(t=>t.Name.Equals(dataType.SqlTypeName,StringComparison.OrdinalIgnoreCase));
            if(shape.Name is null){rows.Add([objectId,parameterName,ordinal,null,null,dataType.SourceMaximumLength,dataType.SourcePrecision,dataType.SourceScale,output]);return;}
            short length=shape.Length;
            if(dataType.MaximumLength is int max&&shape.Name is "nvarchar" or "nchar" or "varchar" or "char" or "varbinary" or "binary"){
                if(max<0){length=-1;}else{length=checked((short)(max*(shape.Name is "nvarchar" or "nchar"?2:1)));}
            }
            rows.Add([objectId,parameterName,ordinal,shape.Id,UserTypeId(shape.Name,shape.Id),dataType.SourceMaximumLength??length,dataType.SourcePrecision??dataType.Precision??shape.Precision,dataType.SourceScale??dataType.Scale??shape.Scale,output]);
        }
        void AddColumn(int objectId,ColumnMetadata c){
            var type=TypeShapes.FirstOrDefault(t=>t.Name.Equals(c.SourceSqlTypeName??c.SqlTypeName,StringComparison.OrdinalIgnoreCase));
            if(type.Name is null){throw new NotSupportedException($"SQL type '{c.SourceSqlTypeName??c.SqlTypeName}' is not supported by sys.columns.");}
            var length=type.Length;var precision=c.Precision??type.Precision;var scale=c.Scale??type.Scale;
            if(c.MaximumLength is int max&&(type.Name is "nvarchar" or "nchar" or "varchar" or "char" or "varbinary" or "binary")){
                if(max<0){length=-1;}else{var bytes=max;if(type.Name is "nvarchar" or "nchar"){bytes=checked(bytes*2);}length=checked((short)bytes);}
            }
            if(type.Name is "decimal" or "numeric"){length=(short)(precision<=9?5:precision<=19?9:precision<=28?13:17);}
            rows.Add([objectId,c.Name,c.Ordinal+1,type.Id,c.SourceMaximumLength??length,c.SourcePrecision??precision,c.SourceScale??scale,c.IsNullable,UserTypeId(type.Name,type.Id),c.Collation,false]);
        }
    }
    private static (string? Type,string? Description) RoutineType(DatabaseRoutineDefinition routine){
        var sourceType=routine.Catalog?.SourceObjectType;
        if(sourceType is not null){return sourceType.ToUpperInvariant() switch{
            "P"=>("P ","SQL_STORED_PROCEDURE"),"PC"=>("PC","CLR_STORED_PROCEDURE"),"FN"=>("FN","SQL_SCALAR_FUNCTION"),"FS"=>("FS","CLR_SCALAR_FUNCTION"),"IF"=>("IF","SQL_INLINE_TABLE_VALUED_FUNCTION"),"TF"=>("TF","SQL_TABLE_VALUED_FUNCTION"),"FT"=>("FT","CLR_TABLE_VALUED_FUNCTION"),_=>(null,null)
        };}
        return (routine.Catalog?.Kind??routine.GeneratedMethod?.Kind) switch{
            RoutineKind.StoredProcedure=>("P ","SQL_STORED_PROCEDURE"),RoutineKind.ScalarFunction=>("FN","SQL_SCALAR_FUNCTION"),RoutineKind.TableFunction=>("TF","SQL_TABLE_VALUED_FUNCTION"),_=>(null,null)
        };
    }
    private static int UserTypeId(string name,byte id)=>name switch{"hierarchyid"=>128,"geometry"=>129,"geography"=>130,_=>id};
    private static ColumnMetadata Col(string name,Type type,string sql,int ordinal)=>new(name,type,sql,false,false,ordinal);
    private static readonly (string Name,byte Id,short Length,byte Precision,byte Scale)[] TypeShapes=[
        ("tinyint",48,1,3,0),("smallint",52,2,5,0),("int",56,4,10,0),("bigint",127,8,19,0),("bit",104,1,1,0),
        ("char",175,8000,0,0),("varchar",167,8000,0,0),("nchar",239,8000,0,0),("nvarchar",231,8000,0,0),
        ("binary",173,8000,0,0),("varbinary",165,8000,0,0),("decimal",106,17,18,0),("numeric",108,17,18,0),
        ("float",62,8,53,0),("real",59,4,24,0),("date",40,3,10,0),("time",41,5,16,7),("datetime2",42,8,27,7),
        ("datetimeoffset",43,10,34,7),("smalldatetime",58,4,16,0),("datetime",61,8,23,3),("uniqueidentifier",36,16,0,0),
        ("money",60,8,19,4),("smallmoney",122,4,10,4),("xml",241,-1,0,0),("text",35,16,0,0),("ntext",99,16,0,0),("image",34,16,0,0),
        ("hierarchyid",240,892,0,0),("geometry",240,-1,0,0),("geography",240,-1,0,0)
    ];

    private static byte GetSystemTypeId(Type clrType){
        var u=Nullable.GetUnderlyingType(clrType)??clrType;
        if(u==typeof(int)) return 56;
        if(u==typeof(long)) return 127;
        if(u==typeof(short)) return 52;
        if(u==typeof(byte)) return 48;
        if(u==typeof(bool)) return 104;
        if(u==typeof(string)) return 231;
        if(u==typeof(DateTime)) return 42;
        if(u==typeof(double)) return 62;
        if(u==typeof(float)) return 59;
        return 231;
    }
}
