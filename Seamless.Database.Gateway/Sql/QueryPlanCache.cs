using System.Collections.Concurrent;
using Seamless.Database.Gateway.Hosting;

namespace Seamless.Database.Gateway.Sql;

public sealed class QueryPlanCache{
    private readonly ConcurrentDictionary<string,ParsedCommand>cache=new(StringComparer.Ordinal);
    private readonly IDatabaseAdapter adapter;

    public QueryPlanCache(IDatabaseAdapter adapter){
        this.adapter=adapter;
    }

    public ParsedCommand GetOrParse(string sql){
        var trimmed=sql.Trim();
        if(this.cache.TryGetValue(trimmed,out var plan)){
            return plan;
        }else{
        }

        var parsed=ScriptDomParser.Parse(trimmed,this.adapter);
        this.cache.TryAdd(trimmed,parsed);
        return parsed;
    }

    public void Clear(){
        this.cache.Clear();
    }
}
