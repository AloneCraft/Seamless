using System.Numerics;
using Benchmark.Dynamic.ElementTypes;

namespace Benchmark.Dynamic.Core;

/// <summary>クエリの要素型。<c>Int</c> を既定値(0)とし、IR JSON では既定値を書き出さない。</summary>
public enum QueryElementType{
    Int,
    Long,
    Float,
    Double,
    Decimal,
    String,
    Int256Struct,
    Int256Class,
    QuadrupleStruct,
    QuadrupleClass,
    NullableInt,
    Order,
    CollidingKey,
}

/// <summary>
/// 要素型ごとの違い(C# 型名、述語・選択子のラムダ、リテラル、キー、合成、値の作り方)を1か所に集める。
/// 型を足す・外すときは、ここへ記述子を1つ足すだけで済むようにする。
/// <c>Int</c> の記述子は、型を一覧化する前に生成していたソースと同じ文字列を作る(基準テストで固定している)。
/// </summary>
/// <remarks>
/// IR のノードの型表記(<see cref="QueryValueType.Int"/>)は、int 以外の要素型でも「要素の列」を表す。
/// 要素型は <see cref="QueryIrDocument.ElementType"/> で決まる。
/// </remarks>
public sealed record ElementTypeDescriptor(
    QueryElementType Id,
    Type ClrType,
    string ClrTypeName,
    bool IsComparable,
    Func<QueryPredicate,string> EmitPredicate,
    Func<QuerySelector,string> EmitSelector,
    Func<int,string> Literal,
    // 要素から決まる int の式(括弧つき)。DistinctBy・OrderBy のキー、Sum の被加数などに使う。
    Func<string,string> Hash,
    // 要素2つを1つの要素へ合わせる式(Zip・Join の結果)。
    Func<string,string,string> Combine,
    // int から要素を作る(単射)。入力の値の元になる。
    Func<int,object?> FromInt,
    // 特殊な値(NaN・null・境界値など)。入力に少しだけ混ぜる。
    IReadOnlyList<object?> SpecialValues){
    // 全演算子のうち、この要素型で使えないもの。要素の列を別の型へ変えるもの、int 専用の式を持つもの、比較できない型の順序づけ。
    private static readonly HashSet<string> IntOnlyOperators=new(StringComparer.Ordinal){
        QueryOperatorIds.SelectIndexed,
        QueryOperatorIds.GroupBySum,
        QueryOperatorIds.GroupByCount,
        QueryOperatorIds.GroupByKeyOnly,
        QueryOperatorIds.GroupJoinXorCount,
        QueryOperatorIds.SelectManyXor,
        QueryOperatorIds.ToIntPair,
        QueryOperatorIds.ToIntGroup,
        QueryOperatorIds.ToDictionary,
        QueryOperatorIds.ToLookup,
        QueryOperatorIds.AggregateXor,
        QueryOperatorIds.Average,
    };

    private static readonly HashSet<string> ComparisonOperators=new(StringComparer.Ordinal){
        QueryOperatorIds.Min,
        QueryOperatorIds.Max,
        QueryOperatorIds.MinBy,
        QueryOperatorIds.MaxBy,
        QueryOperatorIds.OrderBy,
        QueryOperatorIds.ThenBy,
    };

    public bool IsInt=>this.Id==QueryElementType.Int;

    /// <summary>この要素型の列に対して、その演算子を生成してよいか。</summary>
    public bool Supports(string operatorId){
        if(this.IsInt){
            return true;
        }
        return !IntOnlyOperators.Contains(operatorId)
            &&(this.IsComparable||!ComparisonOperators.Contains(operatorId));
    }

    /// <summary>[0, modulus) の int キーを作る式。<c>Int</c> では従来の <c>(value%m+m)%m</c> と同じ。</summary>
    public string KeyExpression(string variable,int modulus){
        return $"({this.Hash(variable)}%{modulus}+{modulus})%{modulus}";
    }

    /// <summary>要素を long にする式(Sum・Average の選択子)。<c>Int</c> では従来の <c>(long)value</c>。</summary>
    public string LongSelector(string variable)=>$"(long){this.Hash(variable)}";

    /// <summary>long の累積に足す項(Aggregate)。<c>Int</c> では従来どおり要素そのもの。</summary>
    public string LongAddend(string variable)=>this.IsInt?variable:$"(long){this.Hash(variable)}";

    /// <summary>int の累積に足す項(AggregateBy)。<c>Int</c> では従来どおり要素そのもの。</summary>
    public string IntAddend(string variable)=>this.IsInt?variable:this.Hash(variable);

    /// <summary>Join の結果の式。<c>Int</c> では従来の排他的論理和、他は <see cref="Combine"/>。</summary>
    public string JoinResult(string outer,string inner)=>this.IsInt?$"{outer}^{inner}":this.Combine(outer,inner);

    /// <summary>入力要素を作る。<paramref name="includeSpecialValues"/> が true のとき、約1/16の位置を特殊な値に置き換える。</summary>
    public object?[] CreateValues(IReadOnlyList<int> numbers,Seed256 seed,int slot,bool includeSpecialValues){
        var values=new object?[numbers.Count];
        for(var index=0;index<values.Length;index++){
            values[index]=this.FromInt(numbers[index]);
        }
        if(includeSpecialValues&&this.SpecialValues.Count>0){
            var random=new DeterministicPrng(SeedDerivation.Derive(seed,"special",slot));
            for(var index=0;index<values.Length;index++){
                if(random.NextInt32(0,16)==0){
                    values[index]=this.SpecialValues[random.NextInt32(0,this.SpecialValues.Count)];
                }
            }
        }
        return values;
    }
}

public static class ElementTypeCatalog{
    private const string Types="global::Benchmark.Dynamic.ElementTypes.";

    private static readonly IReadOnlyDictionary<QueryElementType,ElementTypeDescriptor> Descriptors=Create();

    public static ElementTypeDescriptor Int=>Descriptors[QueryElementType.Int];

    public static IReadOnlyCollection<ElementTypeDescriptor> All=>Descriptors.Values.ToArray();

    public static ElementTypeDescriptor Get(QueryElementType type){
        return Descriptors.TryGetValue(type,out var descriptor)
            ?descriptor
            :throw new ArgumentOutOfRangeException(nameof(type));
    }

    private static Func<QueryPredicate,string> Predicate(
        string even,
        string odd,
        Func<int,string> greaterThan,
        Func<int,string> lessThan,
        Func<int,int,string> moduloEquals){
        return predicate=>predicate.Kind switch{
            QueryPredicateKind.IsEven=>"value=>"+even,
            QueryPredicateKind.IsOdd=>"value=>"+odd,
            QueryPredicateKind.GreaterThan=>"value=>"+greaterThan(predicate.Operand),
            QueryPredicateKind.LessThan=>"value=>"+lessThan(predicate.Operand),
            QueryPredicateKind.ModuloEquals=>"value=>"+moduloEquals(predicate.Modulus,predicate.Operand),
            _=>throw new ArgumentOutOfRangeException(nameof(predicate)),
        };
    }

    private static Func<QuerySelector,string> Selector(Func<int,string> add,Func<int,string> multiply,string negate,string identity="value=>value"){
        return selector=>selector.Kind switch{
            QuerySelectorKind.Identity=>identity,
            QuerySelectorKind.Add=>"value=>"+add(selector.Operand),
            QuerySelectorKind.Multiply=>"value=>"+multiply(selector.Operand),
            QuerySelectorKind.Negate=>"value=>"+negate,
            _=>throw new ArgumentOutOfRangeException(nameof(selector)),
        };
    }

    // 数値(long・float・double)の型。接尾辞つきのリテラルで、述語と選択子を作る。
    private static ElementTypeDescriptor Numeric(
        QueryElementType id,
        Type clrType,
        string clrTypeName,
        string suffix,
        string hash,
        Func<int,object?> fromInt,
        IReadOnlyList<object?> specials,
        string even,
        string odd,
        Func<int,int,string> moduloEquals){
        return new(
            id,
            clrType,
            clrTypeName,
            true,
            Predicate(
                even,
                odd,
                operand=>$"value>{operand}{suffix}",
                operand=>$"value<{operand}{suffix}",
                moduloEquals),
            Selector(
                operand=>$"value+({operand}{suffix})",
                operand=>$"value*({operand}{suffix})",
                "-value"),
            literal=>$"{literal}{suffix}",
            variable=>hash.Replace("value",variable,StringComparison.Ordinal),
            (left,right)=>$"{left}+{right}",
            fromInt,
            specials);
    }

    private static ElementTypeDescriptor CustomNumber(QueryElementType id,Type clrType,string name,Func<int,object?> fromInt,IReadOnlyList<object?> specials,string evenBase,string hash){
        var cast=$"({Types}{name})";
        return new(
            id,
            clrType,
            $"global::{clrType.FullName}",
            true,
            Predicate(
                $"({evenBase})",
                $"!({evenBase})",
                operand=>$"value>{cast}({operand})",
                operand=>$"value<{cast}({operand})",
                (modulus,operand)=>$"(({hash.Replace("value","value",StringComparison.Ordinal)}%{modulus})+{modulus})%{modulus}=={operand}"),
            Selector(
                operand=>$"value+{cast}({operand})",
                operand=>$"value*{cast}({operand})",
                "-value"),
            literal=>$"{cast}({literal})",
            variable=>hash.Replace("value",variable,StringComparison.Ordinal),
            (left,right)=>$"{left}+{right}",
            fromInt,
            specials);
    }

    private static IReadOnlyDictionary<QueryElementType,ElementTypeDescriptor> Create(){
        var descriptors=new List<ElementTypeDescriptor>();

        descriptors.Add(new(
            QueryElementType.Int,
            typeof(int),
            "global::System.Int32",
            true,
            Predicate(
                "(value&1)==0",
                "(value&1)!=0",
                operand=>$"value>{operand}",
                operand=>$"value<{operand}",
                (modulus,operand)=>$"value%{modulus}=={operand}"),
            Selector(
                operand=>$"value+({operand})",
                operand=>$"value*({operand})",
                "-value"),
            literal=>literal.ToString(),
            static variable=>variable,
            static(left,right)=>$"{left}+{right}",
            static number=>number,
            []));

        descriptors.Add(Numeric(
            QueryElementType.Long,typeof(long),"global::System.Int64","L","unchecked((int)value)",
            static number=>unchecked((long)number*4_294_967_311L),
            [0L,long.MinValue,long.MaxValue,-1L],
            "(value&1L)==0","(value&1L)!=0",
            (modulus,operand)=>$"value%{modulus}L=={operand}L"));

        descriptors.Add(Numeric(
            QueryElementType.Float,typeof(float),"global::System.Single","f","value.GetHashCode()",
            static number=>number*0.5f,
            [float.NaN,-0f,0f,float.PositiveInfinity,float.NegativeInfinity,float.Epsilon,float.MaxValue],
            "((long)value&1L)==0","((long)value&1L)!=0",
            (modulus,operand)=>$"((long)value)%{modulus}L=={operand}L"));

        descriptors.Add(Numeric(
            QueryElementType.Double,typeof(double),"global::System.Double","d","value.GetHashCode()",
            static number=>number*0.1,
            [double.NaN,-0d,0d,double.PositiveInfinity,double.NegativeInfinity,double.Epsilon,1e300],
            "((long)value&1L)==0","((long)value&1L)!=0",
            (modulus,operand)=>$"((long)value)%{modulus}L=={operand}L"));

        // decimal は long への変換が桁あふれで例外になるので、剰余(decimal のまま)で偶奇を決める。
        descriptors.Add(Numeric(
            QueryElementType.Decimal,typeof(decimal),"global::System.Decimal","m","value.GetHashCode()",
            static number=>number/100m,
            [0m,1.10m,1.1m,-0.0m,decimal.MinusOne],
            "value%2m==0m","value%2m!=0m",
            (modulus,operand)=>$"value%{modulus}m=={operand}m"));

        descriptors.Add(new(
            QueryElementType.String,
            typeof(string),
            "global::System.String",
            true,
            Predicate(
                "(value?.Length??0)%2==0",
                "(value?.Length??0)%2!=0",
                operand=>$"string.CompareOrdinal(value,\"{operand}\")>0",
                operand=>$"string.CompareOrdinal(value,\"{operand}\")<0",
                (modulus,operand)=>$"(value?.Length??0)%{modulus}=={operand}"),
            Selector(
                operand=>$"value+\"{operand}\"",
                _=>"value+value",
                "value?.ToUpperInvariant()"),
            literal=>$"\"{literal}\"",
            static variable=>$"({variable}?.Length??0)",
            static(left,right)=>$"{left}+{right}",
            static number=>$"v{number}",
            [null,"","ß","İ","a\0b","v1","v1"]));

        descriptors.Add(CustomNumber(
            QueryElementType.Int256Struct,typeof(Int256),"Int256",
            static number=>Int256.FromBigInteger((BigInteger)number*((BigInteger.One<<128)|BigInteger.One)),
            [Int256.Zero,Int256.MinValue,Int256.MaxValue,-Int256.One],
            "value.IsEven","value.StableKey"));

        descriptors.Add(CustomNumber(
            QueryElementType.Int256Class,typeof(Int256Object),"Int256Object",
            static number=>new Int256Object(Int256.FromBigInteger((BigInteger)number*((BigInteger.One<<128)|BigInteger.One))),
            [new Int256Object(Int256.Zero),new Int256Object(Int256.MinValue),new Int256Object(Int256.MaxValue)],
            "value.IsEven","value.StableKey"));

        descriptors.Add(CustomNumber(
            QueryElementType.QuadrupleStruct,typeof(Quadruple),"Quadruple",
            static number=>Quadruple.FromInt64(number)/(Quadruple)10,
            [Quadruple.NaN,Quadruple.NegativeZero,Quadruple.Zero,Quadruple.PositiveInfinity,Quadruple.NegativeInfinity],
            "(value.StableKey&1)==0","value.StableKey"));

        descriptors.Add(CustomNumber(
            QueryElementType.QuadrupleClass,typeof(QuadrupleObject),"QuadrupleObject",
            static number=>new QuadrupleObject(Quadruple.FromInt64(number)/(Quadruple)10),
            [new QuadrupleObject(Quadruple.NaN),new QuadrupleObject(Quadruple.NegativeZero),new QuadrupleObject(Quadruple.PositiveInfinity)],
            "(value.StableKey&1)==0","value.StableKey"));

        descriptors.Add(new(
            QueryElementType.NullableInt,
            typeof(int?),
            "global::System.Nullable<global::System.Int32>",
            true,
            Predicate(
                "value.HasValue&&(value.Value&1)==0",
                "value.HasValue&&(value.Value&1)!=0",
                operand=>$"value>{operand}",
                operand=>$"value<{operand}",
                (modulus,operand)=>$"value%{modulus}=={operand}"),
            Selector(
                operand=>$"value+({operand})",
                operand=>$"value*({operand})",
                "-value"),
            literal=>$"(int?)({literal})",
            static variable=>$"({variable}??0)",
            static(left,right)=>$"{left}+{right}",
            static number=>(int?)number,
            [null,0,-1]));

        descriptors.Add(new(
            QueryElementType.Order,
            typeof(Order),
            $"{Types}Order",
            false,
            Predicate(
                "(value.Id&1)==0",
                "(value.Id&1)!=0",
                operand=>$"value.Price>{operand}m",
                operand=>$"value.Price<{operand}m",
                (modulus,operand)=>$"value.CustomerId%{modulus}=={operand}"),
            Selector(
                operand=>$"value with{{Price=value.Price+({operand}m)}}",
                operand=>$"value with{{CustomerId=value.CustomerId*({operand})}}",
                "value with{Id=-value.Id}"),
            literal=>$"new {Types}Order({literal},(({literal})%16+16)%16,{literal}m)",
            static variable=>$"{variable}.Id",
            static(left,_)=>left,
            static number=>new Order(number,((number%16)+16)%16,number/100m),
            []));

        descriptors.Add(new(
            QueryElementType.CollidingKey,
            typeof(CollidingKey),
            $"{Types}CollidingKey",
            true,
            Predicate(
                "(value.Value&1)==0",
                "(value.Value&1)!=0",
                operand=>$"value.Value>{operand}",
                operand=>$"value.Value<{operand}",
                (modulus,operand)=>$"value.Value%{modulus}=={operand}"),
            Selector(
                operand=>$"new {Types}CollidingKey(value.Value+({operand}))",
                operand=>$"new {Types}CollidingKey(value.Value*({operand}))",
                $"new {Types}CollidingKey(-value.Value)"),
            literal=>$"new {Types}CollidingKey({literal})",
            static variable=>$"{variable}.Value",
            static(left,right)=>$"new {Types}CollidingKey({left}.Value+{right}.Value)",
            static number=>new CollidingKey(number),
            []));

        return descriptors.ToDictionary(static descriptor=>descriptor.Id);
    }
}
