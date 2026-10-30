using Benchmark.Dynamic.Core;
using Core=Benchmark.Dynamic.Core;

namespace Benchmark.Dynamic.Tests;

public sealed class TypedQueryTests{
    public static TheoryData<QueryElementType> NonIntTypes=>new(
        Enum.GetValues<QueryElementType>().Where(static type=>type!=QueryElementType.Int));

    private static QueryIrDocument Generate(QueryElementType type,int index,SourceTarget target=SourceTarget.SystemArray){
        var capability=QueryCapabilityProfiles.CreateSet(OperatorSetMode.Maximum,[target],type);
        return new Core.RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
            MasterSeed=Core.Seed256.Parse("0x7E7E"),
            QueryIndex=index,
            SourceTarget=target,
            OperatorCount=4,
            MaximumDepth=4,
            InputElementCount=16,
            EnableBinaryOperators=true,
            EnableNestedOperators=true,
            AllowedOperatorIds=capability.OperatorIds,
            ElementType=type,
        });
    }

    [Theory,Trait("Category","Unit")]
    [MemberData(nameof(NonIntTypes))]
    public void 要素型つきのクエリはIR往復と生成ができ要素型の名前が生成ソースに出る(QueryElementType type){
        var descriptor=ElementTypeCatalog.Get(type);
        var emitter=new Core.QuerySourceEmitter();
        var withElementType=0;
        for(var index=0;index<24;index++){
            var document=Generate(type,index);
            Assert.Equal(type,document.ElementType);
            var json=QueryIrJson.Serialize(document);
            Assert.Contains("\"elementType\":",json,StringComparison.Ordinal);
            var restored=QueryIrJson.Deserialize(json);
            Assert.Equal(type,restored.ElementType);
            Assert.Equal(json,QueryIrJson.Serialize(restored));

            var emitted=emitter.Emit(document);
            if(emitted.SourceText.Contains(descriptor.ClrTypeName+"[]",StringComparison.Ordinal)){
                withElementType++;
            }
            // int 専用の式(排他的論理和・int の剰余を要素にかける式など)が紛れ込まない。
            Assert.DoesNotContain("value^index",emitted.SourceText,StringComparison.Ordinal);
            Assert.DoesNotContain("IntPair",emitted.SourceText,StringComparison.Ordinal);
        }
        Assert.Equal(24,withElementType);
    }

    [Theory,Trait("Category","Unit")]
    [MemberData(nameof(NonIntTypes))]
    public void 要素型ごとに使えない演算子は集合から外れる(QueryElementType type){
        var descriptor=ElementTypeCatalog.Get(type);
        var set=QueryCapabilityProfiles.CreateSet(OperatorSetMode.Maximum,[SourceTarget.SystemArray],type);
        Assert.Contains(QueryOperatorIds.Where,set.OperatorIds);
        Assert.Contains(QueryOperatorIds.Select,set.OperatorIds);
        Assert.Contains(QueryOperatorIds.ToArray,set.OperatorIds);
        Assert.DoesNotContain(QueryOperatorIds.GroupBySum,set.OperatorIds);
        Assert.DoesNotContain(QueryOperatorIds.SelectIndexed,set.OperatorIds);
        Assert.DoesNotContain(QueryOperatorIds.ToDictionary,set.OperatorIds);
        Assert.Equal(descriptor.IsComparable,set.OperatorIds.Contains(QueryOperatorIds.Min));
        Assert.Equal(descriptor.IsComparable,set.OperatorIds.Contains(QueryOperatorIds.OrderBy));
        Assert.NotEqual(
            QueryCapabilityProfiles.CreateSet(OperatorSetMode.Maximum,[SourceTarget.SystemArray]).CapabilityId,
            set.CapabilityId);
    }

    [Fact,Trait("Category","Unit")]
    public void 要素型がintのときの集合と識別子は従来と同じ(){
        var plain=QueryCapabilityProfiles.CreateSet(OperatorSetMode.Maximum,[SourceTarget.SystemArray]);
        var typed=QueryCapabilityProfiles.CreateSet(OperatorSetMode.Maximum,[SourceTarget.SystemArray],QueryElementType.Int);
        Assert.Equal(plain.CapabilityId,typed.CapabilityId);
        Assert.Equal(plain.OperatorIds,typed.OperatorIds);
    }

    [Fact,Trait("Category","Unit")]
    public void int以外の要素型はRangeと永続系のSourceでは使えない(){
        foreach(var target in new[]{SourceTarget.SystemRange,SourceTarget.SystemPersisted,SourceTarget.SeamlessTsavorite}){
            Assert.False(QueryCapabilityProfiles.SupportsElementType(target,QueryElementType.Long));
            Assert.Throws<ArgumentException>(()=>Generate(QueryElementType.Long,0,target));
        }
        foreach(var target in new[]{SourceTarget.SystemArray,SourceTarget.SystemList,SourceTarget.SystemIEnumerable}){
            Assert.True(QueryCapabilityProfiles.SupportsElementType(target,QueryElementType.String));
        }
    }

    [Fact,Trait("Category","Unit")]
    public void 比較できない要素型ではキー指定なしの順序づけと最小最大を生成しない(){
        foreach(var type in new[]{QueryElementType.Order}){
            var set=QueryCapabilityProfiles.CreateSet(OperatorSetMode.Maximum,[SourceTarget.SystemArray],type);
            foreach(var id in new[]{QueryOperatorIds.OrderBy,QueryOperatorIds.ThenBy,QueryOperatorIds.Min,QueryOperatorIds.Max,QueryOperatorIds.MinBy,QueryOperatorIds.MaxBy}){
                Assert.DoesNotContain(id,set.OperatorIds);
            }
            Assert.Contains(QueryOperatorIds.OrderByKey,set.OperatorIds);
            for(var index=0;index<40;index++){
                var document=Generate(type,index);
                foreach(var node in Enumerate(document.Root).OfType<UnaryQueryNode>()){
                    if(node.Arguments is OrderByKeyOperatorArguments orderBy){
                        Assert.True(orderBy.Modulus>=2,"A non-comparable element needs a projected key.");
                    }
                }
            }
        }
    }

    [Theory,Trait("Category","Unit")]
    [MemberData(nameof(NonIntTypes))]
    public void 入力要素の元の値は単射で正規形も重複しない(QueryElementType type){
        var descriptor=ElementTypeCatalog.Get(type);
        var numbers=Enumerable.Range(-8,64).ToArray();
        var values=descriptor.CreateValues(numbers,Core.Seed256.Parse("0x5E"),0,includeSpecialValues:false);
        Assert.All(values,value=>Assert.IsType(descriptor.ClrType==typeof(int?)?typeof(int):descriptor.ClrType,value));
        var canonical=values.Select(Core.ElementCanonicalizer.Canonical).ToArray();
        Assert.Equal(canonical.Length,canonical.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(canonical,descriptor.CreateValues(numbers,Core.Seed256.Parse("0x5E"),0,includeSpecialValues:false).Select(Core.ElementCanonicalizer.Canonical));
    }

    [Theory,Trait("Category","Unit")]
    [MemberData(nameof(NonIntTypes))]
    public void 特殊な値は決定的に混ざり正規形にできる(QueryElementType type){
        var descriptor=ElementTypeCatalog.Get(type);
        var numbers=Enumerable.Range(0,400).ToArray();
        var seed=Core.Seed256.Parse("0x5F");
        var first=descriptor.CreateValues(numbers,seed,1,includeSpecialValues:true).Select(Core.ElementCanonicalizer.Canonical).ToArray();
        var second=descriptor.CreateValues(numbers,seed,1,includeSpecialValues:true).Select(Core.ElementCanonicalizer.Canonical).ToArray();
        Assert.Equal(first,second);
        var plain=descriptor.CreateValues(numbers,seed,1,includeSpecialValues:false).Select(Core.ElementCanonicalizer.Canonical).ToArray();
        if(descriptor.SpecialValues.Count>0){
            Assert.NotEqual(plain,first);
        }else{
            Assert.Equal(plain,first);
        }
    }

    [Fact,Trait("Category","Unit")]
    public void 正規形は浮動小数点のNaNを揃えゼロの符号とdecimalの桁を区別する(){
        Assert.Equal(Core.ElementCanonicalizer.Canonical(float.NaN),Core.ElementCanonicalizer.Canonical(BitConverter.UInt32BitsToSingle(0xFFC00001)));
        Assert.Equal(Core.ElementCanonicalizer.Canonical(double.NaN),Core.ElementCanonicalizer.Canonical(BitConverter.UInt64BitsToDouble(0xFFF8000000000001UL)));
        Assert.NotEqual(Core.ElementCanonicalizer.Canonical(0.0),Core.ElementCanonicalizer.Canonical(-0.0));
        Assert.NotEqual(Core.ElementCanonicalizer.Canonical(1.1m),Core.ElementCanonicalizer.Canonical(1.10m));
        Assert.NotEqual(Core.ElementCanonicalizer.Canonical(null),Core.ElementCanonicalizer.Canonical(""));
        Assert.Equal("null",Core.ElementCanonicalizer.Canonical((int?)null));
        Assert.NotEqual(Core.ElementCanonicalizer.Canonical("ab"),Core.ElementCanonicalizer.Canonical("a"+"b "));
    }

    private static IEnumerable<QueryNode> Enumerate(QueryNode node){
        yield return node;
        foreach(var child in node.Children){
            foreach(var descendant in Enumerate(child)){
                yield return descendant;
            }
        }
    }
}
