using Seamless.Database;
using Seamless.Application.Schema;
using Keys=Seamless.Application.Schema.Keys;
using Records=Seamless.Application.Schema.Records;

namespace Seamless.Application.ServerSchema;

internal static class DatabaseGenerator;

public sealed partial class ApplicationDatabase(DatabaseContainerOptions options):DatabaseContainer(options){
    public Table<Keys.Item,Records.Item>Items=>this.Table<Keys.Item,Records.Item>(static row=>new(row.Id));
}
