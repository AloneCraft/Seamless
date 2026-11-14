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
    [InlineData("readonly-field")]
    [InlineData("readonly-field-stopped")]
    [InlineData("method-stopped")]
    [InlineData("literal-stopped")]
    public void CallSiteInlinesFixedDelegatesAndRetainsTheOthers(string mode){
        var declaration=mode switch{
            "local"=>"Func<int,int> selector=value=>value*10;",
            "reassigned"=>"Func<int,int> selector=value=>value+1;selector=value=>value*10;",
            _=>""
        };
        var argument=mode switch{
            "literal"=>"value=>value*10",
            "cast"=>"(Func<int,int>)(value=>value*10)",
            "field"=>"this.selector",
            "readonly-field"=>"this.fixedSelector",
            "readonly-field-stopped"=>"this.stoppedSelector",
            "method-stopped"=>"StoppedProject",
            "literal-stopped"=>"[LinqIntercept(false)] (int value)=>value*10",
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
                [LinqIntercept(false)] private static int StoppedProject(int value)=>value*10;
                private readonly Func<int,int> fixedSelector=value=>value*10;
                [LinqIntercept(false)] private readonly Func<int,int> stoppedSelector=value=>value*10;
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
        // 呼び出し時の中身が確定しているもの(直接のラムダ・delegate 構文・キャスト・代入が1回だけの変数・メソッドグループ・readonly のフィールド)は本体を取り込む。
        // 再代入のある変数、readonly でないフィールド、匿名型を返す delegate 構文、[LinqIntercept(false)] を付けたものはデリゲートを保持する。
        if(mode is "literal" or "cast" or "local" or "method" or "anonymous" or "anonymous-block" or "readonly-field"){Assert.DoesNotContain("[delegate]",output.Generated);}
        else{Assert.Contains("[delegate]",output.Generated);}
    }

    // LINQ の引数ではないラムダ(Task.Run(()=>…) へ渡すものや、変数へ入れるラムダ)でも、本体の中の LINQ は展開する。
    // 本体をその場で組み立て直すだけなので、変数の再代入やデリゲートの受け渡しの意味は変わらない。
    [Theory]
    [InlineData("argument",5)]
    [InlineData("local",12)]
    [InlineData("reassigned",13)]
    [InlineData("delegate-syntax",5)]
    public void LambdaOutsideLinqArgumentsStillExpandsTheLinqInsideItsBody(string mode,int expected){
        var body=mode switch{
            "argument"=>"return Wrap(()=>input.Where(value=>value>1).Sum())();",
            "local"=>"Func<int,int> add=bias=>bias+input.Count(value=>value>1);return add(10);",
            "reassigned"=>"Func<int,int> add=bias=>bias;add=bias=>bias+input.Count(value=>value>0);return add(10);",
            _=>"return Wrap(delegate(){return input.Where(value=>value>1).Sum();})();"
        };
        var source=$$"""
            using System;
            using System.Linq;
            using Seamless.FLinq;
            namespace NestedLambda;
            public sealed class Probe{
                private static Func<int> Wrap(Func<int> function)=>function;
                [LinqIntercept(true)]
                private static int Execute(int[] input){
                    {{body}}
                }
                public static int Run()=>Execute(new[]{1,2,3});
            }
            """;
        var output=ExecutableGeneration.Generate(source,"NestedLambda_"+mode.Replace('-','_'),1);
        var result=ExecutableGeneration.Run(output.Compilation,"NestedLambda.Probe",type=>(int)type.GetMethod("Run")!.Invoke(null,null)!);
        Assert.Equal(expected,result);
        var executable=GeneratedSourceAssert.ExecutableText(output.Generated);
        Assert.DoesNotContain("input.Where(",executable);
        Assert.DoesNotContain("input.Count(",executable);
    }

    // デリゲートのまま呼ぶ形でも、本体の中の LINQ は展開する(System.Linq の呼び出しを残さない)。
    [Theory]
    [InlineData("outer-write")]
    [InlineData("outer-write-block")]
    [InlineData("local-function")]
    [InlineData("cast")]
    [InlineData("anonymous-result")]
    public void KeptDelegateStillExpandsTheLinqInsideItsBody(string mode){
        var argument=mode switch{
            "outer-write"=>"value=>value*10+inner.Count(item=>item>(calls++*0))",
            "outer-write-block"=>"value=>{calls++;var positive=inner.Where(item=>item>0).Count();return value*10+positive;}",
            "local-function"=>"value=>Scale(value)+inner.Count(item=>item>0)",
            "cast"=>"(Func<int,int>)(value=>value*10+inner.Count(item=>item>0))",
            _=>"delegate(int value){return new {Value=value*10+inner.Count(item=>item>0)};}"
        };
        var source=$$"""
            using System;
            using System.Linq;
            using Seamless.FLinq;
            namespace KeptDelegate;
            public sealed class Probe{
                [LinqIntercept(true)]
                private static int[] Execute(int[] input,int[] inner){
                    int calls=0;
                    int Scale(int value)=>value*10;
                    _=calls;_=Scale(0);
                    return input.Select({{argument}}){{(mode=="anonymous-result"?".Select(value=>value.Value)":"")}}.ToArray();
                }
                public static int[] Run()=>Execute(new[]{1,2},new[]{-1,5,7});
            }
            """;
        var output=ExecutableGeneration.Generate(source,"KeptDelegate_"+mode.Replace('-','_'),1);
        var result=ExecutableGeneration.Run(output.Compilation,"KeptDelegate.Probe",type=>(int[])type.GetMethod("Run")!.Invoke(null,null)!);
        Assert.Equal([12,22],result);
        var executable=GeneratedSourceAssert.ExecutableText(output.Generated);
        Assert.DoesNotContain("inner.Count(",executable);
        Assert.DoesNotContain("inner.Where(",executable);
        Assert.DoesNotContain("System.Linq.Enumerable.Count",executable);
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
