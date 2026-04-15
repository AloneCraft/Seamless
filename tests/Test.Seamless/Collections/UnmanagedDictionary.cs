namespace Test.Seamless.Collections;

public abstract class UnmanagedDictionary<TStrategy,TKey,TValue>:DictionaryTestBase<TStrategy,TKey,TValue>
    where TStrategy:IDictTestStrategy<TKey,TValue>
    where TKey:unmanaged, IEquatable<TKey>
    where TValue:unmanaged, IEquatable<TValue>{
}

public sealed class UnmanagedDictionary_UnmanagedStruct_UnmanagedStruct
    : UnmanagedDictionary<UnmanagedStoreDictionaryStrategy<UnmanagedStructDictTypeProvider,UnmanagedStructDictTypeProvider,UnmanagedStruct,UnmanagedStruct>,UnmanagedStruct,UnmanagedStruct>{
}
