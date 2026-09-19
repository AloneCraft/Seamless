namespace Seamless.Database;

/// <summary>
/// コンテナの Table/View への通常 LINQ で定義する宣言ビュー。列挙のたびに定義を評価する。
/// 定義ラムダ内の LINQ はコンパイル時に FLinq が展開する。
/// </summary>
public sealed class View<TResult>:IEnumerable<TResult> {
    private readonly Func<IEnumerable<TResult>> definition;
    internal View(Func<IEnumerable<TResult>> definition,string name) {
        this.definition=definition;
        this.Name=name;
    }
    /// <summary>宣言メンバー名。</summary>
    public string Name { get; }
    public IEnumerator<TResult> GetEnumerator() => this.definition().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}
