using System.Security.Cryptography;
using System.Text;
using Benchmark.Dynamic.Core;
using Core=Benchmark.Dynamic.Core;

namespace Benchmark.Dynamic.Tests;

public sealed class ElementTypeGoldenTests{
    // 要素型を一覧化する前後で、int のIRハッシュと生成ソースがバイト単位で変わらないことを固定する。
    // 版12時点の演算子ID。以後に演算子を足しても、この基準は変わらない。
    private static readonly HashSet<string> Version12OperatorIds=new(StringComparer.Ordinal){
        "where","where.indexedModulo","select","select.indexedXor","distinct","distinctBy.modulo","take","skip",
        "reverse","orderBy","thenBy","concat","union","except","intersect","unionBy.identity","exceptBy.identity",
        "intersectBy.identity","selectMany.xor","join.xor","groupJoin.xorCount","count","longCount","any","all",
        "contains","sum","min","max","minBy.identity","maxBy.identity","average","aggregate.xor",
        "materialize.intPair","materialize.intGroup","first","firstOrDefault","last","lastOrDefault","elementAt",
        "elementAtOrDefault","toArray","toList","toHashSet","toDictionary.identity","toLookup.modulo",
    };

    // 2026-11-12 に取り直した。前の基準(509fcd68...)から、意図した変更3つで生成物が変わっていた:
    // 09e365ec(配列の Union で右辺を ToArray しない)、dfdf10e1(int スカラー結果の生成)、ea9823a3(ThenBy は OrderBy/ThenBy の直後だけ)。
    // 同じ日に演算子を足したが、この基準は版12の演算子だけを使うので、演算子の追加では変わらないことを確かめた
    // (追加の前後で同じ 27cb53c9... になる)。
    private const string GoldenDigest="27cb53c988d7ae4b735a93d95286ecbd298325ee85286c4be4d63d8d1d0c54d6";

    [Fact,Trait("Category","Unit")]
    public void int要素のIRハッシュと生成ソースは固定SeedでBit一致する(){
        var generator=new Core.RandomQueryGenerator();
        var emitter=new Core.QuerySourceEmitter();
        var lines=new StringBuilder();
        foreach(var target in Enum.GetValues<SourceTarget>()){
            var capability=QueryCapabilityProfiles.CreateSet(OperatorSetMode.Maximum,[target]);
            for(var index=0;index<64;index++){
                var document=generator.Generate(new RandomQueryGenerationOptions{
                    MasterSeed=Core.Seed256.Parse("0xE1E3E7"),
                    QueryIndex=index,
                    SourceTarget=target,
                    OperatorCount=1+index%12,
                    MaximumDepth=12,
                    InputElementCount=32,
                    EnableBinaryOperators=true,
                    EnableNestedOperators=true,
                    AllowedOperatorIds=capability.OperatorIds
                        // selectMany.xor は、基準を取った時点では FLinq 非対応で必ず除かれていた。2026-10-22 に FLinq を直して対応したが、
                        // この基準(int の出力が変わらないこと)を保つため、引き続き除く。
                        .Where(id=>Version12OperatorIds.Contains(id)&&id!=QueryOperatorIds.SelectManyXor&&QueryCapabilityProfiles.Get(target).SupportsOnAllPaths(id))
                        .ToArray(),
                });
                var emitted=emitter.Emit(document);
                lines.Append(target).Append('|').Append(index).Append('|')
                    .Append(QueryIrJson.ComputeHash(document)).Append('|')
                    .Append(emitted.SourceHash).Append('\n');
            }
        }
        var digest=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(lines.ToString())));
        Assert.True(GoldenDigest==digest,$"actual digest: {digest}");
    }
}
