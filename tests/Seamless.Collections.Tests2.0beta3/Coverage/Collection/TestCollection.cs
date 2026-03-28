namespace Seamless.Collections.Tests;
internal sealed class TestCollection<TKey,TValue>(CollectionStore<TKey,TValue> store)
    :Collection<TKey,TValue>(store.Store,store.LogMemorySize,store.MutableFraction)
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>{
    private bool disposed;
    public override bool IsDisposed=>this.disposed;
    protected override void Dispose(bool disposing){
        base.Dispose(disposing);
        this.disposed=true;
    }
}
