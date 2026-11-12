using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Query;
using Seamless.Execution;
using Seamless.Security;

namespace Seamless.Database.Gateway.Tds;

public sealed partial class TdsSession{
    private static readonly SqlType MetadataName=new("nvarchar",typeof(string),128);
    private static readonly SqlParameter[] KeyArguments=[new("@table_name",MetadataName),new("@table_owner",MetadataName),new("@table_qualifier",MetadataName)];
    private static readonly SqlParameter[] TableArguments=[..KeyArguments,new("@table_type",MetadataName)];
    private static readonly SqlType MetadataSmallInt=new("smallint",typeof(short));
    private static readonly SqlType MetadataVarchar=new("varchar",typeof(string),254);
    private static readonly (string Name,SqlType Type)[] ColumnProcedureShape=[
        ("TABLE_QUALIFIER",MetadataName),("TABLE_OWNER",MetadataName),("TABLE_NAME",MetadataName),("COLUMN_NAME",MetadataName),
        ("DATA_TYPE",MetadataSmallInt),("TYPE_NAME",MetadataName),("PRECISION",SqlType.Int),("LENGTH",SqlType.Int),
        ("SCALE",MetadataSmallInt),("RADIX",MetadataSmallInt),("NULLABLE",MetadataSmallInt),("REMARKS",MetadataVarchar),
        ("COLUMN_DEF",new("nvarchar",typeof(string),4000)),("SQL_DATA_TYPE",MetadataSmallInt),("SQL_DATETIME_SUB",MetadataSmallInt),
        ("CHAR_OCTET_LENGTH",SqlType.Int),("ORDINAL_POSITION",SqlType.Int),("IS_NULLABLE",MetadataVarchar),
        ("SS_IS_SPARSE",MetadataSmallInt),("SS_IS_COLUMN_SET",MetadataSmallInt),("SS_IS_COMPUTED",MetadataSmallInt),("SS_IS_IDENTITY",MetadataSmallInt),
        ("SS_UDT_CATALOG_NAME",MetadataName),("SS_UDT_SCHEMA_NAME",MetadataName),("SS_UDT_ASSEMBLY_TYPE_NAME",SqlType.String),
        ("SS_XML_SCHEMACOLLECTION_CATALOG_NAME",MetadataName),("SS_XML_SCHEMACOLLECTION_SCHEMA_NAME",MetadataName),
        ("SS_XML_SCHEMACOLLECTION_NAME",MetadataName),("SS_DATA_TYPE",new("tinyint",typeof(byte)))];
    private static readonly SqlParameter[] ColumnProcedureArguments=[..KeyArguments,new("@column_name",MetadataName),
        new("@NameScope",SqlType.Int),new("@ODBCVer",SqlType.Int)];

    private IReadOnlyList<QueryResult>? ExecuteJdbcColumnMetadata(string sql,IReadOnlyList<SqlParameter> parameters,
        IReadOnlyDictionary<string,object?>? values,AuthorizationLease authorization,bool metadataOnly=false){
        if(sql.Length>16384||!sql.AsSpan().TrimStart().StartsWith("DECLARE",StringComparison.OrdinalIgnoreCase)){return null;}
        var parsed=new TSql170Parser(true).Parse(new StringReader(sql),out var errors) as TSqlScript;
        if(errors.Count!=0||parsed?.Batches is not [{Statements:[
            DeclareTableVariableStatement{Body:{AsDefined:false,VariableName:{} variable,
                Definition:{TableConstraints.Count:0,Indexes.Count:0,SystemTimePeriod:null} definition}},
            InsertStatement{WithCtesAndXmlNamespaces:null,OptimizerHints.Count:0,InsertSpecification:{Columns.Count:0,TopRowFilter:null,
                OutputClause:null,OutputIntoClause:null,Target:VariableTableReference{Alias:null,ForPath:false,Variable:{} destination},InsertSource:ExecuteInsertSource{Execute:{} execute}}},
            SelectStatement{Into:null,WithCtesAndXmlNamespaces:null,ComputeClauses.Count:0,OptimizerHints.Count:0,
                QueryExpression:QuerySpecification{FromClause.TableReferences:[VariableTableReference{Alias:null,ForPath:false} source]}} projection]}]){return null;}
        if(!destination.Name.Equals(variable.Value,StringComparison.OrdinalIgnoreCase)||!source.Variable.Name.Equals(variable.Value,StringComparison.OrdinalIgnoreCase)
            ||!MatchesColumnProcedureShape(sql,definition)){return null;}
        var (target,arguments)=this.ReadProcedureInvocation(new ExecuteStatement{ExecuteSpecification=execute});
        if(target.Name.ToLowerInvariant() is not("sp_columns_170" or "sp_columns_100")||target.Schema.ToLowerInvariant() is not("" or "dbo" or "sys")){return null;}
        if(arguments.Count!=ColumnProcedureArguments.Length||arguments.Any(static argument=>argument.IsOutput)){throw ArgumentsDoNotMatch();}
        var registry=this.sqlEngine.Databases;
        var policy=this.Protection!.Policy;
        if(!policy.CanDiscover(authorization,target.Database)){throw new SqlQueryException("The identity is not permitted to discover this database.",229);}
        var sourceName=new SqlObjectName(target.Database,"dbo","__jdbc_column_metadata");
        var columns=ColumnProcedureShape.Select((column,index)=>new SqlColumn(column.Name,column.Type,true,sourceName,index)).ToArray();
        IEnumerable<object?[]> rows=[];
        var local=new ColumnProjectionCatalog(new(sourceName,columns,_=>rows),registry.GetDatabaseCollation(target.Database));
        var select=sql.Substring(projection.StartOffset,source.StartOffset-projection.StartOffset)+"dbo.__jdbc_column_metadata"+
            sql.Substring(source.StartOffset+source.FragmentLength,projection.StartOffset+projection.FragmentLength-source.StartOffset-source.FragmentLength);
        var plan=new SqlQueryEngine(local,cacheCapacity:0).Prepare(select,target.Database);
        var resultColumns=plan.Columns.Select((column,index)=>new ColumnMetadata(column.Name,column.Type.ClrType,column.Type.Name,column.IsNullable,false,index,
            column.Type.Length,column.Type.Precision,column.Type.Scale,column.Type.Collation)).ToArray();
        if(metadataOnly){return [new QueryResult(resultColumns,[],-1){StatementKind=SqlCommandKind.Select}];}
        var options=registry.GatewayTransactions?.QueryExecutionOptions(this.sessionContext.CancellationToken)??new ExecutionOptions{CancellationToken=this.sessionContext.CancellationToken};
        var budget=options.ParentBudget?.CreateChild(options.MemoryLimitBytes,options.TemporaryStorageLimitBytes)
            ??new ResourceBudget(options.MemoryLimitBytes,options.TemporaryStorageLimitBytes);
        var memory=budget.ReserveMemory(64);
        try{
            rows=this.ResolveColumnProcedureRows(target,arguments,parameters,values,authorization,memory);
            IReadOnlyList<QueryResult> results=[new QueryResult(resultColumns,plan.Execute(options:options with{ParentBudget=budget}).Rows,-1){StatementKind=SqlCommandKind.Select}];
            this.sessionContext.RetainResults(memory);
            return results;
        }catch{
            memory.Dispose();
            throw;
        }
    }

    private static bool MatchesColumnProcedureShape(string sql,TableDefinition definition){
        if(definition.ColumnDefinitions.Count!=ColumnProcedureShape.Length){return false;}
        for(var index=0;index<ColumnProcedureShape.Length;index++){
            var actual=definition.ColumnDefinitions[index];var expected=ColumnProcedureShape[index];
            if(!actual.ColumnIdentifier.Value.Equals(expected.Name,StringComparison.OrdinalIgnoreCase)||actual.DataType is not{} type
                ||actual.ComputedColumnExpression is not null||actual.DefaultConstraint is not null||actual.IdentityOptions is not null||actual.Constraints.Count!=0){return false;}
            SqlType declared;
            if(type is UserDataTypeReference{Name.Identifiers:[{Value:{} name}]}&&name.Equals("sysname",StringComparison.OrdinalIgnoreCase)){declared=MetadataName;}
            else if(type is SqlDataTypeReference){declared=ParameterDeclarations.Parse("@column "+sql.Substring(type.StartOffset,type.FragmentLength)).Single().Type;}
            else{return false;}
            if(declared!=expected.Type){return false;}
        }
        return true;
    }

    private IEnumerable<object?[]> ResolveColumnProcedureRows(SqlObjectName target,IList<ExecuteParameter> arguments,IReadOnlyList<SqlParameter> parameters,
        IReadOnlyDictionary<string,object?>? values,AuthorizationLease authorization,ResourceReservation memory){
        var supplied=BindNamedOrPositionalValues(ColumnProcedureArguments,arguments.Select(argument=>(argument.Variable?.Name??string.Empty,
            ReadProcedureArgument(argument.ParameterValue,parameters,values),argument.IsOutput)));
        if(SqlScalarConversion.Assign(supplied["@NameScope"],SqlType.Int) is not 2
            ||SqlScalarConversion.Assign(supplied["@ODBCVer"],SqlType.Int) is not 3){throw ArgumentsDoNotMatch();}
        var table=ReadPattern("@table_name");var schema=ReadPattern("@table_owner");var catalog=ReadPattern("@table_qualifier");var column=ReadPattern("@column_name");
        if(catalog is not null&&!catalog.Equals(target.Database,StringComparison.OrdinalIgnoreCase)){return [];}
        return this.ReadMetadataDatabase(target.Database,registration=>{
            var rows=new List<object?[]>();
            foreach(var visible in VisibleTables(registration,authorization)){
                this.sessionContext.CancellationToken.ThrowIfCancellationRequested();
                if(!Matches(visible.SchemaName,schema,registration)||!Matches(visible.TableName,table,registration)){continue;}
                foreach(var field in visible.Columns){
                    if(Matches(field.Name,column,registration)){AddRow(visible.SchemaName,visible.TableName,field);}
                }
            }
            foreach(var view in VisibleViews(registration,authorization)){
                this.sessionContext.CancellationToken.ThrowIfCancellationRequested();
                if(!Matches(view.Schema,schema,registration)||!Matches(view.Name,table,registration)){continue;}
                for(var index=0;index<view.Columns.Count;index++){
                    var field=view.Columns[index];var type=field.Type;
                    if(Matches(field.Name,column,registration)){AddRow(view.Schema,view.Name,
                        new(field.Name,type.ClrType,type.Name,field.IsNullable,false,index,type.Length,type.Precision,type.Scale,type.Collation));}
                }
            }
            return rows;
            void AddRow(string schemaName,string tableName,ColumnMetadata field){
                this.sessionContext.CancellationToken.ThrowIfCancellationRequested();
                // 行配列・ボックス値とリストの拡張分を、列候補を保持する前に計上する。
                var rowBytes=64L+ColumnProcedureShape.Length*(IntPtr.Size+32L);
                memory.Resize(checked(memory.Bytes+rowBytes));
                rows.Add(ColumnProcedureRow(registration.Name,schemaName,tableName,field));
            }
        });
        string? ReadPattern(string name)=>supplied[name] switch{null=>null,string pattern when pattern.Length<=1024=>pattern,_=>throw ArgumentsDoNotMatch()};
    }

    private static object?[] ColumnProcedureRow(string database,string schema,string table,ColumnMetadata field){
        var name=field.SqlTypeName.ToLowerInvariant();
        var scale=field.Scale;
        if(name is "time" or "datetime2" or "datetimeoffset"){scale??=7;}
        var (type,size,radix,octets,wire)=ColumnType(field,name,scale);
        short nullable=field.IsNullable?(short)1:(short)0;
        // 発見権限は式や既定値のソースを取得する権限と分ける。
        return [database,schema,table,field.Name,type,field.SqlTypeName,size,octets,(short?)scale,radix,nullable,null,null,
            type,null,octets,checked(field.Ordinal+1),field.IsNullable?"YES":"NO",(short)0,(short)0,
            field.ComputedDefinition is null?(short)0:(short)1,field.IsIdentity?(short)1:(short)0,null,null,null,null,null,null,wire];
    }

    private static (short Type,int? Size,short? Radix,int? Octets,byte? Wire) ColumnType(ColumnMetadata field,string name,byte? scale){
        var length=field.MaximumLength;
        // The ODBC column catalog represents MAX with SQL_SS_LENGTH_UNLIMITED (0).
        if(length==-1&&name is "varchar" or "nvarchar" or "varbinary"){length=0;}
        var fractionalLength=0;
        if(scale is >0){fractionalLength=scale.Value+1;}
        return name switch{
            "bit"=>(-7,1,2,null,104),"tinyint"=>(-6,3,10,null,48),"smallint"=>(5,5,10,null,52),"int"=>(4,10,10,null,56),"bigint"=>(-5,19,10,null,127),
            "decimal"=>(3,field.Precision,10,null,106),"numeric"=>(2,field.Precision,10,null,108),
            "money"=>(3,19,10,null,60),"smallmoney"=>(3,10,10,null,122),"real"=>(7,24,2,null,59),"float"=>(8,53,2,null,62),
            "char"=>(1,length,null,length,175),"varchar"=>(12,length,null,length,167),"text"=>(-1,length,null,length,35),
            "nchar"=>(-15,length,null,UnicodeBytes(length),239),"nvarchar" or "sysname"=>(-9,length,null,UnicodeBytes(length),231),"ntext"=>(-16,length,null,UnicodeBytes(length),99),
            "binary"=>(-2,length,null,length,173),"varbinary"=>(-3,length,null,length,165),"image"=>(-4,length,null,length,34),"rowversion" or "timestamp"=>(-2,8,null,8,189),
            "uniqueidentifier"=>(-145,36,null,null,36),"date"=>(91,10,null,null,40),"time"=>(-154,8+fractionalLength,null,null,41),
            "datetime"=>(93,23,null,null,61),"smalldatetime"=>(93,19,null,null,58),"datetime2"=>(93,19+fractionalLength,null,null,42),
            "datetimeoffset"=>(-155,26+fractionalLength,null,null,43),"xml"=>(-152,length,null,null,241),_=>(1111,length,null,null,null)
        };
        static int? UnicodeBytes(int? length)=>length is >=0 and <=int.MaxValue/2?length*2:length;
    }

    private sealed class ColumnProjectionCatalog(SqlTable table,string? collation):ISqlCatalog{
        public long Generation=>0;
        public SqlTable? FindTable(SqlObjectName name)=>name==table.Name?table:null;
        public string? GetDatabaseCollation(string database)=>collation;
    }
}
