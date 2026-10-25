namespace Seamless.Execution.Equalities;

public interface IStrategy<TSelf,T> where TSelf:struct,IStrategy<TSelf,T>{
    static abstract bool Equals(ref TSelf self,T? left,T? right);
    static abstract int GetHashCode(ref TSelf self,T value);
}
public readonly struct Default<T>:IStrategy<Default<T>,T>{
    public static bool Equals(ref Default<T> self,T? left,T? right)=>EqualityComparer<T>.Default.Equals(left,right);
    public static int GetHashCode(ref Default<T> self,T value)=>EqualityComparer<T>.Default.GetHashCode(value!);
}
public sealed class Adapter<T,TStrategy>(TStrategy strategy):IEqualityComparer<T> where TStrategy:struct,IStrategy<TStrategy,T>{
    private TStrategy strategy=strategy;
    public bool Equals(T? left,T? right)=>TStrategy.Equals(ref this.strategy,left,right);
    public int GetHashCode(T value)=>TStrategy.GetHashCode(ref this.strategy,value);
}
