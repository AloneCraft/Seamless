using ProductTransactions;
using Seamless.FLinq;
using NativeRows=Seamless.Collections.Tsavorite.Set<int,ProductTransactions.Row>;
// staticクラスは現行生成器の対象外なので、既存Queriesと同じsealed型にする。
internal sealed class ComparisonQueries{
    [LinqIntercept(true)]
    internal static long NativeSum(NativeRows rows)=>rows.Where(static row=>row.Id%2==0).Sum(static row=>row.Balance);
    [LinqIntercept(true)]
    internal static long NativeCount(NativeRows rows)=>rows.LongCount(static row=>row.Id>=0);
    [LinqIntercept(true)]
    internal static long NativeGroups(NativeRows rows)=>Fold(rows.GroupBy(static row=>row.Id%64).Select(static g=>(g.Key,g.LongCount(),g.Sum(static row=>row.Balance))).ToArray());
    [LinqIntercept(true)]
    internal static long NativeJoin(NativeRows rows)=>rows.Where(static row=>row.Id<64).Join(rows,static row=>row.Id,static row=>row.Id%64,static(left,right)=>left.Balance+right.Balance).Sum();
    [LinqIntercept(true)]
    internal static long TransactionSum(Database.TypedTransaction tx)=>tx.Accounts.Where(static row=>row.Id%2==0).Sum(static row=>row.Balance);
    [LinqIntercept(true)]
    internal static long TransactionCount(Database.TypedTransaction tx)=>tx.Accounts.LongCount(static row=>row.Id>=0);
    [LinqIntercept(true)]
    internal static long TransactionGroups(Database.TypedTransaction tx)=>Fold(tx.Accounts.GroupBy(static row=>row.Id%64).Select(static g=>(g.Key,g.LongCount(),g.Sum(static row=>row.Balance))).ToArray());
    [LinqIntercept(true)]
    internal static long TransactionJoin(Database.TypedTransaction tx)=>tx.Accounts.Where(static row=>row.Id<64).Join(tx.Accounts,static row=>row.Id,static row=>row.Id%64,static(left,right)=>left.Balance+right.Balance).Sum();
    // assembly既定のintercept=false。引数をIEnumerableにして同じ保存先をBCLで列挙する。
    internal static long Linq(IEnumerable<Row> rows,int query)=>query switch{
        0=>rows.Where(static row=>row.Id%2==0).Sum(static row=>row.Balance),
        1=>rows.LongCount(static row=>row.Id>=0),
        2=>Fold(rows.GroupBy(static row=>row.Id%64).Select(static g=>(g.Key,g.LongCount(),g.Sum(static row=>row.Balance))).ToArray()),
        3=>rows.Where(static row=>row.Id<64).Join(rows,static row=>row.Id,static row=>row.Id%64,static(left,right)=>left.Balance+right.Balance).Sum(),
        _=>throw new ArgumentOutOfRangeException(nameof(query))
    };
    internal static long Native(NativeRows rows,int query)=>query switch{0=>NativeSum(rows),1=>NativeCount(rows),2=>NativeGroups(rows),3=>NativeJoin(rows),_=>throw new ArgumentOutOfRangeException(nameof(query))};
    internal static long Transaction(Database.TypedTransaction tx,int query)=>query switch{0=>TransactionSum(tx),1=>TransactionCount(tx),2=>TransactionGroups(tx),3=>TransactionJoin(tx),_=>throw new ArgumentOutOfRangeException(nameof(query))};
    internal static long Fold((int Key,long Count,long Total)[] groups){long value=0;foreach(var group in groups){value=checked(value+(group.Key+1)*group.Total+17*group.Count);}return value;}
}
