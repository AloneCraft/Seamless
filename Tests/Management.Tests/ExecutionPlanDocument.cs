using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class ExecutionPlanDocument{
    [Fact]public void Read(){
        var source=new SourceSnapshot("1",[new("query.cs","""
            using System.Linq;
            using Seamless.FLinq;
            public sealed class Query{
                [LinqIntercept(true)] private static int Sum(int[] data)=>data.Where(x=>x>1).Select(x=>x*2).Sum();
                public static int Run()=>Sum(new[]{1,2,3});
            }
            """)],[]);
        var compiled=new RuntimeCompiler().Compile(source);
        Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");
        using var pe=new MemoryStream(compiled.AssemblyBytes);
        var plans=Seamless.Management.ExecutionPlanDocument.Read(pe);
        var plan=Assert.Single(plans);Assert.Equal(1,plan.SchemaVersion);
        Assert.Equal(typeof(global::Seamless.FLinq.Generator.FLinqIncrementalGenerator).Module.ModuleVersionId.ToString("D"),plan.GeneratorBuild);
        Assert.Contains(plan.Nodes,node=>node.Kind=="Fragment");
        Assert.Contains(plan.Nodes,node=>node.Kind=="SourceOperator"&&node.Label=="Where");
        Assert.Contains("digraph",plan.ToDot());Assert.NotEmpty(plan.GeneratedBodyHash);
        Assert.Equal(10,compiled.InvokeStatic("Query","Run"));
    }
    [Fact]public void ToDot(){
        var plan=new Seamless.Management.ExecutionPlanDocument(1,"M","1","s","b",[
            new("a","Custom","a\"\\\nb",[],null,null,0,0,[]),
            new("b","Filter","Where",["a"],null,null,0,0,[])],JsonSerializer.SerializeToElement(new{}));
        var dot=plan.ToDot();Assert.Contains("\"a\" -> \"b\"",dot);Assert.Contains("a\\\"\\\\\\nb",dot);
    }
    [Fact]public void OperatorsAndSourceMapping(){
        var source=new SourceSnapshot("operators",[new("operators.cs","""
            using System.Linq;using Seamless.FLinq;
            public sealed class Query{
                [LinqIntercept(true)] private static int[] Compute(int[] left,int[] right)=>left.Where(x=>x>0).Join(right,x=>x,x=>x,(x,y)=>x+y).GroupBy(x=>x%2).Select(group=>group.Sum()).ToArray();
                [LinqIntercept(true)] private static bool Early(int[] left)=>left.Where(x=>x>0).Any(x=>x>1);
                public static int[] Run()=>Compute(new[]{1,2,3},new[]{2,3});
                public static bool ShortCircuit()=>Early(new[]{0,1,2});
            }
            """)],[]);
        var compiled=new RuntimeCompiler().Compile(source);Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");Assert.Equal(new[]{10},Assert.IsType<int[]>(compiled.InvokeStatic("Query","Run")));Assert.Equal(true,compiled.InvokeStatic("Query","ShortCircuit"));
        Assert.Equal(2,compiled.Result.Plans.Length);var nodes=compiled.Result.Plans.SelectMany(plan=>plan.Nodes).ToArray();
        foreach(var name in new[]{"Where","Join","GroupBy","Select","Sum","Any"}){Assert.Contains(nodes,node=>node.Kind=="SourceOperator"&&node.Label==name);}
        Assert.Contains(nodes,node=>node.Kind=="Fragment"&&node.SourcePath=="operators.cs"&&node.Length>0&&node.QueryId is not null);
        foreach(var plan in compiled.Result.Plans){Assert.Equal(plan.Nodes.Length,plan.Nodes.Select(node=>node.Id).Distinct().Count());Assert.All(plan.Nodes.SelectMany(node=>node.Inputs),input=>Assert.Contains(plan.Nodes,node=>node.Id==input));}
    }
    [Fact]public void EarlyExitAndDispose(){
        var source=new SourceSnapshot("dispose",[new("dispose.cs","""
            using System;using System.Linq;using System.Collections.Generic;using Seamless.FLinq;
            public sealed class Query{
                private static int moves,disposes;
                private static IEnumerable<int> Source(){try{moves++;yield return 1;moves++;yield return 2;moves++;yield return 3;}finally{disposes++;}}
                [LinqIntercept(true)] private static bool Early()=>Source().Where(x=>x>0).Any(x=>x==2);
                private static IEnumerable<int> FailingSource(){try{moves++;yield return 1;moves++;throw new InvalidOperationException("expected");}finally{disposes++;}}
                [LinqIntercept(true)] private static int Failure()=>FailingSource().Where(x=>x>0).Sum();
                public static int[] Run(){moves=0;disposes=0;var found=0;if(Early()){found=1;}return [found,moves,disposes];}
                public static int[] RunFailure(){moves=0;disposes=0;try{Failure();}catch(InvalidOperationException){return [moves,disposes];}throw new Exception("expected exception");}
            }
            """)],[]);
        var compiled=new RuntimeCompiler().Compile(source);Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");Assert.Equal(new[]{1,2,1},Assert.IsType<int[]>(compiled.InvokeStatic("Query","Run")));Assert.Equal(new[]{2,1},Assert.IsType<int[]>(compiled.InvokeStatic("Query","RunFailure")));Assert.NotEmpty(compiled.Result.Plans);
    }
}
