namespace Seamless.Linq.Optimization;

public abstract class EnumerableSource<T>:global::Common.Base<T> where T : notnull {
    private readonly T[] values;
    protected EnumerableSource(int count = 4_096) : base(count) => this.values=this.srcCount(count);
    protected global::System.Collections.Generic.IEnumerable<T> Source() { for(var index = 0;index<this.values.Length;index++) yield return this.values[index]; }
}
