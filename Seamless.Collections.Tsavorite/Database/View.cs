namespace Seamless.Database;

/// <summary>
/// コンテナの Table/View への通常 LINQ で定義する宣言ビュー。列挙のたびに定義を評価する。
/// 定義ラムダ内の LINQ はコンパイル時に FLinq が展開する。
/// </summary>
public sealed class View<TResult>:IEnumerable<TResult>{
    private readonly Func<IEnumerable<TResult>>definition;
    internal View(Func<IEnumerable<TResult>>definition,string name,string sqlName){
        this.definition=definition;
        this.Name=name;
        this.SqlName=sqlName;
    }
    /// <summary>宣言メンバー名。</summary>
    public string Name{get;}
    /// <summary>SQL Server 互換名。[View]属性が無ければ宣言名。</summary>
    public string SqlName{get;}
    public IEnumerator<TResult>GetEnumerator()=>this.definition().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
}
