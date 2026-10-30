using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Query;
using Seamless.Database.Rdbms;

namespace TestSeamless;

internal static class GeneratedDatabaseAcceptance{
    public static async Task ValidateAsync(string directory,ComparisonGeneration generation,string source,ComparisonHost direct){
        var checks=new List<object>();
        var database=generation.Databases.SingleOrDefault(item=>item.Name.Equals("AdventureWorks2016",StringComparison.OrdinalIgnoreCase));
        var reportPath=Path.Combine(directory,"generated-database-acceptance.json");
        async Task Report(string status,string?reason=null){await File.WriteAllTextAsync(reportPath,JsonSerializer.Serialize(new{Generation=generation.Id,Database="AdventureWorks2016",Status=status,Reason=reason,Checks=checks},new JsonSerializerOptions{WriteIndented=true}));}
        if(database is null){await Report("Skipped","AdventureWorks2016 is absent from this generation.");return;}
        try{
            var sourceConnection=new SqlConnectionStringBuilder(source){InitialCatalog=database.Name}.ConnectionString;
            var targetConnection=ComparisonRunner.Target(database.Name,14343);
            var sqlFile=Path.Combine(AppContext.BaseDirectory,"Cases","AdventureWorks","Basic.sql");
            var statements=new List<string>();var current=new StringBuilder();
            foreach(var line in await File.ReadAllLinesAsync(sqlFile)){
                if(line.Trim().Equals("GO",StringComparison.OrdinalIgnoreCase)){if(!string.IsNullOrWhiteSpace(current.ToString())){statements.Add(current.ToString());}current.Clear();}else{current.AppendLine(line);}
            }
            if(!string.IsNullOrWhiteSpace(current.ToString())){statements.Add(current.ToString());}
            if(statements.Count==5){
                var use=statements[0];SqlConsole.ValidateSourceSql(database.Name,use);
                var parser=new Microsoft.SqlServer.TransactSql.ScriptDom.TSql180Parser(true);
                var script=parser.Parse(new StringReader(use),out var errors) as Microsoft.SqlServer.TransactSql.ScriptDom.TSqlScript;
                if(errors.Count!=0||script is null||script.Batches.Count!=1||script.Batches[0].Statements.Count!=1||script.Batches[0].Statements[0] is not Microsoft.SqlServer.TransactSql.ScriptDom.UseStatement){throw new InvalidDataException("The initial batch must contain only USE for the selected database.");}
                statements.RemoveAt(0);
            }
            if(statements.Count!=4){throw new InvalidDataException("AdventureWorks Basic.sql must contain exactly four cases.");}
            for(var index=0;index<statements.Count;index++){
                var id=$"AW-BASIC-{index+1:00}";var sql=statements[index];
                try{
                    SqlConsole.ValidateSourceSql(database.Name,sql);
                    ComparisonRunner.CompareSchema(sourceConnection,targetConnection,sql);
                    var test=new ComparisonCase(id,database.Name,sql,null,Ordered:true,TopOrderUnique:true);
                    var result=ComparisonRunner.CompareResults(test,ComparisonRunner.ReadSql(sourceConnection,sql),ComparisonRunner.ReadSql(targetConnection,sql),Path.Combine(directory,"verification-work"));
                    checks.Add(new{Id=id,Status=result.Equal?"Passed":"Failed",result.Rows,result.Reason});await Report("InProgress");
                    if(!result.Equal){throw new InvalidDataException(id+": "+result.Reason);}
                }catch(Exception error){checks.Add(new{Id=id,Status="Failed",Reason=error.Message});throw;}
            }
            var compiled=database.Routines.Where(binding=>binding.Kind==RoutineKind.ScalarFunction&&!binding.IsStub).ToArray();
            if(compiled.Length==0){throw new InvalidDataException("No compiled scalar routine is present in the AdventureWorks2016 generation.");}
            using var sourceDb=new SqlConnection(sourceConnection);using var targetDb=new SqlConnection(targetConnection);await sourceDb.OpenAsync();await targetDb.OpenAsync();
            foreach(var binding in compiled){
                var id=binding.SourceSchemaName+"."+binding.SourceName;
                try{
                    var routine=database.Catalog.Schemas.Single(schema=>schema.Name==binding.SourceSchemaName).Routines.Single(item=>item.Name==binding.SourceName);
                    if(routine.Parameters.Count!=1||routine.Parameters[0].Type.Kind!=DatabaseTypeKind.Byte){throw new InvalidDataException("Compiled routine is outside the tinyint acceptance profile: "+id);}
                    var resolved=direct.Databases.FindRoutine(new(database.Name,binding.SourceSchemaName,binding.SourceName));
                    var native=resolved?.Native??throw new InvalidDataException("Direct generated delegate is unavailable: "+id);
                    var function=ComparisonRunner.QuoteSqlName(binding.SourceSchemaName)+"."+ComparisonRunner.QuoteSqlName(binding.SourceName);
                    var sample="SELECT "+function+"(CAST(0 AS tinyint)) AS [value]";
                    ComparisonRunner.CompareSchema(sourceConnection,targetConnection,sample);
                    for(var number=-1;number<=255;number++){
                        string argument;object? nativeArgument;
                        if(number<0){argument="CAST(NULL AS tinyint)";nativeArgument=null;}else{argument=$"CAST({number} AS tinyint)";nativeArgument=(byte)number;}
                        var query="SELECT "+function+"("+argument+") AS [value]";
                        using var left=sourceDb.CreateCommand();left.CommandText=query;left.CommandTimeout=0;
                        using var right=targetDb.CreateCommand();right.CommandText=query;right.CommandTimeout=0;
                        var expected=await ReadScalarAsync(left);var actual=await ReadScalarAsync(right);var directValue=native.Invoke([nativeArgument]);
                        if(expected is DBNull){expected=null;}if(actual is DBNull){actual=null;}if(directValue is DBNull){directValue=null;}
                        if(!Equals(expected,actual)||!Equals(expected,directValue)){throw new InvalidDataException($"{id} input {argument}: SQL Server={Display(expected)}, SeamlessSQL={Display(actual)}, direct={Display(directValue)}");}
                    }
                    checks.Add(new{Id="Routine:"+id,Status="Passed",Inputs=257,DirectCopy=true});await Report("InProgress");
                }catch(Exception error){checks.Add(new{Id="Routine:"+id,Status="Failed",Reason=error.Message});throw;}
            }
            try{VerifyCatalog(database,targetConnection);checks.Add(new{Id="RoutineCatalog",Status="Passed",Routines=database.Routines.Count});}
            catch(Exception error){checks.Add(new{Id="RoutineCatalog",Status="Failed",Reason=error.Message});throw;}
            await Report("Passed");
        }catch(Exception error){await Report("Failed",error.Message);throw;}
    }
    private static async Task<object?>ReadScalarAsync(SqlCommand command){
        using var reader=await command.ExecuteReaderAsync();
        if(reader.FieldCount!=1||!await reader.ReadAsync()){throw new InvalidDataException("Scalar query must return exactly one column and row.");}
        object?value=reader.IsDBNull(0)?null:reader.GetValue(0);
        if(await reader.ReadAsync()||await reader.NextResultAsync()){throw new InvalidDataException("Scalar query returned additional rows or results.");}
        return value;
    }
    private static string Display(object?value)=>value is null?"NULL":Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture)??"NULL";
    private static void VerifyCatalog(ComparisonDatabase database,string connection){
        var schemas=Read(connection,"sys.schemas");var objects=Read(connection,"sys.objects");var procedures=Read(connection,"sys.procedures");
        var modules=Read(connection,"sys.sql_modules");var parameters=Read(connection,"sys.parameters");var columns=Read(connection,"sys.columns");var methods=Read(connection,"seamless.generated_methods");var types=Read(connection,"sys.types");
        var schemaNames=schemas.ToDictionary(row=>Int(row[1]),row=>Text(row[0]));
        var catalog=database.Catalog.Schemas.SelectMany(schema=>schema.Routines.Select(routine=>(Schema:schema.Name,Routine:routine))).ToArray();
        Require(catalog.Length==database.Routines.Count,"Generated binding count differs from catalog.");
        Require(methods.Length==database.Routines.Count,"generated_methods row count differs.");
        var objectIds=new HashSet<int>();
        foreach(var (schema,routine) in catalog){
            var binding=database.Routines.Single(item=>item.SourceSchemaName==schema&&item.SourceName==routine.Name);
            var matches=objects.Where(row=>Text(row[0])==routine.Name&&schemaNames.TryGetValue(Int(row[2]),out var value)&&value==schema).ToArray();
            Require(matches.Length==1,"sys.objects mismatch: "+schema+"."+routine.Name);
            var obj=matches[0];var id=Int(obj[1]);Require(objectIds.Add(id),"Duplicate routine object ID.");
            var expectedType=routine.SourceObjectType;
            if(expectedType is null){expectedType=routine.Kind switch{RoutineKind.ScalarFunction=>"FN",RoutineKind.TableFunction=>"TF",RoutineKind.StoredProcedure=>"P",_=>throw new InvalidDataException("Unknown routine kind.")};}
            Require(Text(obj[3]).Trim().Equals(expectedType,StringComparison.OrdinalIgnoreCase),"sys.objects type mismatch: "+routine.Name);
            var procedureRows=procedures.Where(row=>Int(row[1])==id).ToArray();
            Require(procedureRows.Length==(routine.Kind==RoutineKind.StoredProcedure?1:0),"sys.procedures mismatch: "+routine.Name);
            var module=modules.Where(row=>Int(row[0])==id).ToArray();
            Require(module.Length==1&&Equals(Null(module[0][1]),routine.Definition),"sys.sql_modules mismatch: "+routine.Name);
            var actualParameters=parameters.Where(row=>Int(row[0])==id).ToArray();
            var expectedCount=routine.Parameters.Count;if(routine.Kind==RoutineKind.ScalarFunction&&routine.ReturnType is not null){expectedCount++;}
            Require(actualParameters.Length==expectedCount,"sys.parameters count mismatch: "+routine.Name);
            if(routine.Kind==RoutineKind.ScalarFunction&&routine.ReturnType is not null){VerifyParameter(actualParameters,id,"",0,routine.ReturnType,false,types,routine.Name);}
            foreach(var parameter in routine.Parameters){VerifyParameter(actualParameters,id,parameter.Name,parameter.Ordinal,parameter.Type,parameter.IsOutput,types,routine.Name);}
            var actualColumns=columns.Where(row=>Int(row[0])==id).ToArray();
            var expectedColumns=routine.Kind==RoutineKind.TableFunction?routine.ResultColumns:[];
            Require(actualColumns.Length==expectedColumns.Count,"sys.columns count mismatch: "+routine.Name);
            foreach(var column in expectedColumns){
                var rows=actualColumns.Where(row=>Int(row[2])==column.Ordinal+1).ToArray();Require(rows.Length==1,"sys.columns ordinal mismatch: "+routine.Name);
                var row=rows[0];Require(Text(row[1])==column.Name&&Bool(row[7])==column.Type.IsNullable,"sys.columns identity/nullability mismatch: "+routine.Name);
                VerifyType(row,column.Type,3,4,5,6,types,"sys.columns "+routine.Name);
            }
            var method=methods.Where(row=>Int(row[0])==id).ToArray();Require(method.Length==1,"generated_methods ID mismatch: "+routine.Name);
            var m=method[0];Require(Text(m[1])==binding.SourceDatabaseName&&Text(m[2])==binding.SourceSchemaName&&Text(m[3])==binding.SourceName&&Text(m[4])==binding.Kind.ToString()&&Text(m[5])==binding.DeclaringTypeName&&Text(m[6])==binding.MethodName&&Text(m[7])==binding.SourceFile&&Text(m[8])==binding.CSharpSource&&Bool(m[9])==binding.IsStub,"generated_methods binding mismatch: "+routine.Name);
        }
        Require(procedures.Length==catalog.Count(item=>item.Routine.Kind==RoutineKind.StoredProcedure),"sys.procedures has extra rows.");
    }
    private static void VerifyParameter(object?[][]rows,int id,string name,int ordinal,DatabaseType type,bool output,object?[][]types,string routine){
        var matches=rows.Where(row=>Int(row[2])==ordinal).ToArray();Require(matches.Length==1,"sys.parameters ordinal mismatch: "+routine);
        var row=matches[0];Require(Text(row[1])==name&&Bool(row[8])==output,"sys.parameters name/output mismatch: "+routine);
        VerifyType(row,type,3,5,6,7,types,"sys.parameters "+routine);
    }
    private static void VerifyType(object?[]row,DatabaseType type,int typeIndex,int lengthIndex,int precisionIndex,int scaleIndex,object?[][]types,string label){
        if(type.SqlTypeName is not null){var matches=types.Where(value=>string.Equals(Text(value[0]),type.SqlTypeName,StringComparison.OrdinalIgnoreCase)).ToArray();Require(matches.Length==1&&Int(row[typeIndex])==Int(matches[0][1]),label+" type mismatch.");}
        if(type.SourceMaximumLength is{}length){Require(Int(row[lengthIndex])==length,label+" length mismatch.");}
        if(type.SourcePrecision is{}precision){Require(Int(row[precisionIndex])==precision,label+" precision mismatch.");}
        if(type.SourceScale is{}scale){Require(Int(row[scaleIndex])==scale,label+" scale mismatch.");}
    }
    private static object?[][]Read(string connection,string name){
        try{return ComparisonRunner.ReadSql(connection,"SELECT * FROM "+name).ToArray();}
        catch(Exception error)when(error is not OperationCanceledException){throw new InvalidDataException("Failed to read catalog '"+name+"'.",error);}
    }
    private static int Int(object?value)=>Convert.ToInt32(value??throw new InvalidDataException("Unexpected NULL integer metadata."),System.Globalization.CultureInfo.InvariantCulture);
    private static bool Bool(object?value)=>Convert.ToBoolean(value??throw new InvalidDataException("Unexpected NULL boolean metadata."),System.Globalization.CultureInfo.InvariantCulture);
    private static string Text(object?value)=>Convert.ToString(value??throw new InvalidDataException("Unexpected NULL string metadata."),System.Globalization.CultureInfo.InvariantCulture)??throw new InvalidDataException("Invalid string metadata.");
    private static object?Null(object?value)=>value is DBNull?null:value;
    private static void Require(bool condition,string message){if(!condition){throw new InvalidDataException(message);}}
}
