using System.Text.Json;

namespace Seamless.Execution.Growth;

public enum Resource{Index,LogMemory,Buffer}
public enum Status{Keep,Grow,Deferred,Rejected}

public readonly record struct Context(
    Resource Resource,long CurrentBytes,long RequiredBytes,long LimitBytes,
    long ExpectedCount=0,double BytesPerItem=0,long RecommendedBytes=0,
    bool IsSafePoint=true,long AllocationUnit=1,bool PowerOfTwo=true);

public readonly record struct Decision(Status Status,long TargetBytes,string Reason);
public sealed record Descriptor(string ID,int Version,string ParametersJSON);
public sealed record Event(string Component,DateTimeOffset RecordedAt,Context Context,Decision Decision);

/// <summary>判断の状態はTSelfに保持する。エンジンの操作と資源予約は変更案の検証後に行う。</summary>
public interface IStrategy<TSelf> where TSelf:struct,IStrategy<TSelf>{
    static abstract string ID{get;}
    static abstract int Version{get;}
    static abstract Decision Decide(ref TSelf self,in Context context);
    static virtual string Describe(in TSelf self)=>"{}";
}

/// <summary>実行時の選択を、型が確定した独立のコントローラーへ接続する入口。</summary>
public abstract class Policy{
    public abstract Descriptor Descriptor{get;}
    public abstract Controller CreateController();
}

public sealed class Policy<TStrategy>:Policy where TStrategy:struct,IStrategy<TStrategy>{
    private readonly TStrategy initial;
    public Policy(TStrategy initial=default){
        this.initial=initial;
        ArgumentException.ThrowIfNullOrWhiteSpace(TStrategy.ID);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(TStrategy.Version);
        using var parameters=JsonDocument.Parse(TStrategy.Describe(in initial));
        if(parameters.RootElement.ValueKind!=JsonValueKind.Object){throw new ArgumentException("Strategy parameters must be a JSON object.");}
        this.Descriptor=new(TStrategy.ID,TStrategy.Version,parameters.RootElement.GetRawText());
    }
    public override Descriptor Descriptor{get;}
    public override Controller CreateController()=>new TypedController(this.initial,this.Descriptor);

    private sealed class TypedController(TStrategy initial,Descriptor descriptor):Controller(descriptor){
        private TStrategy strategy=initial;
        protected override Decision DecideCore(in Context context)=>TStrategy.Decide(ref this.strategy,in context);
    }
}

public abstract class Controller(Descriptor descriptor){
    public Descriptor Descriptor{get;}=descriptor;
    public Decision Decide(in Context context){
        ArgumentOutOfRangeException.ThrowIfNegative(context.CurrentBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(context.RequiredBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(context.LimitBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(context.AllocationUnit);
        if(!context.IsSafePoint){return new(Status.Deferred,context.CurrentBytes,"An operation or parallel region is active.");}
        var decision=this.DecideCore(in context);
        if(decision.TargetBytes<context.CurrentBytes){throw new InvalidOperationException("A growth strategy cannot shrink an allocation.");}
        if(decision.Status!=Status.Grow){return decision;}
        if(decision.TargetBytes<context.RequiredBytes){throw new InvalidOperationException("The proposed capacity does not satisfy the requested size.");}
        if(decision.TargetBytes>context.LimitBytes){return new(Status.Rejected,context.CurrentBytes,"The proposed capacity exceeds the resource limit.");}
        if(decision.TargetBytes%context.AllocationUnit!=0
            ||(context.PowerOfTwo&&(decision.TargetBytes<=0||(decision.TargetBytes&(decision.TargetBytes-1))!=0))){
            throw new InvalidOperationException("The proposed capacity is not supported by this resource.");
        }
        return decision;
    }
    protected abstract Decision DecideCore(in Context context);
}

internal static class Capacity{
    internal static long Round(long value,in Context context){
        value=Math.Max(value,context.AllocationUnit);
        if(context.PowerOfTwo){
            long result=1;
            while(result<value){
                if(result>long.MaxValue/2){return long.MaxValue;}
                result*=2;
            }
            return result;
        }
        return checked(((value-1)/context.AllocationUnit+1)*context.AllocationUnit);
    }
}
