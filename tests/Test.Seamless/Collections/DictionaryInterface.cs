namespace Test.Seamless.Collections;

/// <summary><see cref="System.Collections.Generic.IDictionary{TKey,TValue}"/> 経由テスト — 両マネージド (string, string)。</summary>
public class DictionaryInterface_Managed:IDictionaryTestBase<ManagedDictStrategy,string,string>{}

/// <summary><see cref="System.Collections.Generic.IDictionary{TKey,TValue}"/> 経由テスト — 両アンマネージド (int, int)。</summary>
public class DictionaryInterface_Unmanaged:IDictionaryTestBase<UnmanagedDictStrategy,int,int>{}

/// <summary><see cref="System.Collections.Generic.IDictionary{TKey,TValue}"/> 経由テスト — キーのみアンマネージド (int, string)。</summary>
public class DictionaryInterface_UnmanagedKey:IDictionaryTestBase<UnmanagedKeyDictStrategy,int,string>{}

/// <summary><see cref="System.Collections.Generic.IDictionary{TKey,TValue}"/> 経由テスト — 値のみアンマネージド (string, int)。</summary>
public class DictionaryInterface_UnmanagedValue:IDictionaryTestBase<UnmanagedValueDictStrategy,string,int>{}
