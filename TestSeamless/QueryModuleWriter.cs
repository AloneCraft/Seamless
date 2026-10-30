using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Seamless.Database.Client;
using Seamless.Database.Rdbms;

namespace TestSeamless;

public static class QueryModuleWriter{
    public static void Write(string directory,ComparisonGeneration generation)=>Write(directory,generation,Path.Combine(AppContext.BaseDirectory,"Queries","query-definitions.json"));
    public static void Write(string directory,ComparisonGeneration generation,string definitionPath){
        var handwritten=JsonSerializer.Deserialize<HandwrittenQuery[]>(File.ReadAllText(definitionPath))??throw new InvalidDataException("Query definitions must be an array.");
        Validate(handwritten,generation);
        var output=Path.Combine(directory,"queries");Directory.CreateDirectory(output);var methods=new StringBuilder();var dispatch=new StringBuilder();var proxy=new StringBuilder();var comparisonArguments=new StringBuilder();var comparisonRows=new StringBuilder();var comparisonRemote=new StringBuilder();var definitions=new List<NativeQueryDefinition>();var cases=new List<object>();var index=0;
        foreach(var database in generation.Databases){foreach(var table in database.Tables){
            var id="count/"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(database.Name+"\0"+table.SourceSchema+"\0"+table.SourceTable)))[..24];var method="Query"+index++;var type="global::"+database.GeneratedName+"."+database.GeneratedName;
            // LINQ呼出しも入口も通常のソースとして保存し、その後にFLinqを含めてcompileする。
            methods.AppendLine($"public long {method}({type} database,CancellationToken cancellationToken)=>database.{string.Join(".",table.ObjectName.Split('.').Select(part=>"@"+part))}.Where(row=>CheckCancellation(cancellationToken)).LongCount();");
            dispatch.AppendLine($"case {Literal(id)}:yield return MemoryPackSerializer.Serialize(new TypedQueries().{method}(({type})context.Database,context.CancellationToken));context.CancellationToken.ThrowIfCancellationRequested();yield break;");
            proxy.AppendLine($"public Task<long> {method}Async(CancellationToken cancellationToken=default)=>this.client.ExecuteScalarAsync<int,long>({Literal(generation.Id)},{Literal(id)},{Literal(database.Name)},{Literal(database.Fingerprint)},0,cancellationToken);");
            comparisonArguments.AppendLine($"case {Literal(id)}:return MemoryPackSerializer.Serialize(0);");comparisonRows.AppendLine($"case {Literal(id)}:return new object?[]{{MemoryPackSerializer.Deserialize<long>(row.Span)}};");comparisonRemote.AppendLine($"case {Literal(id)}:await foreach(var value in client.QueryAsync<int,long>(moduleId,queryId,database,fingerprint,0,cancellationToken)){{yield return new object?[]{{value}};}}yield break;");
            definitions.Add(new(id,database.Name,database.Fingerprint,typeof(int).FullName!,typeof(long).FullName!));cases.Add(new{Id=id,Database=database.Name,ClassName="TestSeamless.Queries.TypedQueries",MethodName=method,ClientMethodName=method+"Async",ArgumentsType=typeof(int).FullName,ResultType=typeof(long).FullName,Sql="SELECT COUNT_BIG(*) AS [Count] FROM "+Quote(table.SourceSchema)+"."+Quote(table.SourceTable),NativeQuery=id,Ordered=false});
        }}
        foreach(var query in handwritten){
            var database=generation.Databases.Single(d=>d.Name==query.Database);var databaseType="global::"+database.GeneratedName+"."+database.GeneratedName;
            var arguments=TypeName(query.ArgumentsType);var result=TypeName(query.ResultType);var target=TypeName(query.ClassName);var method=Identifier(query.MethodName);var clientMethod=Identifier(query.ClientMethodName);
            dispatch.AppendLine($"case {Literal(query.Id)}:{{var args=MemoryPackSerializer.Deserialize<{arguments}>(arguments.Span)!;Func<{databaseType},{arguments},CancellationToken,IEnumerable<{result}>> invoke=new {target}().{method};foreach({result} row in invoke(({databaseType})context.Database,args,context.CancellationToken)){{context.CancellationToken.ThrowIfCancellationRequested();yield return MemoryPackSerializer.Serialize(row);}}yield break;}}");
            proxy.AppendLine($"public IAsyncEnumerable<{result}> {clientMethod}({arguments} arguments,CancellationToken cancellationToken=default)=>this.client.QueryAsync<{arguments},{result}>({Literal(generation.Id)},{Literal(query.Id)},{Literal(database.Name)},{Literal(database.Fingerprint)},arguments,cancellationToken);");
            if(query.ComparisonArgumentsJson is not null){
                comparisonArguments.AppendLine($"case {Literal(query.Id)}:return MemoryPackSerializer.Serialize(System.Text.Json.JsonSerializer.Deserialize<{arguments}>({Literal(query.ComparisonArgumentsJson)})!);");
                var columns=string.Join(",",query.ComparisonColumns!.Select(column=>column=="$value"?"value":"value."+Identifier(column)));
                comparisonRows.AppendLine($"case {Literal(query.Id)}:{{var value=MemoryPackSerializer.Deserialize<{result}>(row.Span)!;return new object?[]{{{columns}}};}}");
                comparisonRemote.AppendLine($"case {Literal(query.Id)}:await foreach(var value in client.QueryAsync<{arguments},{result}>(moduleId,queryId,database,fingerprint,System.Text.Json.JsonSerializer.Deserialize<{arguments}>({Literal(query.ComparisonArgumentsJson)})!,cancellationToken)){{yield return new object?[]{{{columns}}};}}yield break;");
            }
            if(query.SourceFile is not null){
                var template=File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(definitionPath))!,query.SourceFile));
                if(!template.Contains("{{DatabaseType}}",StringComparison.Ordinal)){throw new InvalidDataException("Query source template has no database type placeholder.");}
                var name="Handwritten_"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(query.Id)))[..24]+".cs";
                File.WriteAllText(Path.Combine(output,name),template.Replace("{{DatabaseType}}",databaseType,StringComparison.Ordinal).Replace("{{DatabaseNamespace}}","global::"+database.GeneratedName,StringComparison.Ordinal),new UTF8Encoding(false));
            }
            definitions.Add(new(query.Id,database.Name,database.Fingerprint,query.ArgumentsType,query.ResultType,query.IsReadOnly));
        }
        var fixture=generation.Databases.SingleOrDefault(d=>d.Name=="Seamless_Comparison_Fixture");
        if(fixture is not null){
            var databaseType="global::"+fixture.GeneratedName+"."+fixture.GeneratedName;var key="global::"+fixture.GeneratedName+".Keys.FixtureParent";var record="global::"+fixture.GeneratedName+".Records.FixtureParent";
            dispatch.AppendLine($"case \"fixture/insert-parent\":{{var args=MemoryPackSerializer.Deserialize<NativeFixtureCommand>(arguments.Span)!;var database=({databaseType})context.Database;if(!database.dbo.FixtureParent.Add(new {record}(args.Id,args.Name))){{throw new InvalidOperationException(\"Duplicate fixture key.\");}}yield return MemoryPackSerializer.Serialize(1L);yield break;}}");
            dispatch.AppendLine($"case \"fixture/update-parent\":{{var args=MemoryPackSerializer.Deserialize<NativeFixtureCommand>(arguments.Span)!;var database=({databaseType})context.Database;var key=new {key}(args.Id);long count=0;if(database.dbo.FixtureParent.ContainsKey(key)){{database.dbo.FixtureParent[key]=new {record}(args.Id,args.Name);count=1;}}yield return MemoryPackSerializer.Serialize(count);yield break;}}");
            dispatch.AppendLine($"case \"fixture/delete-parent\":{{var args=MemoryPackSerializer.Deserialize<NativeFixtureCommand>(arguments.Span)!;var database=({databaseType})context.Database;long count=0;if(database.dbo.FixtureParent.Remove(new {key}(args.Id))){{count=1;}}yield return MemoryPackSerializer.Serialize(count);yield break;}}");
            dispatch.AppendLine($"case \"fixture/read-parent\":{{var args=MemoryPackSerializer.Deserialize<NativeFixtureCommand>(arguments.Span)!;var database=({databaseType})context.Database;if(database.dbo.FixtureParent.TryGetValue(new {key}(args.Id),out var row)){{yield return MemoryPackSerializer.Serialize(new NativeFixtureRow(row.Id,row.Name));}}yield break;}}");
            foreach(var operation in new[]{"insert-parent","update-parent","delete-parent","read-parent"}){var read=operation=="read-parent";var result=typeof(long).FullName!;if(read){result="TestSeamless.Queries.NativeFixtureRow";}definitions.Add(new("fixture/"+operation,fixture.Name,fixture.Fingerprint,"TestSeamless.Queries.NativeFixtureCommand",result,read));}
        }
        var source="using System;using System.Linq;using System.Collections.Generic;using System.Threading;using System.Threading.Tasks;using System.Runtime.CompilerServices;using MemoryPack;using Seamless.Database.Client;namespace TestSeamless.Queries;public sealed class TypedQueries{private static bool CheckCancellation(CancellationToken token){token.ThrowIfCancellationRequested();return true;}"+methods+"}public sealed class QueryModule:INativeQueryModule{public IEnumerable<byte[]> Execute(string queryId,ReadOnlyMemory<byte> arguments,INativeQueryContext context){context.CancellationToken.ThrowIfCancellationRequested();switch(queryId){"+dispatch+"default:throw new NotSupportedException(\"Unknown typed query ID.\");}}public byte[] CreateComparisonArguments(string queryId){switch(queryId){"+comparisonArguments+"default:throw new NotSupportedException(\"No comparison arguments for query.\");}}public object?[] ProjectComparisonRow(string queryId,ReadOnlyMemory<byte> row){switch(queryId){"+comparisonRows+"default:throw new NotSupportedException(\"No comparison projection for query.\");}}public async IAsyncEnumerable<object?[]> ExecuteComparisonRemoteAsync(NativeQueryClient client,string moduleId,string queryId,string database,string fingerprint,[EnumeratorCancellation]CancellationToken cancellationToken=default){switch(queryId){"+comparisonRemote+"default:await Task.CompletedTask;foreach(var value in Array.Empty<object?[]>()){yield return value;}throw new NotSupportedException(\"No comparison query.\");}}}public sealed class TypedClient(NativeQueryClient client){private readonly NativeQueryClient client=client;"+proxy+"}";
        File.WriteAllText(Path.Combine(output,"QueryModule.cs"),source,new UTF8Encoding(false));File.WriteAllText(Path.Combine(directory,"native-definitions.json"),JsonSerializer.Serialize(definitions));File.WriteAllText(Path.Combine(directory,"generated-cases.json"),JsonSerializer.Serialize(cases,new JsonSerializerOptions{WriteIndented=true}));
    }
    public static void Deploy(string directory){
        var generation=ComparisonGeneration.Read(directory);var source=Path.GetDirectoryName(typeof(Program).Assembly.Location)!;var destination=Path.Combine(directory,"modules");Directory.CreateDirectory(destination);var path=Path.Combine(source,"TestSeamless.Queries.dll");if(!File.Exists(path)){throw new FileNotFoundException("Build the generated query module first.",path);}
        foreach(var file in Directory.EnumerateFiles(source,"*.dll")){File.Copy(file,Path.Combine(destination,Path.GetFileName(file)),true);}
        var assembly=Assembly.LoadFrom(path);if(assembly.GetType("TestSeamless.Queries.QueryModule") is null){throw new InvalidDataException("The generated query module entry is absent.");}
        var definitions=JsonSerializer.Deserialize<NativeQueryDefinition[]>(File.ReadAllText(Path.Combine(directory,"native-definitions.json")))!;
        using var stream=File.OpenRead(path);var manifest=new NativeModuleManifest(generation.Id,"TestSeamless.Queries.dll",Convert.ToHexString(SHA256.HashData(stream)),"TestSeamless.Queries.QueryModule",generation.ProductVersion,definitions);
        File.WriteAllText(Path.Combine(destination,"native-module.json"),JsonSerializer.Serialize(manifest,new JsonSerializerOptions{WriteIndented=true}));(generation with{ModuleManifest="modules/native-module.json"}).Write(directory);
    }
    private sealed record HandwrittenQuery(string Id,string Database,string ClassName,string MethodName,string ArgumentsType,string ResultType,string ClientMethodName,bool IsReadOnly,string? ComparisonArgumentsJson=null,string[]? ComparisonColumns=null,string? SourceFile=null);
    private static void Validate(HandwrittenQuery[] queries,ComparisonGeneration generation){
        var ids=new HashSet<string>(StringComparer.Ordinal);var names=new HashSet<string>(StringComparer.Ordinal){"client","TypedClient"};var countIndex=0;
        foreach(var database in generation.Databases){foreach(var table in database.Tables){ids.Add("count/"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(database.Name+"\0"+table.SourceSchema+"\0"+table.SourceTable)))[..24]);names.Add("Query"+(countIndex++)+"Async");}}
        if(generation.Databases.Any(d=>d.Name=="Seamless_Comparison_Fixture")){foreach(var operation in new[]{"insert-parent","update-parent","delete-parent","read-parent"}){ids.Add("fixture/"+operation);}}
        foreach(var query in queries){
            if(query is null||string.IsNullOrWhiteSpace(query.Id)||string.IsNullOrWhiteSpace(query.Database)){throw new InvalidDataException("Query ID and database are required.");}
            if(!ids.Add(query.Id)||!names.Add(query.ClientMethodName)){throw new InvalidDataException("Duplicate query ID or client method name.");}
            var matches=generation.Databases.Where(d=>d.Name==query.Database).ToArray();if(matches.Length!=1){throw new InvalidDataException("Query database is missing or ambiguous: "+query.Database);}
            if(matches[0].ReadOnly&&!query.IsReadOnly){throw new InvalidDataException("A mutating query cannot target a read-only database: "+query.Database);}
            if((query.ComparisonArgumentsJson is null)!=(query.ComparisonColumns is null)){throw new InvalidDataException("Comparison arguments and columns must be supplied together.");}
            if(query.ComparisonArgumentsJson is not null){using var document=JsonDocument.Parse(query.ComparisonArgumentsJson);if(query.ComparisonColumns!.Length==0){throw new InvalidDataException("Comparison columns must not be empty.");}foreach(var column in query.ComparisonColumns){if(column!="$value"){_=Identifier(column);}}}
            if(query.SourceFile is not null&&(Path.GetFileName(query.SourceFile)!=query.SourceFile||!query.SourceFile.EndsWith(".cs.template",StringComparison.Ordinal))){throw new InvalidDataException("Query source template must be a local .cs.template file.");}
            _=TypeName(query.ClassName);_=TypeName(query.ArgumentsType);_=TypeName(query.ResultType);_=Identifier(query.MethodName);_=Identifier(query.ClientMethodName);
        }
    }
    private static string Identifier(string value){
        if(string.IsNullOrEmpty(value)||!(char.IsAsciiLetter(value[0])||value[0]=='_')||value.Any(c=>!(char.IsAsciiLetterOrDigit(c)||c=='_'))){throw new InvalidDataException("Invalid C# identifier: "+value);}
        return "@"+value;
    }
    private static string TypeName(string value){if(string.IsNullOrEmpty(value)){throw new InvalidDataException("A fully qualified type name is required.");}return "global::"+string.Join(".",value.Split('.').Select(Identifier));}
    private static string Literal(string value)=>JsonSerializer.Serialize(value);
    private static string Quote(string value)=>"["+value.Replace("]","]]")+"]";
}
