using System.Text;
namespace Seamless.Database.SqlServer.Functions;
/// <summary>
/// 集約の関数。NULL は数えない。値が1つも無ければ NULL(COUNT は 0)。
/// 結果の型は SQL Server と同じ(int の SUM・AVG は int で、AVG は0の方へ切り捨てる)。
/// </summary>
public static class AggregateFunctions{
    /// <summary>SUM の int 版。int に収まらなければ 8115。</summary>
    [SqlFunction("SUM")]
    public static int? Sum(IEnumerable<int?> values){
        long total=0;
        var any=false;
        foreach(var value in values){
            if(value is{} number){
                total+=number;
                any=true;
                if(total>int.MaxValue||total<int.MinValue){
                    throw Overflow("int");
                }
            }
        }
        if(!any){
            return null;
        }
        return (int)total;
    }
    /// <summary>SUM の bigint 版。</summary>
    [SqlFunction("SUM")]
    public static long? Sum(IEnumerable<long?> values){
        long total=0;
        var any=false;
        foreach(var value in values){
            if(value is{} number){
                try{
                    total=checked(total+number);
                }catch(OverflowException){
                    throw Overflow("bigint");
                }
                any=true;
            }
        }
        if(!any){
            return null;
        }
        return total;
    }
    /// <summary>SUM の decimal 版。精度の上限は翻訳器が型で確かめる。</summary>
    [SqlFunction("SUM")]
    public static decimal? Sum(IEnumerable<decimal?> values){
        decimal total=0;
        var any=false;
        foreach(var value in values){
            if(value is{} number){
                total+=number;
                any=true;
            }
        }
        if(!any){
            return null;
        }
        return total;
    }
    /// <summary>SUM の float 版。</summary>
    [SqlFunction("SUM")]
    public static double? Sum(IEnumerable<double?> values){
        double total=0;
        var any=false;
        foreach(var value in values){
            if(value is{} number){
                total+=number;
                any=true;
            }
        }
        if(!any){
            return null;
        }
        return total;
    }
    /// <summary>AVG の int 版。合計を件数で割って0の方へ切り捨てる。</summary>
    [SqlFunction("AVG")]
    public static int? Avg(IEnumerable<int?> values){
        long total=0;
        long count=0;
        foreach(var value in values){
            if(value is{} number){
                total+=number;
                count++;
            }
        }
        if(count==0){
            return null;
        }
        return (int)(total/count);
    }
    /// <summary>AVG の bigint 版。</summary>
    [SqlFunction("AVG")]
    public static long? Avg(IEnumerable<long?> values){
        Int128 total=0;
        long count=0;
        foreach(var value in values){
            if(value is{} number){
                total+=number;
                count++;
            }
        }
        if(count==0){
            return null;
        }
        return (long)(total/count);
    }
    /// <summary>
    /// AVG の decimal 版。結果の scale は max(元の scale,6)。割り算の余りは scale の桁で切り捨てる(本物と同じ)。
    /// </summary>
    [SqlFunction("AVG")]
    public static decimal? Avg(IEnumerable<decimal?> values,byte scale){
        decimal total=0;
        long count=0;
        foreach(var value in values){
            if(value is{} number){
                total+=number;
                count++;
            }
        }
        if(count==0){
            return null;
        }
        var resultScale=Math.Max(scale,(byte)6);
        var quotient=total/count;
        return decimal.Round(quotient,resultScale,MidpointRounding.ToZero);
    }
    /// <summary>AVG の float 版。</summary>
    [SqlFunction("AVG")]
    public static double? Avg(IEnumerable<double?> values){
        double total=0;
        long count=0;
        foreach(var value in values){
            if(value is{} number){
                total+=number;
                count++;
            }
        }
        if(count==0){
            return null;
        }
        return total/count;
    }
    /// <summary>MIN の値型版。</summary>
    [SqlFunction("MIN")]
    public static T? Min<T>(IEnumerable<T?> values)where T:struct,IComparable<T>{
        return Extreme(values,-1);
    }
    /// <summary>MAX の値型版。</summary>
    [SqlFunction("MAX")]
    public static T? Max<T>(IEnumerable<T?> values)where T:struct,IComparable<T>{
        return Extreme(values,1);
    }
    /// <summary>MIN の文字列版。照合順序で比べる。</summary>
    [SqlFunction("MIN")]
    public static string? Min(IEnumerable<string?> values,SqlCollation collation)=>Extreme(values,collation,-1);
    /// <summary>MAX の文字列版。照合順序で比べる。</summary>
    [SqlFunction("MAX")]
    public static string? Max(IEnumerable<string?> values,SqlCollation collation)=>Extreme(values,collation,1);
    /// <summary>COUNT(式)。NULL でない値の数。int に収まらなければ 8115。</summary>
    [SqlFunction("COUNT")]
    public static int Count<T>(IEnumerable<T> values){
        long count=0;
        foreach(var value in values){
            if(value is not null){
                count++;
            }
        }
        if(count>int.MaxValue){
            throw Overflow("int");
        }
        return (int)count;
    }
    private static T? Extreme<T>(IEnumerable<T?> values,int direction)where T:struct,IComparable<T>{
        T? best=null;
        foreach(var value in values){
            if(value is{} number&&(best is not{} current||number.CompareTo(current)*direction>0)){
                best=number;
            }
        }
        return best;
    }
    private static string? Extreme(IEnumerable<string?> values,SqlCollation collation,int direction){
        string? best=null;
        foreach(var value in values){
            if(value is not null&&(best is null||collation.Compare(value.TrimEnd(' '),best.TrimEnd(' '))*direction>0)){
                best=value;
            }
        }
        return best;
    }
    /// <summary>STRING_AGG。NULL を飛ばして区切りでつなぐ。値が無ければ NULL。並び順は呼び出し側(WITHIN GROUP)が決める。</summary>
    [SqlFunction("STRING_AGG")]
    public static string? StringAgg(IEnumerable<string?> values,string? separator){
        StringBuilder? builder=null;
        foreach(var value in values){
            if(value is null){
                continue;
            }
            if(builder is null){
                builder=new StringBuilder(value);
            }else{
                builder.Append(separator).Append(value);
            }
        }
        return builder?.ToString();
    }
    private static SqlFunctionException Overflow(string type)=>new($"Arithmetic overflow error converting expression to data type {type}.",8115);
}
