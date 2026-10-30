namespace Seamless.Collections.Iteration;

/// <summary>要素を処理し、falseで走査を打ち切る。可変状態は呼び出しごとのstructに保持する。</summary>
public interface IStrategy<TSelf,in T> where TSelf:struct,IStrategy<TSelf,T>{
    static abstract bool Invoke(ref TSelf self,T item);
}
