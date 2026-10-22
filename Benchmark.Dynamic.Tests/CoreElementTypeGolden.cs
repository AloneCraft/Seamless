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

    private const string GoldenDigest="509fcd68c20a2ed47a451c7d4b94516a4d694eb6ee6350cf91fb32635c8459c6";

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
                    AllowedOperatorIds=capability.OperatorIds.Where(Version12OperatorIds.Contains).ToArray(),
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
