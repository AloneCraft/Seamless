namespace Test.Seamless.Collections;

public abstract class ManagedDictionary<TStrategy,TKey,TValue>:DictionaryTestBase<TStrategy,TKey,TValue>
    where TStrategy:IDictTestStrategy<TKey,TValue>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>;
public sealed class ManagedDictionary_String_String:ManagedDictionary<ManagedDictStrategy,string,string>;
public sealed class ManagedDictionary_String_Int:ManagedDictionary<UnmanagedValueDictStrategy,string,int>;
public sealed class ManagedDictionary_Int_String:ManagedDictionary<UnmanagedKeyDictStrategy,int,string>;
