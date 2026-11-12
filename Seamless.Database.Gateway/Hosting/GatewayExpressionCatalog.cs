using Seamless.Database.Query;
using Seamless.Database.Rdbms;

namespace Seamless.Database.Gateway.Hosting;

/// <summary>生成前の型推論と生成世代の式評価で同じ SQL 定義を参照する。</summary>
internal sealed class GatewayExpressionCatalog(DatabaseCatalog definition,ISqlCatalog? fallback=null,InProcessDatabaseAdapter? adapter=null):ISqlCatalog{
    public long Generation=>0;
    public bool CanCachePlans=>false;
    public void TrackPlan(SqlQueryPlan plan)=>fallback?.TrackPlan(plan);
    public long GetGeneration(string database)=>database.Equals(definition.Name,StringComparison.OrdinalIgnoreCase)?0:fallback?.GetGeneration(database)??0;
    public IDisposable? AcquireRead(IReadOnlyCollection<string> databases,CancellationToken cancellationToken=default)=>fallback?.AcquireRead(databases,cancellationToken);
    public string? GetDatabaseCollation(string database)=>database.Equals(definition.Name,StringComparison.OrdinalIgnoreCase)?definition.DefaultCollation:fallback?.GetDatabaseCollation(database);
    public SqlRoutine? FindRoutine(SqlObjectName name){if(!name.Database.Equals(definition.Name,StringComparison.OrdinalIgnoreCase)){return fallback?.FindRoutine(name);}var routine=definition.Schemas.FirstOrDefault(schema=>schema.Name.Equals(name.Schema,StringComparison.OrdinalIgnoreCase))?.Routines.FirstOrDefault(routine=>routine.Name.Equals(name.Name,StringComparison.OrdinalIgnoreCase));return routine is null?fallback?.FindRoutine(name):new(name,routine.Definition);}
    public SqlTable? FindTable(SqlObjectName name){
        if(!name.Database.Equals(definition.Name,StringComparison.OrdinalIgnoreCase)){return fallback?.FindTable(name);}var table=definition.Schemas.FirstOrDefault(schema=>schema.Name.Equals(name.Schema,StringComparison.OrdinalIgnoreCase))?.Tables.FirstOrDefault(table=>table.Name.Equals(name.Name,StringComparison.OrdinalIgnoreCase));if(table is null){return fallback?.FindTable(name);}return new(name,table.Columns.Select(column=>new SqlColumn(column.Name,ComparisonHost.ToSqlType(column.Type),column.Type.IsNullable,name,column.Ordinal)).ToArray(),Rows);
        IEnumerable<object?[]> Rows(CancellationToken cancellationToken){if(adapter is null){yield break;}var session=DatabaseRegistry.CurrentSqlSession;using var owned=session is null?new SessionContext(0,definition.Name){CancellationToken=cancellationToken}:null;foreach(var row in adapter.ExecuteSelect(new TableMetadata(name.Schema,name.Name,[]).IdentifierKey,null,null,session??owned!).Rows){cancellationToken.ThrowIfCancellationRequested();yield return row;}}
    }
}
