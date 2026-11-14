using System.Data.SqlTypes;
namespace Seamless.Database.SqlServer.Functions;
/// <summary>
/// 比較演算子(= &lt;&gt; &lt; &lt;= &gt; &gt;=)と IN。どちらかが NULL なら NULL(不明)を返す SQL の三値論理。
/// 文字列は照合順序で比べ、末尾の空白は無視する(SQL Server と同じ)。
/// 型をそろえる変換は翻訳器が先に済ませ、ここには同じ型の2つの値が来る。
/// </summary>
public static class SqlCompare{
    /// <summary>値型の大小(NULL なら NULL)。</summary>
    public static int? Compare<T>(T? left,T? right)where T:struct,IComparable<T>{
        if(left is not{} a||right is not{} b){
            return null;
        }
        return a.CompareTo(b);
    }
    /// <summary>文字列の大小(NULL なら NULL)。末尾の空白を除いて照合順序で比べる。</summary>
    public static int? Compare(string? left,string? right,SqlCollation collation){
        if(left is null||right is null){
            return null;
        }
        return collation.Compare(left.TrimEnd(' '),right.TrimEnd(' '));
    }
    /// <summary>バイト列の大小(NULL なら NULL)。末尾の 0x00 の違いは無視する(SQL Server と同じ)。</summary>
    public static int? Compare(byte[]? left,byte[]? right){
        if(left is null||right is null){
            return null;
        }
        return new SqlBinary(left).CompareTo(new SqlBinary(right));
    }
    /// <summary>uniqueidentifier の大小。SQL Server の並び(後ろのバイトから比べる)に従う。</summary>
    public static int? Compare(Guid? left,Guid? right){
        if(left is not{} a||right is not{} b){
            return null;
        }
        return new SqlGuid(a).CompareTo(new SqlGuid(b));
    }
    /// <summary>bit の大小。</summary>
    public static int? Compare(bool? left,bool? right){
        if(left is not{} a||right is not{} b){
            return null;
        }
        return a.CompareTo(b);
    }
    /// <summary>= 。</summary>
    public static bool? Equal(int? comparison)=>Test(comparison,static value=>value==0);
    /// <summary>&lt;&gt; 。</summary>
    public static bool? NotEqual(int? comparison)=>Test(comparison,static value=>value!=0);
    /// <summary>&lt; 。</summary>
    public static bool? Less(int? comparison)=>Test(comparison,static value=>value<0);
    /// <summary>&lt;= 。</summary>
    public static bool? LessOrEqual(int? comparison)=>Test(comparison,static value=>value<=0);
    /// <summary>&gt; 。</summary>
    public static bool? Greater(int? comparison)=>Test(comparison,static value=>value>0);
    /// <summary>&gt;= 。</summary>
    public static bool? GreaterOrEqual(int? comparison)=>Test(comparison,static value=>value>=0);
    private static bool? Test(int? comparison,Func<int,bool> test){
        if(comparison is{} value){
            return test(value);
        }
        return null;
    }
    /// <summary>
    /// IN。どれかと等しければ true。等しいものが無く、比べた中に NULL(不明)があれば NULL、無ければ false。
    /// 比べた結果(<see cref="Equal"/> の値)の並びを受け取る。
    /// </summary>
    public static bool? In(IEnumerable<bool?> comparisons){
        var unknown=false;
        foreach(var comparison in comparisons){
            if(comparison is true){
                return true;
            }
            if(comparison is null){
                unknown=true;
            }
        }
        if(unknown){
            return null;
        }
        return false;
    }
}
