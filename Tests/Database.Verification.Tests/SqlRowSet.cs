using System.Globalization;
using Microsoft.Data.SqlClient;

namespace Database.Verification.Tests;

/// <summary>SQL Server の結果と Seamless の行を、型の違いを吸収した文字列の集合にして比べる(翻訳器の試験と同じ規則)。</summary>
internal static class SqlRowSet{
    public static SortedSet<string> Query(SqlConnection connection,string sql){
        using var command=connection.CreateCommand();
        command.CommandText=sql;
        command.CommandTimeout=600;
        using var reader=command.ExecuteReader();
        var rows=new SortedSet<string>(StringComparer.Ordinal);
        while(reader.Read()){
            var values=new object?[reader.FieldCount];
            // hierarchyid・geography・geometry などの SQL Server の独自の型(型名が「DB.スキーマ.型」)は、.NET の型を読み込まずにバイト列で比べる。
            for(var i=0;i<values.Length;i++){values[i]=reader.IsDBNull(i)?null:reader.GetDataTypeName(i).Contains('.')?reader.GetSqlBytes(i).Value:reader.GetValue(i);}
            rows.Add(Key(values));
        }
        return rows;
    }

    // 行を、型の違い(DateOnly と DateTime、float と double、decimal の桁の0)を吸収した文字列にする。
    public static string Key(object?[] values)=>string.Join(",",values.Select(static value=>value switch{
        null=>"∅",
        DateOnly date=>date.ToDateTime(TimeOnly.MinValue).ToString("o",CultureInfo.InvariantCulture),
        // SQL Server の datetime には時差の種類が無い。保存した値は Kind=Utc で戻る(取り込みの保存の既存の動き)ので、種類は比べない。
        DateTime date=>DateTime.SpecifyKind(date,DateTimeKind.Unspecified).ToString("o",CultureInfo.InvariantCulture),
        decimal number=>(number/1.000000000000000000000000000000000m).ToString(CultureInfo.InvariantCulture),
        double number=>number.ToString("R",CultureInfo.InvariantCulture),
        float number=>((double)number).ToString("R",CultureInfo.InvariantCulture),
        byte or short or int or long=>System.Convert.ToInt64(value,CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
        bool flag=>flag?"1":"0",
        string text=>"'"+text+"'",
        TimeOnly time=>time.ToTimeSpan().ToString("c",CultureInfo.InvariantCulture),
        TimeSpan time=>time.ToString("c",CultureInfo.InvariantCulture),
        DateTimeOffset offset=>offset.ToString("o",CultureInfo.InvariantCulture),
        byte[] bytes=>"0x"+System.Convert.ToHexString(bytes),
        _=>value.ToString()!
    }));
}
