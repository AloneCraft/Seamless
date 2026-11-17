using Seamless.FLinq;
namespace ProductTransactions;

// 通常LINQ側はassembly既定のintercept=false。入力をIEnumerableへ渡して同じsnapshotを列挙する。
internal sealed class OrderedComparisonQueries{
    internal readonly record struct Joined(int Id,IEnumerable<Row> Matches);
    [LinqIntercept(true)]
    internal static long[] JoinIdentity(Database.TypedTransaction tx)=>tx.Accounts.Where(static row=>row.Id<64)
        .Join(tx.Accounts,static row=>row.Id,static row=>row.Id%64,static(left,right)=>right.Id*257L+left.Balance)
        .OrderByDescending(static value=>value).ThenBy(static value=>value%17).ToArray();
    [LinqIntercept(true)]
    internal static long[] JoinMultiple(Database.TypedTransaction tx)=>tx.Accounts.Where(static row=>row.Id<64)
        .Join(tx.Accounts,static row=>row.Id,static row=>row.Id%64,static(left,right)=>right.Id*257L+left.Balance)
        .OrderBy(static value=>value%17).Select(static value=>value+1)
        .OrderByDescending(static value=>value%13,LongComparer()).ThenBy(static value=>value).ToArray();
    [LinqIntercept(true)]
    internal static long[] GroupIdentity(Database.TypedTransaction tx)=>tx.Accounts.GroupBy(static row=>row.Id%1024)
        .Select(static group=>group.Sum(static row=>row.Balance))
        .OrderByDescending(static value=>value).ThenBy(static value=>value%17).ToArray();
    [LinqIntercept(true)]
    internal static IGrouping<int,Row>[] GroupEscapeMultiple(Database.TypedTransaction tx)=>tx.Accounts.GroupBy(static row=>row.Id%1024)
        .OrderByDescending(static group=>group.Count()).OrderBy(static group=>group.Key,IntComparer()).ToArray();
    [LinqIntercept(true)]
    internal static long[] GroupJoinCounts(Database.TypedTransaction tx)=>tx.Accounts.Where(static row=>row.Id<256)
        .GroupJoin(tx.Accounts,static row=>row.Id%64,static row=>row.Id%64,static (row,group)=>row.Id*65536L+group.Count()).ToArray();
    [LinqIntercept(true)]
    internal static Joined[] GroupJoinOwned(Database.TypedTransaction tx)=>tx.Accounts.Where(static row=>row.Id<256)
        .GroupJoin(tx.Accounts,static row=>row.Id%64,static row=>row.Id%64,static (row,group)=>new Joined(row.Id,group))
        .OrderBy(static joined=>joined.Id).ToArray();
    [LinqIntercept(true)]
    internal static long[] GroupJoinSpillCounts(Database.TypedTransaction tx)=>tx.Accounts.Where(static row=>row.Id<256)
        .GroupJoin(tx.Accounts,static row=>row.Id&127,static row=>row.Id&8191,static (row,group)=>row.Id*65536L+group.Count()).ToArray();
    [LinqIntercept(true)]
    internal static Joined[] GroupJoinSpillOwned(Database.TypedTransaction tx)=>tx.Accounts.Where(static row=>row.Id<256)
        .GroupJoin(tx.Accounts,static row=>row.Id&127,static row=>row.Id&8191,static (row,group)=>new Joined(row.Id,group))
        .OrderBy(static joined=>joined.Id).ToArray();
    [LinqIntercept(true)]
    internal static long[] CarrierJoin(Database.TypedTransaction tx)=>tx.Accounts.Where(static row=>row.Id<64)
        .Join(tx.Accounts,static row=>new{Key=row.Id,Even=row.Id%2},static row=>new{Key=row.Id%64,Even=row.Id%2},
            static (left,right)=>new{Id=right.Id,Value=right.Id*257L+left.Balance})
        .OrderBy(static item=>item.Id).Select(static item=>item.Value).ToArray();
    [LinqIntercept(true)]
    internal static long[] CarrierChain(Database.TypedTransaction tx)=>tx.Accounts
        .Select(static row=>new{row.Id,Value=row.Balance})
        .OrderBy(static item=>item.Id)
        .Select(static item=>new{Key=item.Id%17,Value=item.Value+item.Id})
        .OrderByDescending(static item=>item.Key)
        .Select(static item=>item.Value).ToArray();
    [LinqIntercept(true)]
    internal static IGrouping<int,Row>[] GroupShape(Database.TypedTransaction tx,int mask,int hot)=>tx.Accounts
        .GroupBy(row=>row.Id<hot?0:row.Id&mask)
        .OrderByDescending(static group=>group.Count()).OrderBy(static group=>group.Key,IntComparer()).ToArray();
    private static IComparer<long> LongComparer()=>Comparer<long>.Default;
    private static IComparer<int> IntComparer()=>Comparer<int>.Default;
    internal static object Expanded(Database.TypedTransaction tx,int query)=>query switch{
        0=>JoinIdentity(tx),1=>JoinMultiple(tx),2=>GroupIdentity(tx),3=>GroupEscapeMultiple(tx),
        _=>throw new ArgumentOutOfRangeException(nameof(query))
    };
    internal static object ExpandedCarrier(Database.TypedTransaction tx,int query,int rows)=>query switch{
        4=>GroupJoinCounts(tx),5=>GroupJoinOwned(tx),6=>CarrierJoin(tx),7=>CarrierChain(tx),
        8=>GroupShape(tx,15,0),9=>GroupShape(tx,8191,0),10=>GroupShape(tx,1023,rows/2),11=>CarrierWide(tx),
        12=>GroupJoinSpillCounts(tx),13=>GroupJoinSpillOwned(tx),
        _=>Expanded(tx,query)
    };
    internal static object LinqCarrier(IEnumerable<Row> rows,int query,int count)=>query switch{
        4=>rows.Where(static row=>row.Id<256).GroupJoin(rows,static row=>row.Id%64,static row=>row.Id%64,
            static (row,group)=>row.Id*65536L+group.Count()).ToArray(),
        5=>rows.Where(static row=>row.Id<256).GroupJoin(rows,static row=>row.Id%64,static row=>row.Id%64,
            static (row,group)=>new Joined(row.Id,group)).OrderBy(static joined=>joined.Id).ToArray(),
        6=>rows.Where(static row=>row.Id<64).Join(rows,static row=>new{Key=row.Id,Even=row.Id%2},
            static row=>new{Key=row.Id%64,Even=row.Id%2},static (left,right)=>new{Id=right.Id,Value=right.Id*257L+left.Balance})
            .OrderBy(static item=>item.Id).Select(static item=>item.Value).ToArray(),
        7=>rows.Select(static row=>new{row.Id,Value=row.Balance}).OrderBy(static item=>item.Id)
            .Select(static item=>new{Key=item.Id%17,Value=item.Value+item.Id}).OrderByDescending(static item=>item.Key)
            .Select(static item=>item.Value).ToArray(),
        8=>LinqGroupShape(rows,15,0),9=>LinqGroupShape(rows,8191,0),10=>LinqGroupShape(rows,1023,count/2),11=>LinqCarrierWide(rows),
        12=>rows.Where(static row=>row.Id<256).GroupJoin(rows,static row=>row.Id&127,static row=>row.Id&8191,
            static (row,group)=>row.Id*65536L+group.Count()).ToArray(),
        13=>rows.Where(static row=>row.Id<256).GroupJoin(rows,static row=>row.Id&127,static row=>row.Id&8191,
            static (row,group)=>new Joined(row.Id,group)).OrderBy(static joined=>joined.Id).ToArray(),
        _=>Linq(rows,query)
    };
    [LinqIntercept(true)]
    internal static long[] CarrierWide(Database.TypedTransaction tx)=>tx.Accounts
        .Select(static row=>new{row.Id,
            V1=row.Balance+1L,V2=row.Balance+2L,V3=row.Balance+3L,V4=row.Balance+4L,V5=row.Balance+5L,
            V6=row.Balance+6L,V7=row.Balance+7L,V8=row.Balance+8L,V9=row.Balance+9L,V10=row.Balance+10L,
            V11=row.Balance+11L,V12=row.Balance+12L,V13=row.Balance+13L,V14=row.Balance+14L,V15=row.Balance+15L,
            V16=row.Balance+16L,V17=row.Balance+17L,V18=row.Balance+18L,V19=row.Balance+19L,V20=row.Balance+20L,
            V21=row.Balance+21L,V22=row.Balance+22L,V23=row.Balance+23L,V24=row.Balance+24L,V25=row.Balance+25L,
            V26=row.Balance+26L,V27=row.Balance+27L,V28=row.Balance+28L,V29=row.Balance+29L,V30=row.Balance+30L,
            V31=row.Balance+31L
        }).OrderBy(static item=>item.Id).Select(static item=>item.V1+item.V2+item.V3+item.V4+item.V5+item.V6+item.V7+item.V8+item.V9+item.V10+item.V11+item.V12+item.V13+item.V14+item.V15+item.V16+item.V17+item.V18+item.V19+item.V20+item.V21+item.V22+item.V23+item.V24+item.V25+item.V26+item.V27+item.V28+item.V29+item.V30+item.V31).ToArray();
    private static long[] LinqCarrierWide(IEnumerable<Row> rows)=>rows
        .Select(static row=>new{row.Id,
            V1=row.Balance+1L,V2=row.Balance+2L,V3=row.Balance+3L,V4=row.Balance+4L,V5=row.Balance+5L,
            V6=row.Balance+6L,V7=row.Balance+7L,V8=row.Balance+8L,V9=row.Balance+9L,V10=row.Balance+10L,
            V11=row.Balance+11L,V12=row.Balance+12L,V13=row.Balance+13L,V14=row.Balance+14L,V15=row.Balance+15L,
            V16=row.Balance+16L,V17=row.Balance+17L,V18=row.Balance+18L,V19=row.Balance+19L,V20=row.Balance+20L,
            V21=row.Balance+21L,V22=row.Balance+22L,V23=row.Balance+23L,V24=row.Balance+24L,V25=row.Balance+25L,
            V26=row.Balance+26L,V27=row.Balance+27L,V28=row.Balance+28L,V29=row.Balance+29L,V30=row.Balance+30L,
            V31=row.Balance+31L
        }).OrderBy(static item=>item.Id).Select(static item=>item.V1+item.V2+item.V3+item.V4+item.V5+item.V6+item.V7+item.V8+item.V9+item.V10+item.V11+item.V12+item.V13+item.V14+item.V15+item.V16+item.V17+item.V18+item.V19+item.V20+item.V21+item.V22+item.V23+item.V24+item.V25+item.V26+item.V27+item.V28+item.V29+item.V30+item.V31).ToArray();
    private static IGrouping<int,Row>[] LinqGroupShape(IEnumerable<Row> rows,int mask,int hot)=>rows
        .GroupBy(row=>row.Id<hot?0:row.Id&mask)
        .OrderByDescending(static group=>group.Count()).OrderBy(static group=>group.Key,IntComparer()).ToArray();
    [LinqIntercept(true)]
    internal static long[] ArrayCarrier(int[] rows)=>rows.Select(static id=>new{Id=id,Value=id*257L})
        .OrderBy(static item=>item.Id).Select(static item=>item.Value).ToArray();
    [LinqIntercept(true)]
    internal static long[] ListCarrier(List<int> rows)=>rows.Select(static id=>new{Id=id,Value=id*257L})
        .OrderBy(static item=>item.Id).Select(static item=>item.Value).ToArray();
    [LinqIntercept(true)]
    internal static long[] TsavoriteCarrier(global::Seamless.Collections.Tsavorite.Set<int,Row> rows)=>rows
        .Select(static row=>new{row.Id,Value=row.Id*257L}).OrderBy(static item=>item.Id).Select(static item=>item.Value).ToArray();
    [LinqIntercept(true)]
    internal static long[] PersistedCarrier(global::Seamless.Collections.Persisted.Set<int> rows)=>rows
        .Select(static id=>new{Id=id,Value=id*257L}).OrderBy(static item=>item.Id).Select(static item=>item.Value).ToArray();
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
