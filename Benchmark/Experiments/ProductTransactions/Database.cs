using Seamless.Database;
using Seamless.FLinq;
[assembly:LinqIntercept(false)]
namespace ProductTransactions;

public readonly record struct Row(int Id,long Balance):global::Seamless.Collections.IRecord<int>{
    int global::Seamless.Collections.IRecord<int>.Key{
        get=>this.Id;
        init=>this.Id=value;
    }
}
internal static partial class DatabaseGenerator;
public sealed partial class Database(DatabaseContainerOptions options):DatabaseContainer(options){
    public Table<int,Row> Accounts=>this.Table<int,Row>(static row=>row.Id);
}
public sealed class Queries{
    [LinqIntercept(true)]
    public static long Total(Database.TypedTransaction tx)=>tx.Accounts.Rows.Where(static row=>row.Id%2==0).Sum(static row=>row.Balance);
    [LinqIntercept(true)]
    public static long Count(Database.TypedTransaction tx)=>tx.Accounts.Rows.LongCount();
    [LinqIntercept(true)]
    public static (int Key,long Count,long Total)[] Groups(Database.TypedTransaction tx)=>tx.Accounts.Rows.GroupBy(static row=>row.Id%64).Select(static group=>(group.Key,group.LongCount(),group.Sum(static row=>row.Balance))).ToArray();
    [LinqIntercept(true)]
    public static (int Left,int Right)[] Join(Database.TypedTransaction tx)=>tx.Accounts.Rows.Where(static row=>row.Id<64).Join(tx.Accounts.Rows,static row=>row.Id,static row=>row.Id%64,static(left,right)=>(left.Id,right.Id)).ToArray();
}
