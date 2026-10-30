using Benchmark.Dynamic.Core;
using Core=Benchmark.Dynamic.Core;

namespace Benchmark.Dynamic.Tests;

public sealed class ExpectExceptionsTests{
    private static RandomQueryGenerationOptions Options(bool expectExceptions,params string[] allowed){
        return new RandomQueryGenerationOptions{
            MasterSeed=Core.Seed256.Parse("0xEC0"),
            QueryIndex=0,
            SourceTarget=SourceTarget.SystemArray,
            OperatorCount=2,
            MaximumDepth=2,
            InputElementCount=16,
            EnableBinaryOperators=false,
            EnableNestedOperators=false,
            AllowedOperatorIds=allowed,
            ExpectExceptions=expectExceptions,
        };
    }

    [Fact,Trait("Category","Unit")]
    public void 例外を許さない既定では空になりうる入力のFirstは生成されない(){
        var exception=Assert.Throws<InvalidOperationException>(
            ()=>new Core.RandomQueryGenerator().Generate(Options(false,QueryOperatorIds.Where,QueryOperatorIds.First)));
        Assert.Contains("No safe terminal",exception.Message,StringComparison.Ordinal);
    }

    [Fact,Trait("Category","Unit")]
    public void 例外を許すと空になりうる入力のFirstも生成されIR往復でも保たれる(){
        var document=new Core.RandomQueryGenerator().Generate(Options(true,QueryOperatorIds.Where,QueryOperatorIds.First));

        Assert.True(document.ExpectExceptions);
        Assert.Equal(QueryOperatorIds.First,document.Root.OperatorId);
        Assert.False(document.Root.Input.Cardinality.IsDefinitelyNonEmpty);
        var json=QueryIrJson.Serialize(document);
        Assert.Contains("\"expectExceptions\":true",json,StringComparison.Ordinal);
        Assert.True(QueryIrJson.Deserialize(json).ExpectExceptions);
    }

    [Fact,Trait("Category","Unit")]
    public void 旗を外すと静的な安全検査が再び効く(){
        var document=new Core.RandomQueryGenerator().Generate(Options(true,QueryOperatorIds.Where,QueryOperatorIds.First));

        var errors=QueryIrValidator.Validate(document with{ExpectExceptions=false});

        Assert.Contains(errors,static error=>error.Contains("statically non-empty",StringComparison.Ordinal));
    }

    [Fact,Trait("Category","Unit")]
    public void 例外を許さない通常のIRのJSONには旗が出ない(){
        var document=new Core.RandomQueryGenerator().Generate(Options(false,QueryOperatorIds.Where,QueryOperatorIds.Count));

        Assert.False(document.ExpectExceptions);
        Assert.DoesNotContain("expectExceptions",QueryIrJson.Serialize(document),StringComparison.Ordinal);
    }

    [Fact,Trait("Category","Unit")]
    public void 例外を許すとElementAtは範囲外の添字も生成する(){
        var generator=new Core.RandomQueryGenerator();
        var outOfRange=0;
        for(var index=0;index<64;index++){
            var document=generator.Generate(Options(true,QueryOperatorIds.Select,QueryOperatorIds.ElementAt) with{QueryIndex=index});
            Assert.Empty(QueryIrValidator.Validate(document));
            if(document.Root.Arguments.Value>=16){
                outOfRange++;
            }
        }
        Assert.InRange(outOfRange,1,63);
    }
}
