namespace Seamless.Database.SqlServer.Functions;
/// <summary>
/// SQL Server の組み込み関数を実装する静的メソッドの目印。<see cref="SqlFunctionCatalog"/> が名前で集める。
/// 1つの SQL 名に、引数の型の組ごとの複数のメソッドを付けてよい。
/// </summary>
[AttributeUsage(AttributeTargets.Method,AllowMultiple=true,Inherited=false)]
public sealed class SqlFunctionAttribute(string name):Attribute{
    /// <summary>SQL 上の関数名(大文字)。</summary>
    public string Name{get;}=name;
}
