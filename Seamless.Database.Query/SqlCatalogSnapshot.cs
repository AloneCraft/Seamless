namespace Seamless.Database.Query;

/// <summary>計画が実際に参照したDBの世代だけを保持する。セッションや取引を保持しない。</summary>
internal sealed class SqlCatalogSnapshot:ISqlCatalog{
    private readonly ISqlCatalog source;
    private readonly long generation;
    private readonly Dictionary<string,long>dependencies=new(StringComparer.OrdinalIgnoreCase);
    internal SqlCatalogSnapshot(ISqlCatalog source,string database,long generation){this.source=source;this.generation=generation;this.dependencies.Add(database,generation);}
    public long Generation{
        get{
            foreach(var entry in this.dependencies){if(this.source.GetGeneration(entry.Key)!=entry.Value){return this.generation^long.MinValue;}}
            return this.generation;
        }
    }
    public long GetGeneration(string database)=>this.source.GetGeneration(database);
    public bool CanCachePlans=>this.source.CanCachePlans;
    public SqlTable?FindTable(SqlObjectName name){this.Capture(name.Database);return this.source.FindTable(name);}
    public SqlRoutine?FindRoutine(SqlObjectName name){this.Capture(name.Database);return this.source.FindRoutine(name);}
    public string?GetDatabaseCollation(string database){this.Capture(database);return this.source.GetDatabaseCollation(database);}
    private void Capture(string database){if(!this.dependencies.ContainsKey(database)){this.dependencies.Add(database,this.source.GetGeneration(database));}}
}
