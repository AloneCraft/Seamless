namespace Seamless.Database;

/// <summary>
/// コンテナの Table/View への通常 LINQ で定義する宣言テーブル関数。
/// 引数はメソッド引数としてラムダにキャプチャされ、列挙のたびに定義を評価する。
/// </summary>
public sealed class TableFunction<TResult>:IEnumerable<TResult>{
    private readonly Func<IEnumerable<TResult>>definition;
    internal TableFunction(Func<IEnumerable<TResult>>definition,string name,string sqlName){
        this.definition=definition;
        this.Name=name;
        this.SqlName=sqlName;
    }
    /// <summary>宣言メソッド名。</summary>
    public string Name{get;}
    /// <summary>SQL Server 互換名。[Function]属性が無ければ宣言名。</summary>
    public string SqlName{get;}
    public IEnumerator<TResult>GetEnumerator()=>this.definition().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
}
