using Benchmark.Dynamic.Core;
using Xunit.Abstractions;

namespace Benchmark.Dynamic.Tests;

public sealed class CorpusChainsTests(ITestOutputHelper output){
    private const int InputElementCount=16;

    private static CorpusEntry Entry(params string[] methods)=>new("test-"+string.Join('_',methods),"test",methods);

    [Fact,Trait("Category","Unit")]
    public void 手書きの並びは固定の引数でIRになる(){
        var document=CorpusChainBuilder.Build(Entry("Where","Select","Sum"),SourceTarget.SystemArray,InputElementCount);

        Assert.True(document.ExpectExceptions);
        Assert.Equal(QueryOperatorIds.Sum,document.Root.OperatorId);
        var select=Assert.IsType<UnaryQueryNode>(document.Root.Input);
        Assert.Equal(new SelectOperatorArguments(new(QuerySelectorKind.Multiply,3)),select.Arguments);
        var where=Assert.IsType<UnaryQueryNode>(select.Input);
        Assert.Equal(new WhereOperatorArguments(new(QueryPredicateKind.IsEven)),where.Arguments);
        Assert.IsType<SourceNode>(where.Input);
        Assert.Equal(new QueryCardinalityRange(0,InputElementCount),select.Cardinality);
        var json=QueryIrJson.Serialize(document);
        Assert.Equal(json,QueryIrJson.Serialize(QueryIrJson.Deserialize(json)));
    }

    [Fact,Trait("Category","Unit")]
    public void 二項と入れ子の演算子は右側が副Sourceになる(){
        var concat=CorpusChainBuilder.Build(Entry("Concat","Where","Count"),SourceTarget.SystemArray,InputElementCount);
        var where=Assert.IsType<UnaryQueryNode>(concat.Root.Input);
        var binary=Assert.IsType<BinaryQueryNode>(where.Input);
        Assert.Equal(QueryBinaryKind.Concat,binary.Arguments.Kind);
        Assert.Equal(SourceSlot.Secondary,Assert.IsType<SourceNode>(binary.Right).Slot);

        var join=CorpusChainBuilder.Build(Entry("Join","Sum"),SourceTarget.SystemArray,InputElementCount);
        var nested=Assert.IsType<NestedQueryNode>(join.Root.Input);
        Assert.Equal(QueryNestedKind.JoinXor,nested.Arguments.Kind);
        Assert.Equal(SourceSlot.Secondary,Assert.IsType<SourceNode>(nested.Inner).Slot);
    }

    [Fact,Trait("Category","Unit")]
    public void 作れない並びは理由つきで拒否する(){
        var selectMany=Assert.Throws<CorpusChainNotConvertibleException>(
            ()=>CorpusChainBuilder.Build(Entry("SelectMany","Sum"),SourceTarget.SystemArray,InputElementCount));
        Assert.Contains("CS1673",selectMany.Message,StringComparison.Ordinal);

        var zipAfterDistinct=Assert.Throws<CorpusChainNotConvertibleException>(
            ()=>CorpusChainBuilder.Build(Entry("Distinct","Zip","Sum"),SourceTarget.SystemArray,InputElementCount));
        Assert.Contains("ordering",zipAfterDistinct.Message,StringComparison.Ordinal);

        Assert.Throws<CorpusChainNotConvertibleException>(
            ()=>CorpusChainBuilder.Build(Entry("Where"),SourceTarget.SystemArray,InputElementCount));
        Assert.Throws<CorpusChainNotConvertibleException>(
            ()=>CorpusChainBuilder.Build(Entry("NoSuchMethod","Sum"),SourceTarget.SystemArray,InputElementCount));
    }

    [Theory,Trait("Category","Unit")]
    [InlineData("representative-chains.json",25,23)]
    [InlineData("regressions.json",200,121)]
    public void コーパスの全項目を変換した結果を数える(string fileName,int expectedEntries,int expectedConverted){
        var corpus=CorpusChainBuilder.Load(fileName);
        Assert.Equal(expectedEntries,corpus.Entries.Count);
        Assert.Equal(corpus.Entries.Count,corpus.Entries.Select(static entry=>entry.Id).Distinct().Count());

        var reasons=new Dictionary<string,int>(StringComparer.Ordinal);
        var converted=0;
        foreach(var entry in corpus.Entries){
            try{
                var document=CorpusChainBuilder.Build(entry,SourceTarget.SystemArray,InputElementCount);
                Assert.Empty(QueryIrValidator.Validate(document));
                converted++;
            }catch(CorpusChainNotConvertibleException exception){
                var key=exception.Message.Split(':')[0];
                reasons[key]=reasons.GetValueOrDefault(key)+1;
            }
        }
        output.WriteLine($"{fileName}: converted={converted}/{corpus.Entries.Count}");
        foreach(var (reason,count) in reasons.OrderByDescending(static pair=>pair.Value)){
            output.WriteLine($"  {count,4} {reason}");
        }
        Assert.Equal(expectedConverted,converted);
    }
}
