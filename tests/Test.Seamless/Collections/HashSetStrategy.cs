using Seamless.Collections;
using Seamless.Stores;
namespace Test.Seamless.Collections;

/// <summary>
/// <see cref="Collections.ISet{T}"/> を通じた HashSet テストの生成戦略。
/// 静的ポリモーフィズムにより具体型を差し替える。
/// </summary>
public interface IHashSetTestStrategy<T> where T:IEquatable<T> {
    /// <summary>インデックスから要素を生成する。</summary>
    static abstract T Elem(int i);
    /// <summary>メモリバックのセットを生成する。</summary>
    static abstract SetHandle<T> CreateMemorySet();
}

/// <summary>
/// テスト用ストア＋セットラッパー。
/// <see cref="Collections.ISet{T}"/> としてセットを公開し、
/// セット演算引数用に具体型 <see cref="ManagedHashSet{T}"/> も公開する。
/// </summary>
public sealed class SetHandle<T>(IDisposable store,ManagedHashSet<T> set):IDisposable
    where T:IEquatable<T> {
    /// <summary>Seamless.Collections.ISet{T} 経由でアクセスするセット。</summary>
    public global::Seamless.Collections.ISet<T> Set => set;

    /// <summary>セット演算メソッド（IntersectWith 等）への引数に渡す具体型。</summary>
    public ManagedHashSet<T> Concrete => set;

    public void Dispose(){
        set.Dispose();
        store.Dispose();
    }
}

/// <summary>ManagedHashSet&lt;int&gt; 戦略。</summary>
public struct ManagedIntHashSetStrategy:IHashSetTestStrategy<int> {
    public static int Elem(int i) => i;
    public static SetHandle<int> CreateMemorySet(){
        var store=Factory.Create().Create<int>();
        return new SetHandle<int>(store,store.Create());
    }
}

/// <summary>ManagedHashSet&lt;string&gt; 戦略。</summary>
public struct ManagedStringHashSetStrategy:IHashSetTestStrategy<string> {
    public static string Elem(int i) => i.ToString();
    public static SetHandle<string> CreateMemorySet(){
        var store=Factory.Create().Create<string>();
        return new SetHandle<string>(store,store.Create());
    }
}
