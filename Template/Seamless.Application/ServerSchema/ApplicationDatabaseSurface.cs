using Seamless.Database.Client;
using Seamless.Database.Gateway.Native;
using Seamless.Application.Schema;

namespace Seamless.Application.ServerSchema;

public sealed class ApplicationDatabaseSurface:IApplicationDatabase{
    public ApplicationDatabaseSurface(ApplicationDatabase database)=>this.Items=new NativeTableAdapter<Schema.Keys.Item,Schema.Records.Item>(database.Items);
    public NativeTable<Schema.Keys.Item,Schema.Records.Item> Items{get;}
}
