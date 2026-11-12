using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Seamless.Management;
using Seamless.Management.Server;
using Plan=Seamless.Management.ExecutionPlanDocument;

namespace Management.Tests;

public sealed class ExecutionPlanSemantics{
    [Fact]public void SharedHelperMetadataRetainsEachActualInterceptionCallSite(){
        const string text="""
            using System.Linq;using Seamless.FLinq;
            public sealed class Query{
                [LinqIntercept(true)] private static int CountValues(int[] values)=>values.Where(value=>value>0).Count();
                public static int First()=>CountValues(new[]{1,2});
                public static int Second()=>CountValues(new[]{3,4,5});
            }
            """;
        using var compiled=new RuntimeCompiler().Compile(new("sites",[new("sites.cs",text)],[]));
        Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");
        Assert.Equal(2,compiled.InvokeStatic("Query","First"));Assert.Equal(3,compiled.InvokeStatic("Query","Second"));
        using var image=new MemoryStream(compiled.AssemblyBytes);var plans=Plan.Read(image);
        Assert.Equal(2,plans.Length);Assert.Single(plans.Select(plan=>plan.MethodKey).Distinct());Assert.Single(plans.Select(plan=>plan.SourceHash).Distinct());
        Assert.Equal(2,plans.Select(plan=>plan.CallSite!.Start).Distinct().Count());
        Assert.All(plans,plan=>{Assert.Equal("sites.cs",plan.CallSite!.Path);Assert.StartsWith("CountValues(",text.Substring(plan.CallSite.Start,plan.CallSite.Length));});
        Assert.Contains(plans,plan=>plan.CallSite!.Caller=="Query.First()");Assert.Contains(plans,plan=>plan.CallSite!.Caller=="Query.Second()");
    }
    [Fact]public void JoinInputsUseTheirBoundParameterRoles(){
        using var compiled=new RuntimeCompiler().Compile(new("join-roles",[new("join.cs","""
            using System.Linq;using Seamless.FLinq;
            public sealed class Query{
                [LinqIntercept(true)] private static int Compute(int[] left,int[] right)=>left.Where(x=>x>0).Join(right.Where(x=>x>0),x=>x,x=>x,(x,y)=>x+y).Sum();
                public static int Run()=>Compute(new[]{1,2},new[]{2,3});
            }
            """)],[]));
        Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");
        Assert.Equal(4,compiled.InvokeStatic("Query","Run"));
        var plan=Assert.Single(compiled.Result.Plans);
        var join=Assert.Single(plan.Nodes,node=>node.Kind=="SourceOperator"&&node.Label=="Join");
        var inputs=plan.Edges.Where(edge=>edge.To==join.Id&&edge.Kind=="OriginalDataFlow").ToArray();
        Assert.Equal(2,inputs.Length);
        Assert.Contains(inputs,edge=>edge.InputRole=="outer");
        Assert.Contains(inputs,edge=>edge.InputRole=="inner");
    }

    [Fact]public void ArithmeticExpressionsHaveTypedDependenciesWithoutInventedMeasurements(){
        using var compiled=new RuntimeCompiler().Compile(new("arithmetic",[new("arithmetic.cs","""
            using System.Linq;using Seamless.FLinq;
            public sealed class Query{
                [LinqIntercept(true)] private static int Compute(int[] values)=>values.Where(x=>x*2==6).Select(x=>x+1).Sum();
                public static int Run()=>Compute(new[]{1,2,3,4});
            }
            """)],[]));
        Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");
        Assert.Equal(4,compiled.InvokeStatic("Query","Run"));
        using var image=new MemoryStream(compiled.AssemblyBytes);
        var plan=Assert.Single(Plan.Read(image));Assert.Equal(2,plan.SchemaVersion);
        var multiply=Assert.Single(plan.Nodes,node=>node.Kind=="Expression"&&node.Label=="Multiply");
        var equal=Assert.Single(plan.Nodes,node=>node.Kind=="Expression"&&node.Label=="Equal");
        Assert.Equal("OriginalExpression",multiply.Layer);Assert.Equal("NotNullable",multiply.Nullability);Assert.NotEmpty(multiply.OutputType!);
        Assert.Equal("NotIndependentlyMeasured",multiply.Measurement);Assert.Equal("arithmetic.cs",multiply.SourcePath);Assert.True(multiply.Length>0);
        Assert.Contains(plan.Edges,edge=>edge.From==multiply.Id&&edge.To==equal.Id&&edge.Kind=="ExpressionDependency"&&edge.InputRole=="Left");
        Assert.Contains(plan.Edges,edge=>edge.From==equal.Id&&edge.Kind=="SourceMapping");
        Assert.All(plan.Nodes.Where(node=>node.Kind=="SourceOperator"),node=>Assert.Equal("Original",node.Layer));
        Assert.All(plan.Nodes.Where(node=>node.Kind is "Fragment" or "PipelineStage"),node=>Assert.Equal("Selected",node.Layer));
        Assert.All(plan.Edges.Where(edge=>edge.Kind=="SelectedReference"),edge=>Assert.Equal("Selected",plan.Nodes.Single(node=>node.Id==edge.To).Layer));
        Assert.DoesNotContain(plan.Edges,edge=>edge.Kind is "ExecutionDependency" or "ControlFlow");
        Assert.Contains("rankdir=TB",plan.ToDot());
    }

    [Fact]public void ShortCircuitAndNullableSemanticsPreserveActualResults(){
        using var compiled=new RuntimeCompiler().Compile(new("nullable",[new("nullable.cs","""
            #nullable enable
            using System.Linq;using Seamless.FLinq;
            public sealed class Query{
                private static int calls;
                private static bool Observe(int value){calls++;return value>0;}
                [LinqIntercept(true)] private static int Compute(int?[] values)=>values.Where(x=>x.HasValue&&Observe(x.Value)).Select(x=>x.Value).Sum();
                public static int[] Run(){calls=0;var value=Compute(new int?[]{null,1,2});return new[]{value,calls};}
            }
            """)],[]));
        Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");
        Assert.Equal(new[]{3,2},Assert.IsType<int[]>(compiled.InvokeStatic("Query","Run")));
        var plan=Assert.Single(compiled.Result.Plans);
        Assert.Contains(plan.Nodes,node=>node.Kind=="Expression"&&node.Properties.TryGetValue("Evaluation",out var evaluation)&&evaluation=="ShortCircuit");
        Assert.Contains(plan.Nodes,node=>node.Kind=="Expression"&&node.Nullability=="Nullable");
        Assert.All(plan.Nodes.Where(node=>node.Kind=="Expression"),node=>Assert.Equal("NotIndependentlyMeasured",node.Measurement));
    }

    [Fact]public void ExpressionCollectionLimitReportsPartialMetadataAndKeepsValidEdges(){
        var condition=string.Join("||",Enumerable.Range(0,70).Select(value=>"x=="+value));
        var text="using System.Linq;using Seamless.FLinq;public sealed class Query{[LinqIntercept(true)]private static int Compute(int[] values)=>values.Where(x=>"+condition+").Count();public static int Run()=>Compute(new[]{0,100});}";
        using var compiled=new RuntimeCompiler().Compile(new("limit",[new("limit.cs",text)],[]));
        Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");Assert.Equal(1,compiled.InvokeStatic("Query","Run"));
        var plan=Assert.Single(compiled.Result.Plans);
        Assert.False(plan.MetadataComplete);Assert.True(plan.OmittedNodeCount>0);Assert.Contains(plan.Diagnostics,value=>value.StartsWith("Metadata.ExpressionLimit",StringComparison.Ordinal));
        Assert.InRange(plan.Nodes.Length,1,Plan.MaximumNodes);Assert.InRange(plan.Edges.Length,1,Plan.MaximumEdges);
        Assert.All(plan.Edges,edge=>{Assert.Contains(plan.Nodes,node=>node.Id==edge.From);Assert.Contains(plan.Nodes,node=>node.Id==edge.To);});
        Assert.All(plan.Nodes.SelectMany(node=>node.Inputs),input=>Assert.Contains(plan.Nodes,node=>node.Id==input));
    }

    [Fact]public void LegacySchemaOneKeepsUnknownLayersAndInputCompatibility(){
        var json="""
            {"schemaVersion":1,"methodKey":"Query.Compute()","generatorVersion":"legacy","sourceHash":"source","generatedBodyHash":"body","nodes":[
            {"id":"a","kind":"Fragment","label":"Source","inputs":[],"queryId":null,"sourcePath":"q.cs","start":0,"length":1,"properties":{}},
            {"id":"b","kind":"Fragment","label":"Filter","inputs":["a"],"queryId":null,"sourcePath":"q.cs","start":1,"length":1,"properties":{}}],"trace":{}}
            """;
        using var image=new MemoryStream(EmitAttribute(1,json));var plan=Assert.Single(Plan.Read(image));
        Assert.Equal(1,plan.SchemaVersion);Assert.Empty(plan.Edges);Assert.All(plan.Nodes,node=>Assert.Equal("Unknown",node.Layer));
        Assert.Contains("\"a\" -> \"b\"",plan.ToDot());Assert.True(plan.MetadataComplete);
    }

    [Theory][InlineData("dangling")][InlineData("count")][InlineData("complete")][InlineData("callsite")]
    public void SchemaTwoRejectsInvalidSemanticMetadata(string invalid){
        var nodes=new[]{new ExecutionPlanNode("a","Fragment","Source",[],null,null,0,0,[]){Layer="Selected"}};
        var plan=new Plan(2,"Query.Compute()","test","source","body",nodes,JsonSerializer.SerializeToElement(new{}));
        if(invalid=="dangling"){plan=plan with{Edges=[new("missing","a","ExpressionDependency")]};}
        else if(invalid=="count"){plan=plan with{Nodes=Enumerable.Range(0,Plan.MaximumNodes+1).Select(index=>nodes[0] with{Id="n"+index}).ToArray()};}
        else if(invalid=="complete"){plan=plan with{OmittedNodeCount=1};}
        else{plan=plan with{CallSite=new("plan.cs",int.MaxValue,1,"Query.Run()")};}
        using var image=new MemoryStream(EmitAttribute(2,JsonSerializer.Serialize(plan,ManagementJson.Options)));
        Assert.Throws<InvalidDataException>(()=>Plan.Read(image));
    }

    [Fact]public void PeReadDoesNotExecuteQueryOrAttributeConstructors(){
        var plan=new Plan(2,"Query.Compute()","test","source","body",[],JsonSerializer.SerializeToElement(new{}));
        using var image=new MemoryStream(EmitAttribute(2,JsonSerializer.Serialize(plan,ManagementJson.Options)));
        Assert.Single(Plan.Read(image));
    }

    private static byte[] EmitAttribute(int version,string json){
        var literal=SymbolDisplay.FormatLiteral(json,true);
        var source="namespace Seamless.FLinq{[System.AttributeUsage(System.AttributeTargets.Method)]public sealed class ExecutionPlanAttribute:System.Attribute{public ExecutionPlanAttribute(int version,string data){throw new System.InvalidOperationException(\"must not execute\");}}}public static class Query{[Seamless.FLinq.ExecutionPlan("+version+","+literal+")]public static int Compute()=>throw new System.InvalidOperationException(\"must not execute\");}";
        var compilation=CSharpCompilation.Create("PlanFixture_"+Guid.NewGuid().ToString("N"),[CSharpSyntaxTree.ParseText(source)],global::Seamless.Management.SourceEditor.PlatformReferences(),new(OutputKind.DynamicallyLinkedLibrary));
        using var image=new MemoryStream();var result=compilation.Emit(image);
        Assert.True(result.Success,string.Join("\n",result.Diagnostics));return image.ToArray();
    }
}
