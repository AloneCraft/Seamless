using Seamless.FLinq;
using Test.SourceGenerator;

namespace Benchmark.Dynamic.Tests;

[LinqIntercept(false)]
public sealed class CoreDelegateBoundaryTests{
    [Theory]
    [InlineData("literal")]
    [InlineData("cast")]
    [InlineData("local")]
    [InlineData("reassigned")]
    [InlineData("field")]
    [InlineData("method")]
    [InlineData("anonymous")]
    [InlineData("anonymous-block")]
    [InlineData("anonymous-result")]
    public void CallSiteRetainsTheSelectedDelegate(string mode){
        var declaration=mode switch{
            "local"=>"Func<int,int> selector=value=>value*10;",
            "reassigned"=>"Func<int,int> selector=value=>value+1;selector=value=>value*10;",
            _=>""
        };
        var argument=mode switch{
            "literal"=>"value=>value*10",
            "cast"=>"(Func<int,int>)(value=>value*10)",
            "field"=>"this.selector",
            "method"=>"Project",
            "anonymous"=>"delegate(int value){return value*10;}",
            "anonymous-block"=>"delegate(int value){var result=value*10;return result;}",
            "anonymous-result"=>"delegate(int value){return new {Value=value*10};}",
            _=>"selector"
        };
        var source=$$"""
            using System;
            using System.Linq;
            using Seamless.FLinq;
            namespace DelegateBoundary;
            public sealed class Probe{
                private Func<int,int> selector=value=>value+1;
                private static int Project(int value)=>value*10;
                [LinqIntercept(true)]
                private int[] Execute(int[] input){
                    {{declaration}}
                    return input.Select({{argument}}){{(mode=="anonymous-result"?".Select(value=>value.Value)":"")}}.ToArray();
                }
                public int[] Run(){
                    this.selector=value=>value*10;
                    return this.Execute(new[]{1,2});
                }
            }
            """;
        var output=ExecutableGeneration.Generate(source,"DelegateBoundary_"+mode,1);
        var result=ExecutableGeneration.Run(output.Compilation,"DelegateBoundary.Probe",type=>(int[])type.GetMethod("Run")!.Invoke(Activator.CreateInstance(type),null)!);
        Assert.Equal([10,20],result);
        if(mode=="literal"){Assert.DoesNotContain("[delegate]",output.Generated);}
        else{Assert.Contains("[delegate]",output.Generated);}
    }

    [Theory]
    [InlineData("factory",new[]{10,20,1})]
    [InlineData("closure",new[]{2,4,2})]
    [InlineData("deferred",new[]{10,20,0})]
    [InlineData("prepend-factory",new[]{1,10,20})]
    [InlineData("prepend-closure",new[]{2,2,4})]
    public void DelegateEvaluationAndClosureIdentityArePreserved(string mode,int[] expected){
        var body=mode switch{
            "factory"=>"var values=input.Select(this.CreateSelector()).ToArray();return values.Append(this.factoryCalls).ToArray();",
            "closure"=>"int calls=0;Func<int,int> selector=value=>value+(++calls);var values=input.Select(selector).ToArray();return values.Append(calls).ToArray();",
            "prepend-factory"=>"return input.Select(this.CreateSelector()).Prepend(this.factoryCalls).ToArray();",
            "prepend-closure"=>"int calls=0;Func<int,int> selector=value=>value+(++calls);var values=input.Select(selector).ToArray();return values.Prepend(calls).ToArray();",
            _=>"Func<int,int> selector=value=>value*10;var query=input.Select(selector);selector=value=>value+1;return query.Append(0).ToArray();"
        };
        var source=$$"""
            using System;
            using System.Linq;
            using Seamless.FLinq;
            namespace DelegateEvaluation;
            public sealed class Probe{
                private int factoryCalls;
                private Func<int,int> CreateSelector(){this.factoryCalls++;return value=>value*10;}
                [LinqIntercept(true)]
                private int[] Execute(int[] input){ {{body}} }
                public int[] Run()=>this.Execute(new[]{1,2});
            }
            """;
        var output=ExecutableGeneration.Generate(source,"DelegateEvaluation_"+mode,1);
        var result=ExecutableGeneration.Run(output.Compilation,"DelegateEvaluation.Probe",type=>(int[])type.GetMethod("Run")!.Invoke(Activator.CreateInstance(type),null)!);
        Assert.Equal(expected,result);
    }

    [Theory]
    [InlineData("Append","1,2,3|selector,element,count")]
    [InlineData("Prepend","3,1,2|selector,element,count")]
    public void AnchoredArgumentsAreEvaluatedOnceInChainOrder(string operation,string expected){
        var source=$$"""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using Seamless.FLinq;
            using Seamless.Linq;
            namespace ArgumentOrder;
            public sealed class Probe{
                private readonly List<string> trace=new();
                private Func<int,int> Selector(){this.trace.Add("selector");return value=>value;}
                private int Mark(int value,string name){this.trace.Add(name);return value;}
                [LinqIntercept(true)]
                private int[] Execute(int[] input)=>input.Select(this.Selector().Anchor)
                    .{{operation}}(this.Mark(3,"element").Anchor).Take(this.Mark(9,"count").Anchor).ToArray();
                public string Run(){var values=this.Execute(new[]{1,2});return string.Join(",",values)+"|"+string.Join(",",this.trace);}
            }
            """;
        var output=ExecutableGeneration.Generate(source,"ArgumentOrder_"+operation,1);
        var result=ExecutableGeneration.Run(output.Compilation,"ArgumentOrder.Probe",type=>(string)type.GetMethod("Run")!.Invoke(Activator.CreateInstance(type),null)!);
        Assert.Equal(expected,result);
    }
}
