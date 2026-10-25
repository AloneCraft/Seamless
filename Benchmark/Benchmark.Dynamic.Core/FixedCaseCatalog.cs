using System.Text;
using System.Text.Json;

namespace Benchmark.Dynamic.Core;

public sealed record FixedCase(string ID,string[] Methods,string Expression,string? IdealBody,string ValueType,string Cardinality,string OriginalSHA256,bool Unordered=false);

/// <summary>旧検証器の25個の手書き理想を、式と対応させて原文のまま保持する。</summary>
public static class FixedCaseCatalog{
    public static IReadOnlyList<FixedCase> All{get;}=Load();
    private static FixedCase[] Load(){
        using var stream=typeof(FixedCaseCatalog).Assembly.GetManifestResourceStream("Benchmark.Dynamic.Core.FixedCases.chains.json")!;
        return JsonSerializer.Deserialize<FixedCase[]>(stream)??throw new InvalidDataException("Fixed cases are missing.");
    }
    public static string Emit(FixedCase item,int flags=15){
        if(flags is <0 or >15){throw new ArgumentOutOfRangeException(nameof(flags));}
        var builder=new StringBuilder("using System;using System.Linq;using System.Collections.Generic;using Benchmark.Dynamic.Core;namespace Benchmark.Dynamic.Generated;public sealed class FixedQuery{\n");
        var order="Ordered";if(item.Unordered){order="Unordered";}
        var materialize=$"global::Benchmark.Dynamic.Core.DynamicQueryValue.FromObject(result,global::Benchmark.Dynamic.Core.QueryValueType.{item.ValueType},global::Benchmark.Dynamic.Core.QueryCardinality.{item.Cardinality},global::Benchmark.Dynamic.Core.QueryOrderSemantics.{order})";
        foreach(var method in new[]{"RunSystem","RunFLinqUnoptimized","RunFLinq"}){
            var core=method;
            if(method!="RunSystem"){
                core+="Core";
                builder.Append($"public static global::Benchmark.Dynamic.Core.DynamicQueryValue {method}(object? primary,object? secondary)=>{core}(primary,secondary);\n");
                var options=0;if(method=="RunFLinq"){options=flags;}
                builder.Append($"[global::Seamless.FLinq.LinqIntercept(true,Optimizations=(global::Seamless.FLinq.LinqOptimizations){options},ConvergenceMode=global::Seamless.FLinq.LinqConvergenceMode.GlobalFixedPoint,EmitOptimizationTrace=true)]\n");
            }
            builder.Append($"public static global::Benchmark.Dynamic.Core.DynamicQueryValue {core}(object? primary,object? secondary){{var source=(int[])primary!;var second=(int[])secondary!;var result={item.Expression};return {materialize};}}\n");
        }
        if(item.IdealBody is not null){
            builder.Append($"public static global::Benchmark.Dynamic.Core.DynamicQueryValue RunIdeal(object? primary,object? secondary){{var result=IdealBody((int[])primary!,(int[])secondary!);return {materialize};}}\n");
            builder.Append("private static object IdealBody(int[] raw,int[] second){\n").Append(item.IdealBody).AppendLine("\n}");
        }
        builder.AppendLine("}");
        return builder.ToString();
    }
}
