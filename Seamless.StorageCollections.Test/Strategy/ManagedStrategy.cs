namespace Seamless.StorageCollections.Test;

/// <summary>
/// Strategy for testing dictionaries with managed (reference-type) keys and values.
/// Uses <see cref="string"/> for both key and value.
/// </summary>
public sealed class ManagedStrategy:IDictTestStrategy<string,string>{
    public string Key(int seed=0)=>$"key{seed}";
    public string Value(int seed=0)=>$"value{seed}";
    public IDictionary<string,string> CreateMemory()=>new System.Collections.Generic.Dictionary<string,string>();
    public IDictionary<string,string> CreateDisk()=>new System.Collections.Generic.Dictionary<string,string>();
}
