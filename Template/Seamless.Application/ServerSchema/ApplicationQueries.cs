using Seamless.Application.Schema;
using Tables=Seamless.Application.Schema.Tables;

namespace Seamless.Application.ServerSchema;

[Seamless.FLinq.LinqIntercept(true)]
public sealed class ApplicationQueries:IApplicationQueries{
    private readonly ApplicationDatabase database;
    public ApplicationQueries(ApplicationDatabase database)=>this.database=database;
    public IEnumerable<Tables.Item> Read(long minimumId)=>this.database.Items.Where(row=>row.Id>=minimumId);
    public long Aggregate(long minimumId){
        var rows=this.database.Items.Where(row=>row.Id>=minimumId);
        return rows.Count()*100+rows.Sum(row=>row.Id);
    }
    public int Insert(long id){
        this.database.Items.Add(new Tables.Item(new(id),1,2));
        return this.database.Items.Count();
    }
    public int Fail(long id){
        this.database.Items.Add(new Tables.Item(new(id),1,2));
        throw new InvalidOperationException("rollback");
    }
}
