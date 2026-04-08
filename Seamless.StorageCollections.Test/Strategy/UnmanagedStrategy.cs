namespace Seamless.StorageCollections.Test;

/// <summary>
/// Strategy for testing dictionaries with unmanaged (value-type) keys and values.
/// Uses <see cref="int"/> for both key and value.
/// </summary>
public sealed class UnmanagedStrategy:IDictTestStrategy<int,int>{
    public int Key(int seed=0)=>seed;
    public int Value(int seed=0)=>seed*100;
    public IDictionary<int,int> CreateMemory()=>new System.Collections.Generic.Dictionary<int,int>();
    public IDictionary<int,int> CreateDisk()=>new System.Collections.Generic.Dictionary<int,int>();
}
