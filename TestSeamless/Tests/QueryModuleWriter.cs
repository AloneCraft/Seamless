using System.Text.Json;
using Seamless.Database.Client;
using Seamless.Database.Rdbms;

namespace TestSeamless.Tests;

public sealed class QueryModuleWriter{
    [Fact]public void Write(){
        var root=TestTempDirectory.NewPath("seamless-query-writer-");
        try{
            Directory.CreateDirectory(root);
            var definitions=Path.Combine(root,"input.json");var output=Path.Combine(root,"generation");
            var database=new ComparisonDatabase("Sales","SalesDb","fingerprint",true,new DatabaseCatalog("Sales",[]),[]);
            var generation=new ComparisonGeneration("generation","version",DateTimeOffset.UtcNow,[database]);
            var valid=new{Id="orders/find",Database="Sales",ClassName="Example.OrderQueries",MethodName="Find",ArgumentsType="Example.OrderArgs",ResultType="Example.OrderRow",ClientMethodName="FindOrdersAsync",IsReadOnly=true,ComparisonArgumentsJson="7",ComparisonColumns=new[]{"Id","Name"}};
            File.WriteAllText(definitions,JsonSerializer.Serialize(new[]{valid}));
            global::TestSeamless.QueryModuleWriter.Write(output,generation,definitions);
            var source=File.ReadAllText(Path.Combine(output,"queries","QueryModule.cs"));
            Assert.Contains("Func<global::SalesDb.SalesDb,global::@Example.@OrderArgs,CancellationToken,IEnumerable<global::@Example.@OrderRow>> invoke=new global::@Example.@OrderQueries().@Find",source);
            Assert.Contains("invoke((global::SalesDb.SalesDb)context.Database,args,context.CancellationToken)",source);
            Assert.Contains("QueryAsync<global::@Example.@OrderArgs,global::@Example.@OrderRow>",source);
            Assert.Contains("CreateComparisonArguments(string queryId)",source);Assert.Contains("ProjectComparisonRow(string queryId,ReadOnlyMemory<byte> row)",source);
            Assert.Contains("value.@Id,value.@Name",source);Assert.Contains("ExecuteComparisonRemoteAsync",source);
            var entry=Assert.Single(JsonSerializer.Deserialize<NativeQueryDefinition[]>(File.ReadAllText(Path.Combine(output,"native-definitions.json")))!);
            Assert.Equal("orders/find",entry.Id);Assert.Equal("fingerprint",entry.SchemaFingerprint);Assert.Equal("Example.OrderArgs",entry.ArgumentsType);Assert.Equal("Example.OrderRow",entry.ResultType);
            var preserve=Path.Combine(output,"queries","Handwritten.cs");File.WriteAllText(preserve,"preserve");global::TestSeamless.QueryModuleWriter.Write(output,generation,definitions);Assert.Equal("preserve",File.ReadAllText(preserve));
            File.WriteAllText(definitions,JsonSerializer.Serialize(new[]{valid,valid}));Assert.Throws<InvalidDataException>(()=>global::TestSeamless.QueryModuleWriter.Write(output,generation,definitions));
            File.WriteAllText(definitions,JsonSerializer.Serialize(new[]{valid with{Database="missing"}}));Assert.Throws<InvalidDataException>(()=>global::TestSeamless.QueryModuleWriter.Write(output,generation,definitions));
            File.WriteAllText(definitions,JsonSerializer.Serialize(new[]{valid with{IsReadOnly=false}}));Assert.Throws<InvalidDataException>(()=>global::TestSeamless.QueryModuleWriter.Write(output,generation,definitions));
            File.WriteAllText(definitions,JsonSerializer.Serialize(new[]{valid with{ComparisonColumns=new[]{"Id);evil"}}}));Assert.Throws<InvalidDataException>(()=>global::TestSeamless.QueryModuleWriter.Write(output,generation,definitions));
            File.WriteAllText(definitions,JsonSerializer.Serialize(new[]{valid with{MethodName="Find();evil"}}));Assert.Throws<InvalidDataException>(()=>global::TestSeamless.QueryModuleWriter.Write(output,generation,definitions));
        }finally{if(Directory.Exists(root)){Directory.Delete(root,true);}}
    }
}
