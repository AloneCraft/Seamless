namespace Seamless.Database.SqlServer.Functions;
/// <summary>式の中の副問い合わせ。</summary>
public static class SqlSubquery{
    /// <summary>
    /// 値を1つ返す副問い合わせ。行が無ければ NULL、2行以上なら 512(本物と同じ)。
    /// </summary>
    public static T? Scalar<T>(IEnumerable<T?> values){
        using var enumerator=values.GetEnumerator();
        if(!enumerator.MoveNext()){
            return default;
        }
        var first=enumerator.Current;
        if(enumerator.MoveNext()){
            throw new SqlFunctionException("Subquery returned more than 1 value. This is not permitted when the subquery follows =, !=, <, <= , >, >= or when the subquery is used as an expression.",512);
        }
        return first;
    }
}
