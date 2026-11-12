namespace Seamless.Database.Query;

/// <summary>計画が実際に参照したDBの世代だけを保持する。セッションや取引を保持しない。</summary>
internal sealed class SqlCatalogSnapshot:ISqlCatalog{
    private readonly ISqlCatalog source;
    private readonly long generation;
    private readonly Dictionary<string,long>dependencies=new(StringComparer.OrdinalIgnoreCase);
    internal SqlCatalogSnapshot(ISqlCatalog source,string database,long generation){this.source=source;this.generation=generation;}
    public long Generation{
        get{
            if(this.source.Generation!=this.generation){return this.generation^long.MinValue;}
            foreach(var entry in this.dependencies){if(this.source.GetGeneration(entry.Key)!=entry.Value){return this.generation^long.MinValue;}}
            return this.generation;
        }
    }
    public long GetGeneration(string database)=>this.source.GetGeneration(database);
    public bool CanCachePlans=>this.source.CanCachePlans;
    public void TrackPlan(SqlQueryPlan plan)=>this.source.TrackPlan(plan);
    public IDisposable? AcquireRead(IReadOnlyCollection<string> databases,CancellationToken cancellationToken=default)=>this.source.AcquireRead(this.dependencies.Keys,cancellationToken);
    public SqlTable?FindTable(SqlObjectName name){this.Capture(name.Database);return this.source.FindTable(name);}
    public SqlRoutine?FindRoutine(SqlObjectName name){this.Capture(name.Database);return this.source.FindRoutine(name);}
    public IDisposable? EnterDefinition(SqlObjectName name,SqlDefinitionKind kind)=>this.source.EnterDefinition(name,kind);
    public string?GetDatabaseCollation(string database){this.Capture(database);return this.source.GetDatabaseCollation(database);}
    private void Capture(string database){if(!this.dependencies.ContainsKey(database)){this.dependencies.Add(database,this.source.GetGeneration(database));}}
}
