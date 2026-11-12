using Seamless.Application.Schema;
using Records=Seamless.Application.Schema.Records;

namespace Seamless.Application.ServerSchema;

[Seamless.FLinq.LinqIntercept(true)]
public sealed class ApplicationQueries:IApplicationQueries{
    private readonly ApplicationDatabase database;
    public ApplicationQueries(ApplicationDatabase database)=>this.database=database;
    public IEnumerable<Records.Item> Read(long minimumId)=>this.database.Items.Where(row=>row.Id>=minimumId);
    public long Aggregate(long minimumId){
        var rows=this.database.Items.Where(row=>row.Id>=minimumId);
        return rows.Count()*100+rows.Sum(row=>row.Id);
    }
    public int Insert(long id){
        this.database.Items.Add(new Records.Item(1,id,2));
        return this.database.Items.Count();
    }
    public int Fail(long id){
        this.database.Items.Add(new Records.Item(1,id,2));
        throw new InvalidOperationException("rollback");
    }
}
