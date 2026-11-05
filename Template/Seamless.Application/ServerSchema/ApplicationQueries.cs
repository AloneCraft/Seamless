using Seamless.Application.Schema;
using Records=Seamless.Application.Schema.Records;

namespace Seamless.Application.ServerSchema;

[Seamless.FLinq.LinqIntercept(true)]
public sealed class ApplicationQueries:IApplicationQueries{
    private readonly ApplicationDatabase database;
    public ApplicationQueries(ApplicationDatabase database)=>this.database=database;
    public IEnumerable<Records.Item> Read(long minimumId)=>this.database.Items.Where(row=>row.Id>=minimumId);
}
