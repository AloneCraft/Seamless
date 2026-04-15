namespace Test.Seamless.Collections;

/// <summary><see cref="Collections.ISet{T}"/> 経由テスト — int 要素。</summary>
public class HashSet_ISet_Int:HashSetTestBase<ManagedIntHashSetStrategy,int> { }

/// <summary><see cref="Collections.ISet{T}"/> 経由テスト — string 要素。</summary>
public class HashSet_ISet_String:HashSetTestBase<ManagedStringHashSetStrategy,string> { }
