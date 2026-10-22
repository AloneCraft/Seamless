using System.Security.Cryptography;
using System.Text;
using Benchmark.Dynamic.Core;
using Core=Benchmark.Dynamic.Core;

namespace Benchmark.Dynamic.Tests;

public sealed class ElementTypeGoldenTests{
    // 要素型を一覧化する前後で、int のIRハッシュと生成ソースがバイト単位で変わらないことを固定する。
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
                    AllowedOperatorIds=capability.OperatorIds,
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
