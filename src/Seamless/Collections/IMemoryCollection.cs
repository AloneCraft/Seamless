namespace Seamless.Collections;

/// <summary>
/// デバッグ表示や検査のために、メモリ上に存在する要素だけを列挙する。
/// </summary>
public interface IMemoryCollection<T>:ICollection<T> {
    new IEnumerator<T> GetEnumerator();
}
