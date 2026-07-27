namespace Shared.Linq.Optimization;

public abstract class ListSource<T>:global::Common.Base<T> where T : notnull {
    private readonly global::System.Collections.Generic.List<T> values;
    protected ListSource(int count = 4_096) : base(count) => this.values= [.. this.srcCount(count)];
    protected internal global::System.Collections.Generic.List<T> Source() => this.values;
}
