using Benchmark.Dynamic.Core;

namespace Benchmark.Dynamic.Tests;

public sealed class QueryShrinkerTests{
    [Fact,Trait("Category","Unit")]
    public void 候補順序はTerminal_Unary_BinaryNested_Predicate_Selector_Inputで決定的(){
        var query=new RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
            MasterSeed=Seed256.Parse("0x5151"),
            QueryIndex=0,
            SourceTarget=SourceTarget.SystemArray,
            OperatorCount=3,
            MaximumDepth=3,
            InputElementCount=32,
            EnableBinaryOperators=false,
            EnableNestedOperators=false,
            AllowedOperatorIds=[QueryOperatorIds.Where,QueryOperatorIds.Select,QueryOperatorIds.ToArray],
        });

        var candidates=new QueryShrinker().EnumerateCandidates(query,32);

        Assert.StartsWith("terminal.",candidates[0].DerivationKind,StringComparison.Ordinal);
        var unaryIndex=Array.FindIndex(candidates.ToArray(),static item=>item.DerivationKind.StartsWith("unary.",StringComparison.Ordinal));
        var predicateIndex=Array.FindIndex(candidates.ToArray(),static item=>item.DerivationKind.StartsWith("predicate.",StringComparison.Ordinal));
        var inputIndex=Array.FindIndex(candidates.ToArray(),static item=>item.DerivationKind.StartsWith("input.",StringComparison.Ordinal));
        Assert.True(unaryIndex>0);
        Assert.True(predicateIndex>unaryIndex);
        Assert.True(inputIndex>predicateIndex);
        Assert.All(candidates,static candidate=>{
            Assert.Empty(QueryIrValidator.Validate(candidate.Query));
            Assert.Equal(candidate.Query.ActualOperatorCount,candidate.Query.RequestedOperatorCount);
            Assert.Equal(candidate.Query.ActualDepth,candidate.Query.MaximumDepth);
        });
    }

    [Fact,Trait("Category","Unit")]
    public async Task 同じ失敗署名だけをgreedy採用してTerminal一個と入力一個まで縮小する(){
        var original=new RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
            MasterSeed=Seed256.Parse("0x7777"),
            QueryIndex=0,
            SourceTarget=SourceTarget.SystemArray,
            OperatorCount=4,
            MaximumDepth=4,
            InputElementCount=64,
            EnableBinaryOperators=false,
            EnableNestedOperators=false,
            AllowedOperatorIds=[QueryOperatorIds.Where,QueryOperatorIds.Select,QueryOperatorIds.Distinct,QueryOperatorIds.ToArray],
        });
        var signature=new FailureSignature("SemanticMismatch",string.Empty,string.Empty,"index=0",string.Empty);
        var shrinker=new QueryShrinker();

        var result=await shrinker.ShrinkAsync(
            new ShrinkRequest(original,64,signature,128,5_000),
            (candidate,_)=>ValueTask.FromResult<FailureSignature?>(
                candidate.Query.Root.Arguments.Kind==QueryTerminalKind.Count?signature:null));

        Assert.Equal(1,result.ShrunkQuery.ActualOperatorCount);
        Assert.Equal(QueryTerminalKind.Count,result.ShrunkQuery.Root.Arguments.Kind);
        Assert.Equal(1,result.ShrunkInputElementCount);
        Assert.NotEmpty(result.AppliedDerivations);
        Assert.False(result.TimedOut);
        Assert.Empty(QueryIrValidator.Validate(result.ShrunkQuery));
    }

    [Fact,Trait("Category","Unit")]
    public void FailureSignatureは同じ値から同じcanonicalJSONとhashを作る(){
        var first=new FailureSignature("CompilationFailed","CS1001","InvalidOperationException","x","CycleDetected");
        var second=first with{};
        Assert.Equal(first.ToCanonicalJson(),second.ToCanonicalJson());
        Assert.Equal(first.ComputeHash(),second.ComputeHash());
    }

    [Fact,Trait("Category","Unit")]
    public void WorkerArtifactsから診断_例外_差分_非収束理由をcanonical化する(){
        var diagnostics="""[{"stage":"Generator","severity":"Error","id":"CS1001","message":"x","location":""}]""";
        var results="""{"exceptions":{"zLinq":{"type":"System.InvalidOperationException","message":"x"}},"comparisons":{"fLinq":{"isMatch":false,"difference":"index=2"}}}""";
        var inner="""{"LayerTerminations":[{"RunId":1,"Layer":"Syntax","RoundCount":128,"Termination":"IterationLimit"}]}""";
        var trace=System.Text.Json.JsonSerializer.Serialize(new[]{inner});

        var signature=FailureSignatureFactory.FromWorkerArtifacts(
            WorkerExecutionStatus.OptimizationNonConverged,
            diagnostics,
            results,
            trace);

        Assert.Equal(nameof(WorkerExecutionStatus.OptimizationNonConverged),signature.Status);
        Assert.Equal("CS1001",signature.DiagnosticId);
        Assert.Equal("System.InvalidOperationException",signature.ExceptionType);
        Assert.Equal("index=2",signature.FirstDifference);
        Assert.Equal("IterationLimit",signature.NonConvergenceReason);
    }
}
