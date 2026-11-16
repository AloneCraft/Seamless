using Seamless.Database;
using Seamless.Application.Schema;
using Keys=Seamless.Application.Schema.Keys;
using Tables=Seamless.Application.Schema.Tables;

namespace Seamless.Application.ServerSchema;

internal static class DatabaseGenerator{
    public const string ClientContract="Seamless.Application.Schema.IApplicationQueries";
    public const string ClientModule="application";
    public const string ClientDatabase="application";
    public const string ServerImplementation="Seamless.Application.ServerSchema.ApplicationQueries";
}

public sealed partial class ApplicationDatabase(DatabaseContainerOptions options):DatabaseContainer(options){
    public partial Table<Keys.Item,Tables.Item> Items{get;set;}
}
