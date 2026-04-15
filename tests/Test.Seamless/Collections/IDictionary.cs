namespace Test.Seamless.Collections;

/// <summary><see cref="IDictionary{TKey,TValue}"/> 経由テスト — 値のみアンマネージド (string, int)。</summary>
public class ManagedManagedIDictionaryInterface : IDictionaryTestBase<ManagedStoreDictionaryStrategy<StringDictTypeProvider, StringDictTypeProvider, string, string>, string, string>;
public class ManagedUnmanagedIDictionaryInterface : IDictionaryTestBase<UnmanagedValueDictionaryStrategy<StringDictTypeProvider, IntDictTypeProvider, string, int>, string, int>;
public class UnmanagedManagedIDictionaryInterface : IDictionaryTestBase<UnmanagedKeyDictionaryStrategy<IntDictTypeProvider, StringDictTypeProvider, int, string>, int, string>;
public class UnmanagedUnmanagedIDictionaryInterface : IDictionaryTestBase<UnmanagedStoreDictionaryStrategy<IntDictTypeProvider, IntDictTypeProvider, int, int>, int, int>;
