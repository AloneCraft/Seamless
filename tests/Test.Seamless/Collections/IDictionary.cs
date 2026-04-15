namespace Test.Seamless.Collections;

/// <summary><see cref="IDictionary{TKey,TValue}"/> 経由テスト — 値のみアンマネージド (string, int)。</summary>
public class ManagedManagedIDictionaryInterface : IDictionaryTestBase<ManagedDictStrategy, string, string>;
public class ManagedUnmanagedIDictionaryInterface : IDictionaryTestBase<UnmanagedValueDictStrategy, string, int>;
public class UnmanagedManagedIDictionaryInterface : IDictionaryTestBase<UnmanagedKeyDictStrategy, int, string>;
public class UnmanagedUnmanagedIDictionaryInterface : IDictionaryTestBase<UnmanagedDictStrategy, int, int>;
