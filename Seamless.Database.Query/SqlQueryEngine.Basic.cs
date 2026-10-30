namespace Seamless.Database.Query;

public sealed partial class SqlQueryEngine{
    /// <summary>OSSパーサーで基本SQLを準備する。現在の実行意味論はSQL Serverだけをサポートする。</summary>
    public SqlQueryPlan PrepareBasic(string sql,string database,SqlDialect dialect,IReadOnlyList<SqlParameter>?parameters=null){
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);ArgumentException.ThrowIfNullOrWhiteSpace(database);
        if(dialect!=SqlDialect.SqlServer){throw new SqlQueryException("Execution semantics for "+dialect+" are not implemented in the basic SQL path.",40515);}
        parameters??=[];
        if(sql.Length>1048576||parameters.Count>2100){throw new SqlQueryException("SQL text or parameter count exceeds the query limits.",103);}
        if(parameters.Any(parameter=>!parameter.Name.StartsWith("@",StringComparison.Ordinal))||parameters.Select(parameter=>parameter.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=parameters.Count){throw new SqlQueryException("Invalid or duplicate SQL parameter name.",137);}
        parameters=parameters.Select(parameter=>{
            var type=parameter.Type;
            if(type.ClrType==typeof(string)&&type.Name!="xml"){type=type with{Collation=type.Collation??this.catalog.GetDatabaseCollation(database),Coercibility=SqlCollationCoercibility.CoercibleDefault};}
            return parameter with{Type=type};
        }).ToArray();
        var key=new CacheKey(this.catalog.GetGeneration(database),database,sql,string.Join(";",parameters.Select(parameter=>parameter.Name+":"+parameter.Type+":"+parameter.IsNullable)),"OssSqlServerBasicV1");
        if(this.catalog.CanCachePlans){
            SqlQueryPlan?cached;lock(this.gate){this.cache.TryGetValue(key,out cached);}
            if(cached is not null){if(cached.IsCurrent){return cached;}lock(this.gate){this.cache.Clear();this.cacheOrder.Clear();}}
        }
        var snapshot=new SqlCatalogSnapshot(this.catalog,database,key.Generation);
        var document=new SqlParserCsAdapter().Parse(sql,dialect);
        if(document.Statements.Count!=1||document.Statements[0] is not SqlSelectStatementSyntax select){throw new SqlQueryException("Exactly one SELECT statement is required in the basic SQL path.",102);}
        var bound=new QueryBinder(snapshot,database,parameters).BindBasic(select.Query);
        if(snapshot.Generation!=key.Generation){throw new SqlQueryException("Catalog generation changed while preparing the SQL statement.",3961);}
        var plan=new SqlQueryPlan(bound,parameters.ToArray(),snapshot,key.Generation);
        if(this.cacheCapacity>0&&this.catalog.CanCachePlans){
            lock(this.gate){
                if(!this.cache.ContainsKey(key)){
                    while(this.cache.Count>=this.cacheCapacity){this.cache.Remove(this.cacheOrder.Dequeue());}
                    this.cache.Add(key,plan);this.cacheOrder.Enqueue(key);
                }
            }
        }
        return plan;
    }
}
