using Benchmark.Dynamic.Core;
using Core=Benchmark.Dynamic.Core;

namespace Benchmark.Dynamic.Tests;

public sealed class ExtendedOperatorTests{
    public static TheoryData<string,string> 演算子と生成される呼び出し=>new(){
        {QueryOperatorIds.TakeLast,"TakeLast("},
        {QueryOperatorIds.SkipLast,"SkipLast("},
        {QueryOperatorIds.TakeWhile,"TakeWhile("},
        {QueryOperatorIds.SkipWhile,"SkipWhile("},
        {QueryOperatorIds.Append,"Append("},
        {QueryOperatorIds.Prepend,"Prepend("},
        {QueryOperatorIds.DefaultIfEmpty,"DefaultIfEmpty("},
        {QueryOperatorIds.OrderByKey,"OrderBy"},
        {QueryOperatorIds.GroupBySum,"GroupBy("},
        {QueryOperatorIds.GroupByCount,"GroupBy("},
        {QueryOperatorIds.GroupByKeyOnly,"GroupBy("},
    };

    [Theory,Trait("Category","Unit")]
    [MemberData(nameof(演算子と生成される呼び出し))]
    public void 追加した単項演算子はIR往復と4経路の生成ができる(string operatorId,string expectedCall){
        var generator=new Core.RandomQueryGenerator();
        var emitter=new Core.QuerySourceEmitter();
        for(var index=0;index<32;index++){
            var document=generator.Generate(new RandomQueryGenerationOptions{
                MasterSeed=Core.Seed256.Parse("0xE17E"),
                QueryIndex=index,
                SourceTarget=SourceTarget.SystemArray,
                OperatorCount=2,
                MaximumDepth=2,
                InputElementCount=16,
                EnableBinaryOperators=false,
                EnableNestedOperators=false,
                AllowedOperatorIds=[operatorId,QueryOperatorIds.Count],
            });

            var json=QueryIrJson.Serialize(document);
            var restored=QueryIrJson.Deserialize(json);
            Assert.Equal(json,QueryIrJson.Serialize(restored));
            Assert.Empty(QueryIrValidator.Validate(restored));

            var unary=Assert.IsType<UnaryQueryNode>(document.Root.Input);
            Assert.Equal(operatorId,unary.OperatorId);
            Assert.IsAssignableFrom<ExtendedUnaryOperatorArguments>(unary.Arguments);

            var emitted=emitter.Emit(document);
            Assert.Contains(expectedCall,emitted.SourceText,StringComparison.Ordinal);
            Assert.Equal(QueryZLinqSupport.IsSupported(document)?4:3,emitted.MethodNames.Count);
            Assert.False(string.IsNullOrWhiteSpace(Core.QueryExpressionFormatter.Format(document)));
        }
    }

    [Fact,Trait("Category","Unit")]
    public void OrderBy系を含むクエリはZLinqの経路を作らない(){
        var generator=new Core.RandomQueryGenerator();
        var emitter=new Core.QuerySourceEmitter();
        foreach(var operatorId in new[]{QueryOperatorIds.OrderBy,QueryOperatorIds.OrderByKey}){
            var document=generator.Generate(new RandomQueryGenerationOptions{
                MasterSeed=Core.Seed256.Parse("0x0DE7"),
                QueryIndex=0,
                SourceTarget=SourceTarget.SystemArray,
                OperatorCount=2,
                MaximumDepth=2,
                InputElementCount=16,
                EnableBinaryOperators=false,
                EnableNestedOperators=false,
                AllowedOperatorIds=[operatorId,QueryOperatorIds.ToArray],
            });
            var emitted=emitter.Emit(document);
            Assert.False(QueryZLinqSupport.IsSupported(document));
            Assert.DoesNotContain("RunZLinq",emitted.SourceText,StringComparison.Ordinal);
            Assert.False(emitted.MethodNames.ContainsKey(ExecutionPath.ZLinq));
            Assert.Equal(3,emitted.MethodNames.Count);
        }

        var plain=generator.Generate(new RandomQueryGenerationOptions{
            MasterSeed=Core.Seed256.Parse("0x0DE7"),
            QueryIndex=0,
            SourceTarget=SourceTarget.SystemArray,
            OperatorCount=2,
            MaximumDepth=2,
            InputElementCount=16,
            EnableBinaryOperators=false,
            EnableNestedOperators=false,
            AllowedOperatorIds=[QueryOperatorIds.Where,QueryOperatorIds.ToArray],
        });
        Assert.True(QueryZLinqSupport.IsSupported(plain));
        Assert.Equal(4,emitter.Emit(plain).MethodNames.Count);
    }

    [Fact,Trait("Category","Unit")]
    public void 演算子集合はZLinq非対応の演算子を含めFLinqの不具合が直ったSelectManyも含む(){
        foreach(var mode in Enum.GetValues<OperatorSetMode>()){
            var set=QueryCapabilityProfiles.CreateSet(mode,[SourceTarget.SystemArray]);
            Assert.Contains(QueryOperatorIds.OrderBy,set.OperatorIds);
            Assert.Contains(QueryOperatorIds.OrderByKey,set.OperatorIds);
            Assert.Contains(QueryOperatorIds.SelectManyXor,set.OperatorIds);
        }
    }

    public static TheoryData<string,string> 追加した終端演算子=>new(){
        {QueryOperatorIds.CountWhere,"Count("},
        {QueryOperatorIds.AnyWhere,"Any("},
        {QueryOperatorIds.AggregateSum,"Aggregate("},
        {QueryOperatorIds.AverageSelector,"Average("},
        {QueryOperatorIds.CountBy,"CountBy("},
        {QueryOperatorIds.AggregateBy,"AggregateBy("},
        {QueryOperatorIds.SequenceEqual,"SequenceEqual("},
    };

    [Theory,Trait("Category","Unit")]
    [MemberData(nameof(追加した終端演算子))]
    public void 追加した終端演算子はIR往復と経路の生成ができる(string operatorId,string expectedCall){
        var generator=new Core.RandomQueryGenerator();
        var emitter=new Core.QuerySourceEmitter();
        for(var index=0;index<32;index++){
            var document=generator.Generate(new RandomQueryGenerationOptions{
                MasterSeed=Core.Seed256.Parse("0x7E57"),
                QueryIndex=index,
                SourceTarget=SourceTarget.SystemArray,
                OperatorCount=2,
                MaximumDepth=2,
                InputElementCount=16,
                EnableBinaryOperators=false,
                EnableNestedOperators=false,
                AllowedOperatorIds=[QueryOperatorIds.Select,operatorId],
            });
            var json=QueryIrJson.Serialize(document);
            var restored=QueryIrJson.Deserialize(json);
            Assert.Equal(json,QueryIrJson.Serialize(restored));
            Assert.Equal(operatorId,document.Root.OperatorId);
            var emitted=emitter.Emit(document);
            Assert.Contains(expectedCall,emitted.SourceText,StringComparison.Ordinal);
            Assert.Equal(4,emitted.MethodNames.Count);
            Assert.False(string.IsNullOrWhiteSpace(Core.QueryExpressionFormatter.Format(document)));
        }
    }

    [Fact,Trait("Category","Unit")]
    public void Zipは二つの順序付き入力から生成されIR往復と経路の生成ができる(){
        var generator=new Core.RandomQueryGenerator();
        var emitter=new Core.QuerySourceEmitter();
        for(var index=0;index<16;index++){
            var document=generator.Generate(new RandomQueryGenerationOptions{
                MasterSeed=Core.Seed256.Parse("0x21B"),
                QueryIndex=index,
                SourceTarget=SourceTarget.SystemArray,
                OperatorCount=2,
                MaximumDepth=2,
                InputElementCount=16,
                EnableBinaryOperators=true,
                EnableNestedOperators=false,
                AllowedOperatorIds=[QueryOperatorIds.Zip,QueryOperatorIds.Count],
            });
            var zip=Assert.IsType<BinaryQueryNode>(document.Root.Input);
            Assert.Equal(QueryBinaryKind.Zip,zip.Arguments.Kind);
            Assert.Equal(QueryCardinalityRange.Exact(16),zip.Cardinality);
            Assert.Equal(QueryOrderSemantics.Ordered,zip.OrderSemantics);
            var json=QueryIrJson.Serialize(document);
            Assert.Equal(json,QueryIrJson.Serialize(QueryIrJson.Deserialize(json)));
            var emitted=emitter.Emit(document);
            Assert.Contains("Zip(",emitted.SourceText,StringComparison.Ordinal);
            Assert.Equal(4,emitted.MethodNames.Count);
        }
    }

    [Fact,Trait("Category","Unit")]
    public void Zipの件数規則と順序なし入力の拒否(){
        Assert.Equal(new QueryCardinalityRange(2,5),ExtendedBinaryShape.ZipCardinality(new QueryCardinalityRange(2,9),new QueryCardinalityRange(3,5)));
        Assert.Equal(new QueryCardinalityRange(0,4),ExtendedBinaryShape.ZipCardinality(new QueryCardinalityRange(0,4),new QueryCardinalityRange(1,null)));

        var capability=QueryCapabilityProfiles.Get(SourceTarget.SystemPersisted);
        Assert.False(capability.SupportsOnCorePaths(QueryOperatorIds.Zip));
        Assert.False(capability.SupportsOnCorePaths(QueryOperatorIds.SequenceEqual));
        Assert.True(capability.SupportsOnCorePaths(QueryOperatorIds.CountBy));
        Assert.True(QueryCapabilityProfiles.Get(SourceTarget.SystemArray).SupportsOnCorePaths(QueryOperatorIds.Zip));
    }

    [Fact,Trait("Category","Unit")]
    public void 追加した単項演算子の件数と順序の規則(){
        var exact=QueryCardinalityRange.Exact(10);
        Assert.Equal(new QueryCardinalityRange(3,3),ExtendedUnaryShape.Cardinality(new TakeLastOperatorArguments(3),exact));
        Assert.Equal(new QueryCardinalityRange(7,7),ExtendedUnaryShape.Cardinality(new SkipLastOperatorArguments(3),exact));
        Assert.Equal(new QueryCardinalityRange(0,10),ExtendedUnaryShape.Cardinality(new TakeWhileOperatorArguments(new(QueryPredicateKind.IsEven)),exact));
        Assert.Equal(new QueryCardinalityRange(11,11),ExtendedUnaryShape.Cardinality(new AppendOperatorArguments(1),exact));
        Assert.Equal(new QueryCardinalityRange(1,1),ExtendedUnaryShape.Cardinality(new DefaultIfEmptyOperatorArguments(0),QueryCardinalityRange.Exact(0)));
        Assert.Equal(new QueryCardinalityRange(1,4),ExtendedUnaryShape.Cardinality(new GroupByAggregateOperatorArguments(GroupByAggregateKind.Sum,4),exact));

        Assert.Equal(QueryOrderSemantics.Unordered,ExtendedUnaryShape.Order(new OrderByKeyOperatorArguments(3,false),QueryOrderSemantics.Unordered));
        Assert.Equal(QueryOrderSemantics.Ordered,ExtendedUnaryShape.Order(new OrderByKeyOperatorArguments(3,false),QueryOrderSemantics.TotalOrdered));
        Assert.Equal(QueryOrderSemantics.Ordered,ExtendedUnaryShape.Order(new TakeLastOperatorArguments(3),QueryOrderSemantics.Ordered));
    }
}
