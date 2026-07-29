using System.Text.Json.Serialization;

namespace Benchmark.Dynamic.Core;

public readonly record struct IntPair(int First,int Second);
public sealed record IntGroup(int Key,IReadOnlyList<int> Values);

public static class DynamicQueryMaterializer{
    public static int CountWithLimit(IEnumerable<int> values){
        ArgumentNullException.ThrowIfNull(values);
        var count=0;
        foreach(var _ in values){
            if(count>=DynamicQueryValue.MaximumMaterializedItemCount){
                throw new InvalidOperationException(
                    $"A dynamic query cannot enumerate more than {DynamicQueryValue.MaximumMaterializedItemCount:N0} grouped items.");
            }
            count++;
        }
        return count;
    }

    public static IntGroup CreateCanonicalIntGroup(int key,int[] values){
        ArgumentNullException.ThrowIfNull(values);
        if(values.Length>DynamicQueryValue.MaximumMaterializedItemCount){
            throw new InvalidOperationException(
                $"A dynamic query cannot materialize more than {DynamicQueryValue.MaximumMaterializedItemCount:N0} items.");
        }
        Array.Sort(values);
        return new(key,values);
    }

    public static IntPair[] CreateCanonicalDictionaryPairs(IEnumerable<KeyValuePair<int,int>> entries){
        ArgumentNullException.ThrowIfNull(entries);
        var result=new List<IntPair>();
        foreach(var entry in entries){
            if(result.Count>=DynamicQueryValue.MaximumMaterializedItemCount){
                throw new InvalidOperationException(
                    $"A dynamic query cannot materialize more than {DynamicQueryValue.MaximumMaterializedItemCount:N0} dictionary entries.");
            }
            result.Add(new(entry.Key,entry.Value));
        }
        result.Sort(static(left,right)=>left.First.CompareTo(right.First));
        return [..result];
    }

    public static IntGroup[] CreateCanonicalLookupGroups(IEnumerable<IGrouping<int,int>> groups){
        ArgumentNullException.ThrowIfNull(groups);
        var result=new List<IntGroup>();
        var totalValueCount=0;
        foreach(var group in groups){
            if(result.Count>=DynamicQueryValue.MaximumMaterializedItemCount){
                throw new InvalidOperationException(
                    $"A dynamic query cannot materialize more than {DynamicQueryValue.MaximumMaterializedItemCount:N0} lookup groups.");
            }
            var values=new List<int>();
            foreach(var value in group){
                if(totalValueCount>=DynamicQueryValue.MaximumMaterializedItemCount){
                    throw new InvalidOperationException(
                        $"A dynamic query cannot materialize more than {DynamicQueryValue.MaximumMaterializedItemCount:N0} lookup values.");
                }
                totalValueCount++;
                values.Add(value);
            }
            values.Sort();
            result.Add(new(group.Key,values));
        }
        result.Sort(static(left,right)=>left.Key.CompareTo(right.Key));
        return [..result];
    }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName="$value")]
[JsonDerivedType(typeof(IntQueryAtom),"int")]
[JsonDerivedType(typeof(LongQueryAtom),"long")]
[JsonDerivedType(typeof(BoolQueryAtom),"bool")]
[JsonDerivedType(typeof(DoubleQueryAtom),"double")]
[JsonDerivedType(typeof(IntPairQueryAtom),"intPair")]
[JsonDerivedType(typeof(IntGroupQueryAtom),"intGroup")]
public abstract record DynamicQueryAtom(QueryValueType Type);

public sealed record IntQueryAtom(int Value):DynamicQueryAtom(QueryValueType.Int);
public sealed record LongQueryAtom(long Value):DynamicQueryAtom(QueryValueType.Long);
public sealed record BoolQueryAtom(bool Value):DynamicQueryAtom(QueryValueType.Bool);
public sealed record DoubleQueryAtom(double Value):DynamicQueryAtom(QueryValueType.Double);
public sealed record IntPairQueryAtom(IntPair Value):DynamicQueryAtom(QueryValueType.IntPair);
public sealed record IntGroupQueryAtom(IntGroup Value):DynamicQueryAtom(QueryValueType.IntGroup);

public sealed record DynamicQueryValue(
    QueryValueType ValueType,
    QueryCardinality Cardinality,
    QueryOrderSemantics OrderSemantics,
    IReadOnlyList<DynamicQueryAtom> Items){
    public const int MaximumMaterializedItemCount=100_000;
    public static DynamicQueryValue Scalar(DynamicQueryAtom item){
        ArgumentNullException.ThrowIfNull(item);
        return new(item.Type,QueryCardinality.Scalar,QueryOrderSemantics.TotalOrdered,[item]);
    }

    public static DynamicQueryValue Sequence(QueryValueType valueType,QueryOrderSemantics orderSemantics,IEnumerable<DynamicQueryAtom> items){
        ArgumentNullException.ThrowIfNull(items);
        var materialized=new List<DynamicQueryAtom>();
        foreach(var item in items){
            if(materialized.Count>=MaximumMaterializedItemCount){
                throw MaterializeLimitException();
            }
            materialized.Add(item);
        }
        var value=new DynamicQueryValue(valueType,QueryCardinality.Sequence,orderSemantics,materialized);
        value.Validate();
        return value;
    }

    public static DynamicQueryValue FromObject(
        object? value,
        QueryValueType valueType,
        QueryCardinality cardinality,
        QueryOrderSemantics orderSemantics){
        ArgumentNullException.ThrowIfNull(value);
        if(cardinality==QueryCardinality.Scalar){
            return Scalar(CreateAtom(value,valueType));
        }
        if(value is not System.Collections.IEnumerable enumerable){
            throw new ArgumentException("A sequence result must implement IEnumerable.",nameof(value));
        }
        var items=new List<DynamicQueryAtom>();
        foreach(var item in enumerable){
            ArgumentNullException.ThrowIfNull(item);
            if(items.Count>=MaximumMaterializedItemCount){
                throw MaterializeLimitException();
            }
            items.Add(CreateAtom(item,valueType));
        }
        return Sequence(valueType,orderSemantics,items);
    }

    private static InvalidOperationException MaterializeLimitException(){
        return new($"A dynamic query cannot materialize more than {MaximumMaterializedItemCount:N0} items.");
    }

    public void Validate(){
        ArgumentNullException.ThrowIfNull(this.Items);
        if(this.Cardinality==QueryCardinality.Scalar&&this.Items.Count!=1){
            throw new InvalidOperationException("A scalar DynamicQueryValue must contain exactly one item.");
        }
        foreach(var item in this.Items){
            if(item.Type!=this.ValueType){
                throw new InvalidOperationException($"Value type {item.Type} does not match declared type {this.ValueType}.");
            }
        }
    }

    private static DynamicQueryAtom CreateAtom(object value,QueryValueType valueType){
        return valueType switch{
            QueryValueType.Int when value is int intValue=>new IntQueryAtom(intValue),
            QueryValueType.Long when value is long longValue=>new LongQueryAtom(longValue),
            QueryValueType.Bool when value is bool boolValue=>new BoolQueryAtom(boolValue),
            QueryValueType.Double when value is double doubleValue=>new DoubleQueryAtom(doubleValue),
            QueryValueType.IntPair when value is IntPair pairValue=>new IntPairQueryAtom(pairValue),
            QueryValueType.IntGroup when value is IntGroup groupValue=>new IntGroupQueryAtom(groupValue),
            _=>throw new ArgumentException($"Value '{value.GetType()}' is not a closed {valueType} value.",nameof(value)),
        };
    }
}

public enum QueryComparisonMode{
    Auto,
    Scalar,
    OrderedSequence,
    UnorderedSet,
    UnorderedMultiset,
}

public sealed record DynamicQueryComparison(bool IsMatch,string? Difference=null,int? DifferenceIndex=null){
    public static readonly DynamicQueryComparison Match=new(true);
}

public static class DynamicQueryComparer{
    public static DynamicQueryComparison Compare(
        DynamicQueryValue expected,
        DynamicQueryValue actual,
        QueryComparisonMode mode=QueryComparisonMode.Auto){
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        expected.Validate();
        actual.Validate();
        if(expected.ValueType!=actual.ValueType){
            return new(false,$"Value type mismatch: expected {expected.ValueType}, actual {actual.ValueType}.");
        }
        if(expected.Cardinality!=actual.Cardinality){
            return new(false,$"Cardinality mismatch: expected {expected.Cardinality}, actual {actual.Cardinality}.");
        }
        var resolvedMode=ResolveMode(expected,mode);
        return resolvedMode switch{
            QueryComparisonMode.Scalar=>CompareOrdered(expected.Items,actual.Items,"scalar"),
            QueryComparisonMode.OrderedSequence=>CompareOrdered(expected.Items,actual.Items,"ordered sequence"),
            QueryComparisonMode.UnorderedSet=>CompareSet(expected.Items,actual.Items),
            QueryComparisonMode.UnorderedMultiset=>CompareMultiset(expected.Items,actual.Items),
            _=>throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }

    private static QueryComparisonMode ResolveMode(DynamicQueryValue expected,QueryComparisonMode mode){
        if(mode!=QueryComparisonMode.Auto){
            return mode;
        }
        if(expected.Cardinality==QueryCardinality.Scalar){
            return QueryComparisonMode.Scalar;
        }
        if(expected.OrderSemantics==QueryOrderSemantics.Unordered){
            return QueryComparisonMode.UnorderedMultiset;
        }
        return QueryComparisonMode.OrderedSequence;
    }

    private static DynamicQueryComparison CompareOrdered(
        IReadOnlyList<DynamicQueryAtom> expected,
        IReadOnlyList<DynamicQueryAtom> actual,
        string description){
        if(expected.Count!=actual.Count){
            return new(false,$"{description} length mismatch: expected {expected.Count}, actual {actual.Count}.");
        }
        for(var index=0;index<expected.Count;index++){
            if(!DynamicQueryAtomEqualityComparer.Instance.Equals(expected[index],actual[index])){
                return new(false,$"{description} mismatch at index {index}: expected {Format(expected[index])}, actual {Format(actual[index])}.",index);
            }
        }
        return DynamicQueryComparison.Match;
    }

    private static DynamicQueryComparison CompareSet(
        IReadOnlyList<DynamicQueryAtom> expected,
        IReadOnlyList<DynamicQueryAtom> actual){
        var expectedSet=new HashSet<DynamicQueryAtom>(expected,DynamicQueryAtomEqualityComparer.Instance);
        var actualSet=new HashSet<DynamicQueryAtom>(actual,DynamicQueryAtomEqualityComparer.Instance);
        if(expectedSet.SetEquals(actualSet)){
            return DynamicQueryComparison.Match;
        }
        return new(false,$"Unordered set mismatch: expected {expectedSet.Count} distinct values, actual {actualSet.Count}.");
    }

    private static DynamicQueryComparison CompareMultiset(
        IReadOnlyList<DynamicQueryAtom> expected,
        IReadOnlyList<DynamicQueryAtom> actual){
        if(expected.Count!=actual.Count){
            return new(false,$"Unordered multiset length mismatch: expected {expected.Count}, actual {actual.Count}.");
        }
        var counts=new Dictionary<DynamicQueryAtom,int>(DynamicQueryAtomEqualityComparer.Instance);
        foreach(var item in expected){
            counts.TryGetValue(item,out var count);
            counts[item]=count+1;
        }
        foreach(var item in actual){
            if(!counts.TryGetValue(item,out var count)){
                return new(false,$"Unexpected multiset value: {Format(item)}.");
            }
            if(count==1){
                counts.Remove(item);
            }else{
                counts[item]=count-1;
            }
        }
        if(counts.Count==0){
            return DynamicQueryComparison.Match;
        }
        var missing=counts.First();
        return new(false,$"Missing multiset value: {Format(missing.Key)} x {missing.Value}.");
    }

    private static string Format(DynamicQueryAtom atom){
        return atom switch{
            IntQueryAtom value=>value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            LongQueryAtom value=>value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            BoolQueryAtom value=>value.Value.ToString(),
            DoubleQueryAtom value=>value.Value.ToString("R",System.Globalization.CultureInfo.InvariantCulture),
            IntPairQueryAtom value=>$"({value.Value.First},{value.Value.Second})",
            IntGroupQueryAtom value=>$"{value.Value.Key}:[{string.Join(",",value.Value.Values)}]",
            _=>atom.ToString()??atom.GetType().Name,
        };
    }

    private sealed class DynamicQueryAtomEqualityComparer:IEqualityComparer<DynamicQueryAtom>{
        public static readonly DynamicQueryAtomEqualityComparer Instance=new();

        public bool Equals(DynamicQueryAtom? left,DynamicQueryAtom? right){
            if(ReferenceEquals(left,right)){
                return true;
            }
            if(left is null||right is null||left.Type!=right.Type){
                return false;
            }
            return (left,right) switch{
                (IntQueryAtom leftValue,IntQueryAtom rightValue)=>leftValue.Value==rightValue.Value,
                (LongQueryAtom leftValue,LongQueryAtom rightValue)=>leftValue.Value==rightValue.Value,
                (BoolQueryAtom leftValue,BoolQueryAtom rightValue)=>leftValue.Value==rightValue.Value,
                (DoubleQueryAtom leftValue,DoubleQueryAtom rightValue)=>leftValue.Value.Equals(rightValue.Value),
                (IntPairQueryAtom leftValue,IntPairQueryAtom rightValue)=>leftValue.Value==rightValue.Value,
                (IntGroupQueryAtom leftValue,IntGroupQueryAtom rightValue)=>
                    leftValue.Value.Key==rightValue.Value.Key&&leftValue.Value.Values.SequenceEqual(rightValue.Value.Values),
                _=>false,
            };
        }

        public int GetHashCode(DynamicQueryAtom value){
            var hash=new HashCode();
            hash.Add(value.Type);
            switch(value){
                case IntQueryAtom item:
                    hash.Add(item.Value);
                    break;
                case LongQueryAtom item:
                    hash.Add(item.Value);
                    break;
                case BoolQueryAtom item:
                    hash.Add(item.Value);
                    break;
                case DoubleQueryAtom item:
                    hash.Add(item.Value);
                    break;
                case IntPairQueryAtom item:
                    hash.Add(item.Value);
                    break;
                case IntGroupQueryAtom item:
                    hash.Add(item.Value.Key);
                    foreach(var element in item.Value.Values){
                        hash.Add(element);
                    }
                    break;
            }
            return hash.ToHashCode();
        }
    }
}
