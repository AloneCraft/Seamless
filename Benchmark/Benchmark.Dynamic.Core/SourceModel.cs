using System.Text.Json.Serialization;

namespace Benchmark.Dynamic.Core;

public enum SourceFamily{
    System,
    Seamless,
}

public enum SourceKind{
    Array,
    IEnumerable,
    Range,
    List,
    Persisted,
    Tsavorite,
}

public enum SourceTarget{
    SystemArray,
    SystemIEnumerable,
    SystemRange,
    SystemList,
    SystemPersisted,
    SeamlessPersisted,
    SystemTsavorite,
    SeamlessTsavorite,
}

public enum ExecutionPath{
    SystemLinq,
    FLinqNoOptimizations,
    FLinqOptimized,
    ZLinq,
}

public enum OperatorSetMode{
    Common,
    Maximum,
}

public enum QueryValueType{
    Int,
    Long,
    Bool,
    Double,
    IntPair,
    IntGroup,
}

public enum QueryCardinality{
    Scalar,
    Sequence,
}

public enum QueryOrderSemantics{
    Ordered,
    Unordered,
    TotalOrdered,
}

public enum QueryEmptySafety{
    Safe,
    RequiresNonEmpty,
}

public enum SourceSlot{
    Primary,
    Secondary,
}

public enum CountCost{
    Constant,
    LinearScan,
    Unknown,
}

public readonly record struct QueryCardinalityRange{
    [JsonConstructor]
    public QueryCardinalityRange(long minimum,long? maximum){
        ArgumentOutOfRangeException.ThrowIfNegative(minimum);
        if(maximum is { }finiteMaximum&&finiteMaximum<minimum){
            throw new ArgumentOutOfRangeException(nameof(maximum));
        }
        this.Minimum=minimum;
        this.Maximum=maximum;
    }

    public long Minimum{get;}
    public long? Maximum{get;}
    public bool IsDefinitelyNonEmpty=>this.Minimum>0;

    public static QueryCardinalityRange Exact(long count)=>new(count,count);

    public static QueryCardinalityRange Add(QueryCardinalityRange left,QueryCardinalityRange right){
        var minimum=SaturatingAdd(left.Minimum,right.Minimum);
        long? maximum;
        if(left.Maximum is not { }leftMaximum||right.Maximum is not { }rightMaximum){
            maximum=null;
        }else{
            var sum=SaturatingAdd(leftMaximum,rightMaximum);
            maximum=sum==long.MaxValue?null:sum;
        }
        return new(minimum,maximum);
    }

    public static QueryCardinalityRange Multiply(QueryCardinalityRange left,QueryCardinalityRange right){
        var minimum=SaturatingMultiply(left.Minimum,right.Minimum);
        long? maximum;
        if(left.Maximum is not { }leftMaximum||right.Maximum is not { }rightMaximum){
            maximum=null;
        }else{
            var product=SaturatingMultiply(leftMaximum,rightMaximum);
            maximum=product==long.MaxValue?null:product;
        }
        return new(minimum,maximum);
    }

    public static long SaturatingAdd(long left,long right){
        if(left>long.MaxValue-right){
            return long.MaxValue;
        }
        return left+right;
    }

    public static long SaturatingMultiply(long left,long right){
        if(left==0||right==0){
            return 0;
        }
        if(left>long.MaxValue/right){
            return long.MaxValue;
        }
        return left*right;
    }
}

public static class SourceTargetFacts{
    public static SourceFamily GetFamily(this SourceTarget target){
        return target switch{
            SourceTarget.SeamlessPersisted or SourceTarget.SeamlessTsavorite=>SourceFamily.Seamless,
            _=>SourceFamily.System,
        };
    }

    public static SourceKind GetSourceKind(this SourceTarget target){
        return target switch{
            SourceTarget.SystemArray=>SourceKind.Array,
            SourceTarget.SystemIEnumerable=>SourceKind.IEnumerable,
            SourceTarget.SystemRange=>SourceKind.Range,
            SourceTarget.SystemList=>SourceKind.List,
            SourceTarget.SystemPersisted or SourceTarget.SeamlessPersisted=>SourceKind.Persisted,
            SourceTarget.SystemTsavorite or SourceTarget.SeamlessTsavorite=>SourceKind.Tsavorite,
            _=>throw new ArgumentOutOfRangeException(nameof(target)),
        };
    }

    public static QueryOrderSemantics GetInitialOrder(this SourceTarget target){
        return target.GetSourceKind() switch{
            SourceKind.Persisted or SourceKind.Tsavorite=>QueryOrderSemantics.Unordered,
            _=>QueryOrderSemantics.Ordered,
        };
    }

    public static bool RequiresDispose(this SourceTarget target){
        return target.GetSourceKind() is SourceKind.Persisted or SourceKind.Tsavorite;
    }

    public static bool RequiresSameThread(this SourceTarget target){
        return target.GetSourceKind()==SourceKind.Tsavorite;
    }

    public static CountCost GetCountCost(this SourceTarget target){
        return target.GetSourceKind() switch{
            // 源は Tsavorite のシングル用(Create())で、件数をデータ本体が管理するので O(1)。
            // 件数を持たないマルチ用(BeginParallel の区間)は ScanCount で全走査になるが、この源には使わない。
            SourceKind.Array or SourceKind.List or SourceKind.Persisted or SourceKind.Tsavorite=>CountCost.Constant,
            _=>CountCost.Unknown,
        };
    }
}
