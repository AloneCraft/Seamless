namespace Seamless.Database.SqlServer.Functions;
/// <summary>SQL Server が同じ入力で出すエラーと同じ番号を持つ例外。番号は本物の SqlException.Number と照合する。</summary>
public sealed class SqlFunctionException(string message,int number):InvalidOperationException(message){
    /// <summary>SQL Server のエラー番号。</summary>
    public int Number{get;}=number;
}
