using System.Text.Json;

namespace Benchmark.Dynamic.Core;

public sealed record LegacyOperator(string Name,string Call,bool Terminal,bool Second,bool Unordered,string OriginalSHA256);

/// <summary>旧検証器の演算子の引数を変えずに保持する。IRで表せない並びも固定回帰に使える。</summary>
public static class LegacyChainCatalog{
    public static IReadOnlyList<LegacyOperator> Operators{get;}=Load();
    private static LegacyOperator[] Load(){
        using var stream=typeof(LegacyChainCatalog).Assembly.GetManifestResourceStream("Benchmark.Dynamic.Core.FixedCases.legacy-operators.json")!;
        return JsonSerializer.Deserialize<LegacyOperator[]>(stream)??throw new InvalidDataException("Legacy operators are missing.");
    }
    public static FixedCase Build(string id,params string[] methods){
        var operators=methods.Select(name=>Operators.Single(item=>item.Name==name)).ToArray();
        var expression="source"+string.Concat(operators.Select(item=>item.Call));
        var terminal=methods[^1];var type="Int";var cardinality="Scalar";
        if(terminal is "Sum" or "LongCount" or "Aggregate"){type="Long";}
        if(terminal is "Any" or "Any_Pred" or "All" or "Contains"){type="Bool";}
        if(terminal=="Average"){type="Double";}
        if(terminal is "ToArray" or "ToList" or "ToHashSet" or "CountBy_ToArray" or "AggregateBy_ToArray"){cardinality="Sequence";}
        if(terminal=="ToDictionary"){
            type="IntPair";cardinality="Sequence";expression="global::Benchmark.Dynamic.Core.DynamicQueryMaterializer.CreateCanonicalDictionaryPairs("+expression+")";
        }
        if(terminal=="ToLookup"){
            type="IntGroup";cardinality="Sequence";expression="global::Benchmark.Dynamic.Core.DynamicQueryMaterializer.CreateCanonicalLookupGroups("+expression+")";
        }
        return new(id,methods,expression,null,type,cardinality,operators[0].OriginalSHA256,operators.Any(item=>item.Unordered));
    }
    public static IEnumerable<FixedCase> Pairs(){
        foreach(var item in FixedCaseCatalog.All.Take(15)){yield return item;}
        foreach(var intermediate in Operators.Where(item=>!item.Terminal)){
            foreach(var terminal in new[]{"ToArray","Count","Sum","FirstOrDefault","Any"}){
                if(FixedCaseCatalog.All.Take(15).Any(item=>item.Methods.SequenceEqual(new[]{intermediate.Name,terminal}))){continue;}
                yield return Build("legacy-pair-"+intermediate.Name+"-"+terminal,intermediate.Name,terminal);
            }
        }
    }
    public static IEnumerable<FixedCase> Triples(){
        foreach(var first in Operators.Where(item=>!item.Terminal)){
            foreach(var second in Operators.Where(item=>!item.Terminal)){
                if(first.Name=="SelectMany"&&second.Name=="SelectMany"){continue;}
                foreach(var terminal in Operators.Where(item=>item.Terminal)){
                    yield return Build("legacy-triple-"+first.Name+"-"+second.Name+"-"+terminal.Name,first.Name,second.Name,terminal.Name);
                }
            }
        }
    }
}
