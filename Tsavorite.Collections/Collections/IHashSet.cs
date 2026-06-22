namespace Seamless.Collections.Tsavorite;

/// <summary>
/// Tsavorite バックエンドを持つ HashSet の共通インターフェース。
/// HashSet と HashSet の両実装が共有する操作を公開する。
/// 引数は ITsavoriteHashSet&lt;T&gt; を受け取る既定実装を持つ。
/// </summary>
public interface IHashSet<T>:IMemoryCollection<T>, IDisposable {
    /// <summary>このインスタンスが既に破棄されているかどうかを取得する。</summary>
    bool IsDisposed { get; }
    /// <summary>
    /// 内部 session を張り替えて、Tsavorite が保持している古い epoch 世代参照を解放する。
    /// 既定実装は no-op で、session を明示管理する実装が必要に応じて上書きする。
    /// </summary>
    void Refresh() {
    }
    new bool Add(T item);

}
