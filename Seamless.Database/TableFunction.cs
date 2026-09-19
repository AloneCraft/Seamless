namespace Seamless.Database;

/// <summary>
/// コンテナの Table/View への通常 LINQ で定義する宣言テーブル関数。
/// 引数はメソッド引数としてラムダにキャプチャされ、列挙のたびに定義を評価する。
/// </summary>
public sealed class TableFunction<TResult>:IEnumerable<TResult> {
    private readonly Func<IEnumerable<TResult>> definition;
    internal TableFunction(Func<IEnumerable<TResult>> definition,string name) {
        this.definition=definition;
        this.Name=name;
    }
    /// <summary>宣言メソッド名。</summary>
    public string Name { get; }
    public IEnumerator<TResult> GetEnumerator() => this.definition().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}
