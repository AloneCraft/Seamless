using System.Globalization;
using Microsoft.Data.SqlClient;
using Seamless.Database.SqlServer.Functions;
namespace Database.SqlServer.Functions.Tests;
/// <summary>本物の SQL Server が無いときは飛ばす。接続文字列は環境変数 SEAMLESS_SQLSERVER_TEST_CONNECTION から読む。</summary>
public sealed class SqlServerFactAttribute:FactAttribute{
    public SqlServerFactAttribute(){
        if(string.IsNullOrWhiteSpace(SqlServerOracle.ConnectionString)){
            this.Skip="SEAMLESS_SQLSERVER_TEST_CONNECTION が未設定のため、本物の SQL Server との照合は未検証です。";
        }
    }
}
/// <summary>
/// 同じ式を本物の SQL Server と C# の実装で評価し、値またはエラー番号が一致するかを確かめる。
/// 接続ごとに SET LANGUAGE us_english・DATEFIRST 7・DATEFORMAT mdy にそろえる(<see cref="SqlSession.Default"/> と同じ)。
/// </summary>
internal static class SqlServerOracle{
    internal static string? ConnectionString=>Environment.GetEnvironmentVariable("SEAMLESS_SQLSERVER_TEST_CONNECTION");
    internal static SqlCollation Latin=>SqlCollation.Get("SQL_Latin1_General_CP1_CI_AS");
    internal static SqlCollation Binary=>SqlCollation.Get("Latin1_General_BIN2");
    internal static SqlSession Session=>SqlSession.Default;
    /// <summary>式の一覧を照合し、一致しなかったものをまとめて失敗にする。</summary>
    internal static void Check(params (string Sql,Func<object?> Actual)[] cases){
        using var connection=new SqlConnection(ConnectionString);
        connection.Open();
        using(var setup=connection.CreateCommand()){
            setup.CommandText="SET LANGUAGE us_english; SET DATEFIRST 7; SET DATEFORMAT mdy;";
            setup.ExecuteNonQuery();
        }
        var failures=new List<string>();
        foreach(var (sql,actual) in cases){
            var expected=Evaluate(connection,sql);
            var got=Run(actual);
            if(!Same(expected,got)){
                failures.Add($"{sql}\n    本物: {Show(expected)}\n    実装: {Show(got)}");
            }
        }
        Assert.True(failures.Count==0,$"{failures.Count}/{cases.Length} 件が本物と違う:\n"+string.Join("\n",failures));
    }
    private readonly record struct Outcome(object? Value,int? Error);
    private static Outcome Evaluate(SqlConnection connection,string sql){
        using var command=connection.CreateCommand();
        command.CommandText="SELECT "+sql;
        try{
            var value=command.ExecuteScalar();
            if(value is DBNull){
                value=null;
            }
            return new(value,null);
        }catch(SqlException error){
            return new(null,error.Number);
        }
    }
    private static Outcome Run(Func<object?> actual){
        try{
            return new(actual(),null);
        }catch(SqlFunctionException error){
            return new(null,error.Number);
        }
    }
    private static bool Same(Outcome expected,Outcome actual){
        if(expected.Error is not null||actual.Error is not null){
            return expected.Error==actual.Error;
        }
        var left=expected.Value;
        var right=actual.Value;
        if(left is null||right is null){
            return left is null&&right is null;
        }
        if(left is double or float&&right is double or float){
            var a=Convert.ToDouble(left,CultureInfo.InvariantCulture);
            var b=Convert.ToDouble(right,CultureInfo.InvariantCulture);
            return a==b||Math.Abs(a-b)<=Math.Abs(a)*1e-15;
        }
        if(IsNumber(left)&&IsNumber(right)){
            return Convert.ToDecimal(left,CultureInfo.InvariantCulture)==Convert.ToDecimal(right,CultureInfo.InvariantCulture);
        }
        if(left is byte[] first&&right is byte[] second){
            return first.AsSpan().SequenceEqual(second);
        }
        if(left is bool flag&&IsNumber(right)){
            return Convert.ToInt32(flag)==Convert.ToInt32(right,CultureInfo.InvariantCulture);
        }
        return left.Equals(right);
    }
    private static bool IsNumber(object value)=>value is byte or short or int or long or decimal;
    private static string Show(Outcome outcome){
        if(outcome.Error is{} number){
            return "エラー "+number.ToString(CultureInfo.InvariantCulture);
        }
        if(outcome.Value is null){
            return "NULL";
        }
        if(outcome.Value is byte[] bytes){
            return "0x"+Convert.ToHexString(bytes);
        }
        if(outcome.Value is DateTime date){
            return date.ToString("yyyy-MM-dd HH:mm:ss.fffffff",CultureInfo.InvariantCulture);
        }
        return $"{outcome.Value} ({outcome.Value.GetType().Name})";
    }
}
