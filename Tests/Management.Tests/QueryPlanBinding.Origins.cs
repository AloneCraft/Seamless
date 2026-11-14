using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed partial class QueryPlanBinding{
    [Fact]public void SharedHelperBindsOnlyTheSelectedDatabaseScopeCallSite(){
        var definition=global::Seamless.Management.DatabaseDesigner.Empty("scope-sites","ScopeSites");
        definition=global::Seamless.Management.DatabaseDesigner.AddSchema(definition,"dbo0");definition=global::Seamless.Management.DatabaseDesigner.AddSchema(definition,"dbo1");
        definition=definition with{Source=definition.Source with{Files=[..definition.Source.Files,new("helpers.cs","""
            using System.Linq;using Seamless.FLinq;
            namespace PlanOrigins;
            public sealed class SharedHelper{
                [LinqIntercept(true)]public static int Amount()=>new[]{1,2,3}.Where(value=>value>0).Sum();
            }
            """)]}};
        foreach(var scope in new[]{"","dbo0","dbo1"}){definition=global::Seamless.Management.DatabaseDesigner.ApplyQuery(definition,new("Amount",scope,DatabaseQueryKind.ScalarFunction,[],[],"int","return global::PlanOrigins.SharedHelper.Amount();"));}
        using var compiled=new RuntimeCompiler().Compile(definition.Source);
        Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");
        var helperPlans=compiled.Result.Plans.Where(plan=>plan.MethodKey=="PlanOrigins.SharedHelper.Amount()").ToArray();Assert.Equal(3,helperPlans.Length);
        var sites=new List<ExecutionPlanCallSite>();
        foreach(var scope in new[]{"","dbo0.","dbo1."}){
            var selection=global::Seamless.Management.DatabaseDesigner.SelectQueryPlans(definition,scope+"Amount",compiled.Result.Plans);
            var selected=Assert.Single(selection.Plans);var site=Assert.IsType<ExecutionPlanCallSite>(selected.CallSite);sites.Add(site);
            Assert.Equal("PlanOrigins.SharedHelper.Amount()",selected.MethodKey);Assert.Null(selection.UnavailableReason);
            Assert.Equal("global::PlanOrigins.SharedHelper.Amount()",definition.Source.Files.Single(file=>file.Path==site.Path).Text.Substring(site.Start,site.Length));
            var prefix=scope.Replace(".","",StringComparison.Ordinal);if(prefix.Length>0){Assert.Contains(prefix,site.Caller);}
        }
        Assert.Equal(3,sites.Distinct().Count());
        var legacy=compiled.Result.Plans.Select(plan=>plan with{CallSite=null}).ToArray();
        var ambiguous=global::Seamless.Management.DatabaseDesigner.SelectQueryPlans(definition,"Amount",legacy);
        Assert.Empty(ambiguous.Plans);Assert.Contains("複数計画",ambiguous.UnavailableReason);
    }
    [Fact]public void GenericAndReducedExtensionCallsUseTheirResolvedDeclaredTarget(){
        var source=new SourceSnapshot("generic-sites",[new("generic-sites.cs","""
            using System.Linq;using Seamless.FLinq;
            namespace PlanOrigins;
            public sealed class Query{
                public static int Run()=>new[]{1,2}.CountPlan();
                [LinqIntercept(true)]public static int CountValues<T>(T[] values)=>values.Count();
            }
            public static class Extensions{
                public static int CountPlan<T>(this T[] values)=>Query.CountValues(values);
            }
            """)],[]);
        using var compiled=new RuntimeCompiler().Compile(source);
        Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");Assert.Equal(2,compiled.InvokeStatic("PlanOrigins.Query","Run"));
        var definition=new Definition("generic-sites","Plans",DefinitionKind.Query,"PlanOrigins.Query","Run",source);
        var selection=global::Seamless.Management.DatabaseDesigner.SelectQueryPlans(definition,definition.Id,compiled.Result.Plans);
        var selected=Assert.Single(selection.Plans);Assert.Equal("PlanOrigins.Query.CountValues<T>(T[])",selected.MethodKey);
        Assert.Equal("PlanOrigins.Extensions.CountPlan<T>(T[])",selected.CallSite!.Caller);Assert.Null(selection.UnavailableReason);
    }
    [Fact]public void UnusedOpaqueDelegatesAndLocalDefinitionsDoNotBecomePlanOrigins(){
        var source=new SourceSnapshot("origins",[new("origins.cs","""
            using System;using System.Linq;using Seamless.FLinq;
            namespace PlanOrigins;
            public sealed class Query{
                public static int Run(){
                    Func<int> unused=()=>Wrong();
                    Func<int> cast=(Func<int>)(()=>CastWrong());
                    int UnusedLocal()=>LocalWrong();
                    int UsedLocal()=>LocalRight();
                    return Right()+UsedLocal()+Opaque(()=>OpaqueWrong());
                }
                private static int Opaque(Func<int> factory)=>0;
                [LinqIntercept(true)] private static int Right()=>new[]{1}.Where(x=>x>0).Sum();
                [LinqIntercept(true)] private static int LocalRight()=>new[]{2}.Where(x=>x>0).Sum();
                [LinqIntercept(true)] private static int Wrong()=>new[]{3}.Where(x=>x>0).Sum();
                [LinqIntercept(true)] private static int CastWrong()=>new[]{4}.Where(x=>x>0).Sum();
                [LinqIntercept(true)] private static int LocalWrong()=>new[]{5}.Where(x=>x>0).Sum();
                [LinqIntercept(true)] private static int OpaqueWrong()=>new[]{6}.Where(x=>x>0).Sum();
            }
            """)],[]);
        using var compiled=new RuntimeCompiler().Compile(source);
        Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");
        Assert.Equal(3,compiled.InvokeStatic("PlanOrigins.Query","Run"));
        var definition=new Definition("origins","Plans",DefinitionKind.Query,"PlanOrigins.Query","Run",source);
        var selection=global::Seamless.Management.DatabaseDesigner.SelectQueryPlans(definition,definition.Id,compiled.Result.Plans);
        Assert.Contains(selection.Plans,plan=>plan.MethodKey=="PlanOrigins.Query.Right()");
        Assert.Contains(selection.Plans,plan=>plan.MethodKey=="PlanOrigins.Query.LocalRight()");
        Assert.DoesNotContain(selection.Methods,method=>method.Contains("Wrong",StringComparison.Ordinal));
    }

    [Fact]public void GetterAndReducedExtensionResolveToTheDeclaredPlanMethod(){
        var source=new SourceSnapshot("getter",[new("getter.cs","""
            using System.Linq;using Seamless.FLinq;
            namespace PlanOrigins;
            public sealed class Query{
                public static int Run()=>Value;
                private static int Value=>new[]{1,2,3}.SumPlan();
                [LinqIntercept(true)] public static int SumCore(int[] values)=>values.Where(x=>x>1).Sum();
            }
            public static class Extensions{
                public static int SumPlan(this int[] values)=>Query.SumCore(values);
            }
            """)],[]);
        using var compiled=new RuntimeCompiler().Compile(source);
        Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");
        Assert.Equal(5,compiled.InvokeStatic("PlanOrigins.Query","Run"));
        var definition=new Definition("getter","Plans",DefinitionKind.Query,"PlanOrigins.Query","Run",source);
        var selection=global::Seamless.Management.DatabaseDesigner.SelectQueryPlans(definition,definition.Id,compiled.Result.Plans);
        Assert.Equal("PlanOrigins.Query.SumCore(int[])",Assert.Single(selection.Plans).MethodKey);
        Assert.Contains("PlanOrigins.Extensions.SumPlan(int[])",selection.Methods);
        Assert.Contains(selection.Methods,method=>method.Contains("Value.get",StringComparison.Ordinal));
    }

    [Fact]public void ViewAndFunctionBodiesKeepTheirActualHelperOrigins(){
        // ビュー・関数は IReader への拡張メンバー。本体のラムダから呼ぶ補助の計画を、その補助の出どころとして選ぶ。
        var definition=global::Seamless.Management.DatabaseDesigner.Apply(global::Seamless.Management.DatabaseDesigner.Empty("wrapper","Plans"),new("Items","",[new("Id","int",true)],[]));
        definition=definition with{Source=definition.Source with{Files=[..definition.Source.Files,new("helpers.cs","""
            using System.Linq;using Seamless.FLinq;
            namespace PlanOrigins;
            public sealed class Helpers{
                [LinqIntercept(true)] public static int[] ViewRows()=>new[]{1,2}.Where(x=>x>0).ToArray();
                [LinqIntercept(true)] public static int[] FunctionRows()=>new[]{3,4}.Where(x=>x>0).ToArray();
                [LinqIntercept(true)] public static int Amount()=>new[]{5,6}.Where(x=>x>0).Sum();
            }
            """)]}};
        definition=global::Seamless.Management.DatabaseDesigner.ApplyQuery(definition,new("Values","",DatabaseQueryKind.View,[new("Id","int",false)],[],"","return q.Items.Where(row=>global::PlanOrigins.Helpers.ViewRows().Contains(row.Id)).Select(row=>new Values(row.Id));"));
        definition=global::Seamless.Management.DatabaseDesigner.ApplyQuery(definition,new("Amount","",DatabaseQueryKind.ScalarFunction,[],[],"int","return new[]{1}.Select(value=>global::PlanOrigins.Helpers.Amount()).Sum();"));
        definition=global::Seamless.Management.DatabaseDesigner.ApplyQuery(definition,new("Find","",DatabaseQueryKind.TableFunction,[new("Id","int",false)],[],"ISet<FindResult>","return q.Items.Where(row=>global::PlanOrigins.Helpers.FunctionRows().Contains(row.Id)).Select(row=>new FindResult(row.Id));"));
        using var compiled=new RuntimeCompiler().Compile(definition.Source);
        Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");
        foreach(var (target,helper) in new[]{("Values","ViewRows"),("Amount","Amount"),("Find","FunctionRows")}){
            var selection=global::Seamless.Management.DatabaseDesigner.SelectQueryPlans(definition,target,compiled.Result.Plans);
            Assert.Contains(selection.Plans,plan=>plan.MethodKey=="PlanOrigins.Helpers."+helper+"()");
            Assert.DoesNotContain(selection.Plans,plan=>plan.MethodKey.StartsWith("PlanOrigins.Helpers.",StringComparison.Ordinal)&&plan.MethodKey!="PlanOrigins.Helpers."+helper+"()");
        }
    }

    [Fact]public void OverloadedEntryHasAnExplicitUnavailableContract(){
        var source=new SourceSnapshot("overload",[new("overload.cs","""
            using System.Linq;using Seamless.FLinq;
            namespace PlanOrigins;
            public sealed class Query{
                public static int Run()=>Sum(new[]{1});
                public static int Run(int value)=>Sum(new[]{value});
                [LinqIntercept(true)] private static int Sum(int[] values)=>values.Where(x=>x>0).Sum();
            }
            """)],[]);
        using var compiled=new RuntimeCompiler().Compile(source);
        Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");
        var definition=new Definition("overload","Plans",DefinitionKind.Query,"PlanOrigins.Query","Run",source);
        var error=Assert.Throws<ManagementException>(()=>global::Seamless.Management.DatabaseDesigner.SelectQueryPlans(definition,definition.Id,compiled.Result.Plans));
        Assert.Equal("Unsupported",error.Code);
    }
}
