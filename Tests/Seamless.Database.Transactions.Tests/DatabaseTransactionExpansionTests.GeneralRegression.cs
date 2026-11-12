using Microsoft.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;
namespace Seamless.Database.Transactions.Tests;

public sealed partial class DatabaseTransactionExpansionTests{
    [Fact]
    public void GeneralGroupingAndLazyEvaluationRegressionsUseInternalTuplesWithoutWarnings(){
        var source="using System;using System.Collections.Generic;using System.Linq;using Xunit;namespace Test.System.Linq.Intercept;";
        foreach(var resource in new[]{"GeneratorRegression.GroupByOrder.cs","GeneratorRegression.LazyEvaluationCounts.cs"}){
            using var stream=typeof(DatabaseTransactionExpansionTests).Assembly.GetManifestResourceStream(resource)??throw new InvalidOperationException("Missing regression source: "+resource);using var reader=new StreamReader(stream);
            source+=reader.ReadToEnd().Replace("namespace Test.System.Linq.Intercept;",string.Empty,StringComparison.Ordinal);
        }
        source+="""
            public sealed class OrderingComparers{
                private static readonly IComparer<int> Reverse=Comparer<int>.Create(static(left,right)=>right.CompareTo(left));
                [global::Seamless.FLinq.LinqIntercept(true)]private static int[] Defaults(IEnumerable<int> rows)=>rows.OrderBy(static value=>value%3).ThenBy(static value=>value%5).ToArray();
                [global::Seamless.FLinq.LinqIntercept(true)]private static int[] CustomFirst(IEnumerable<int> rows)=>rows.OrderBy(static value=>value%3,Reverse).ThenBy(static value=>value%5).ToArray();
                [global::Seamless.FLinq.LinqIntercept(true)]private static int[] CustomSecond(IEnumerable<int> rows)=>rows.OrderBy(static value=>value%3).ThenBy(static value=>value%5,Reverse).ToArray();
                [global::Seamless.FLinq.LinqIntercept(true)]private static int[] Both(IEnumerable<int> rows)=>rows.OrderBy(static value=>value%3,Reverse).ThenBy(static value=>value%5,Reverse).ToArray();
                [Fact]public void KeepsMixedComparers(){
                    var rows=Enumerable.Range(0,24).Select(static value=>value*7%19).ToArray();
                    Assert.Equal(rows.OrderBy(static value=>value%3).ThenBy(static value=>value%5),Defaults(rows));
                    Assert.Equal(rows.OrderBy(static value=>value%3,Reverse).ThenBy(static value=>value%5),CustomFirst(rows));
                    Assert.Equal(rows.OrderBy(static value=>value%3).ThenBy(static value=>value%5,Reverse),CustomSecond(rows));
                    Assert.Equal(rows.OrderBy(static value=>value%3,Reverse).ThenBy(static value=>value%5,Reverse),Both(rows));
                }
            }
            """;
        IReadOnlyList<Diagnostic> observed=[];var debug=false;
#if DEBUG
        debug=true;
#endif
        var(output,generated)=Generate(source,"GeneralGroupingRegression",debug,generatedEvidencePath:Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"general-grouping.g.cs"),observeDiagnostics:diagnostics=>observed=diagnostics);
        var warnings=observed.Where(static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Warning).ToArray();
        // 中間匿名型のValueTuple化ができる形は、元の部分最適化の警告を必要としない。
        Assert.Empty(warnings);
        var proof=string.Join(Environment.NewLine,generated);Assert.Contains("OptimizedLinq_SelectOrderBy_Optimized",proof);Assert.Contains("OptimizedLinq_JoinEmptyOuterImpl",proof);Assert.Contains("OptimizedLinq_CustomFirst",proof);
        Assert.Contains("OptimizedLinq_SelectAnonymousOrderBy_Optimized",proof);
        Assert.DoesNotContain("__TsavoriteAnonRecords_",proof);
        using var image=new MemoryStream();var emitted=output.Emit(image);Assert.True(emitted.Success,string.Join(Environment.NewLine,emitted.Diagnostics));image.Position=0;
        var load=new AssemblyLoadContext("general-grouping-"+Guid.NewGuid(),isCollectible:true);load.Resolving+=static(_,name)=>AssemblyLoadContext.Default.LoadFromAssemblyName(name);
        try{
            var assembly=load.LoadFromStream(image);var executed=0;
            foreach(var type in assembly.GetTypes().Where(static type=>type.FullName is "Test.System.Linq.Intercept.GroupByOrder" or "Test.System.Linq.Intercept.LazyEvaluationCounts" or "Test.System.Linq.Intercept.OrderingComparers")){
                var instance=Activator.CreateInstance(type);
                foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).Where(static method=>method.GetCustomAttribute<FactAttribute>() is not null)){
                    try{method.Invoke(instance,null);executed++;}catch(TargetInvocationException error){ExceptionDispatchInfo.Capture(error.InnerException!).Throw();}
                }
            }
            Assert.Equal(30,executed);
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"general-grouping-regression.json"),global::System.Text.Json.JsonSerializer.Serialize(new{Executed=executed,Warnings=warnings.Length,GeneratedFiles=generated.Length}));
        }finally{load.Unload();}
    }
}
