using Seamless.FLinq;
namespace ProductTransactions;

// 通常LINQ側はassembly既定のintercept=false。入力をIEnumerableへ渡して同じsnapshotを列挙する。
internal sealed class OrderedComparisonQueries{
    [LinqIntercept(true)]
    internal static long[] JoinIdentity(Database.TypedTransaction tx)=>tx.Accounts.Rows.Where(static row=>row.Id<64)
        .Join(tx.Accounts.Rows,static row=>row.Id,static row=>row.Id%64,static(left,right)=>right.Id*257L+left.Balance)
        .OrderByDescending(static value=>value).ThenBy(static value=>value%17).ToArray();
    [LinqIntercept(true)]
    internal static long[] JoinMultiple(Database.TypedTransaction tx)=>tx.Accounts.Rows.Where(static row=>row.Id<64)
        .Join(tx.Accounts.Rows,static row=>row.Id,static row=>row.Id%64,static(left,right)=>right.Id*257L+left.Balance)
        .OrderBy(static value=>value%17).Select(static value=>value+1)
        .OrderByDescending(static value=>value%13,LongComparer()).ThenBy(static value=>value).ToArray();
    [LinqIntercept(true)]
    internal static long[] GroupIdentity(Database.TypedTransaction tx)=>tx.Accounts.Rows.GroupBy(static row=>row.Id%1024)
        .Select(static group=>group.Sum(static row=>row.Balance))
        .OrderByDescending(static value=>value).ThenBy(static value=>value%17).ToArray();
    [LinqIntercept(true)]
    internal static IGrouping<int,Row>[] GroupEscapeMultiple(Database.TypedTransaction tx)=>tx.Accounts.Rows.GroupBy(static row=>row.Id%1024)
        .OrderByDescending(static group=>group.Count()).OrderBy(static group=>group.Key,IntComparer()).ToArray();
    private static IComparer<long> LongComparer()=>Comparer<long>.Default;
    private static IComparer<int> IntComparer()=>Comparer<int>.Default;
    internal static object Expanded(Database.TypedTransaction tx,int query)=>query switch{
        0=>JoinIdentity(tx),1=>JoinMultiple(tx),2=>GroupIdentity(tx),3=>GroupEscapeMultiple(tx),
        _=>throw new ArgumentOutOfRangeException(nameof(query))
    };
    internal static object Linq(IEnumerable<Row> rows,int query)=>query switch{
        0=>rows.Where(static row=>row.Id<64).Join(rows,static row=>row.Id,static row=>row.Id%64,static(left,right)=>right.Id*257L+left.Balance)
            .OrderByDescending(static value=>value).ThenBy(static value=>value%17).ToArray(),
        1=>rows.Where(static row=>row.Id<64).Join(rows,static row=>row.Id,static row=>row.Id%64,static(left,right)=>right.Id*257L+left.Balance)
            .OrderBy(static value=>value%17).Select(static value=>value+1).OrderByDescending(static value=>value%13,LongComparer()).ThenBy(static value=>value).ToArray(),
        2=>rows.GroupBy(static row=>row.Id%1024).Select(static group=>group.Sum(static row=>row.Balance))
            .OrderByDescending(static value=>value).ThenBy(static value=>value%17).ToArray(),
        3=>rows.GroupBy(static row=>row.Id%1024).OrderByDescending(static group=>group.Count()).OrderBy(static group=>group.Key,IntComparer()).ToArray(),
        _=>throw new ArgumentOutOfRangeException(nameof(query))
    };
}
