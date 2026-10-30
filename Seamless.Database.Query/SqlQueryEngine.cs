using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Execution;
namespace Seamless.Database.Query;
/// <summary>カタログ世代と引数型で分離した有界計画キャッシュ。</summary>
public sealed partial class SqlQueryEngine{
    private readonly ISqlCatalog catalog;
    private readonly int cacheCapacity;
    private readonly object gate=new();
    private readonly Dictionary<CacheKey,SqlQueryPlan>cache=[];
    private readonly Queue<CacheKey>cacheOrder=new();
    private sealed record CacheKey(long Generation,string Database,string Sql,string Parameters,string Mode="ScriptDom");
    public SqlQueryEngine(ISqlCatalog catalog,int cacheCapacity=128){ArgumentNullException.ThrowIfNull(catalog);ArgumentOutOfRangeException.ThrowIfNegative(cacheCapacity);this.catalog=catalog;this.cacheCapacity=cacheCapacity;}
    public SqlQueryPlan Prepare(string sql,string database,IReadOnlyList<SqlParameter>? parameters=null){
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);ArgumentException.ThrowIfNullOrWhiteSpace(database);
        parameters??=[];
        parameters=parameters.Select(parameter=>{var type=parameter.Type;if(type.ClrType==typeof(string)&&type.Name!="xml"){type=type with{Collation=type.Collation??this.catalog.GetDatabaseCollation(database),Coercibility=SqlCollationCoercibility.CoercibleDefault};}return parameter with{Type=type};}).ToArray();
        if(sql.Length>1048576||parameters.Count>2100){throw new SqlQueryException("SQL text or parameter count exceeds the query limits.",103);}
        var key=new CacheKey(this.catalog.GetGeneration(database),database,sql,string.Join(";",parameters.Select(x=>x.Name+":"+x.Type+":"+x.IsNullable)));
        if(parameters.Any(x=>!x.Name.StartsWith("@",StringComparison.Ordinal))||parameters.Select(x=>x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=parameters.Count){throw new SqlQueryException("Invalid or duplicate SQL parameter name.",137);}
        if(this.catalog.CanCachePlans){
            SqlQueryPlan?cached;lock(this.gate){this.cache.TryGetValue(key,out cached);}
            if(cached is not null){if(cached.IsCurrent){return cached;}lock(this.gate){this.cache.Clear();this.cacheOrder.Clear();}}
        }
        var snapshot=new SqlCatalogSnapshot(this.catalog,database,key.Generation);
        var statement=Parse(sql);
        var binder=new QueryBinder(snapshot,database,parameters);
        var bound=binder.BindStatement(statement);
        if(snapshot.Generation!=key.Generation){throw new SqlQueryException("Catalog generation changed while preparing the SQL statement.",3961);}
        var plan=new SqlQueryPlan(bound,parameters.ToArray(),snapshot,key.Generation);
        if(this.cacheCapacity>0&&this.catalog.CanCachePlans){lock(this.gate){if(!this.cache.ContainsKey(key)){while(this.cache.Count>=this.cacheCapacity){this.cache.Remove(this.cacheOrder.Dequeue());}this.cache.Add(key,plan);this.cacheOrder.Enqueue(key);}}}
        return plan;
    }
    internal static TSqlStatement Parse(string sql){
        var parser=new TSql170Parser(true);
        var fragment=parser.Parse(new StringReader(sql),out var errors);
        if(errors.Count>0){throw new SqlQueryException(string.Join("; ",errors.Select(x=>x.Message)),102,errors[0].Line);}
        if(fragment is not TSqlScript script||script.Batches.Count!=1||script.Batches[0].Statements.Count!=1){throw new SqlQueryException("Exactly one SQL statement is required.",102);}
        return script.Batches[0].Statements[0];
    }
}
public sealed class SqlQueryPlan{
    private readonly BoundQuery query;
    private readonly SqlParameter[]parameters;
    private readonly ISqlCatalog catalog;
    private readonly long generation;
    internal SqlQueryPlan(BoundQuery query,SqlParameter[]parameters,ISqlCatalog catalog,long generation){this.query=query;this.parameters=parameters;this.catalog=catalog;this.generation=generation;this.Columns=Array.AsReadOnly(query.Columns.ToArray());}
    public IReadOnlyList<SqlColumn>Columns{get;}
    public SqlQueryResult Execute(IReadOnlyDictionary<string,object?>? parameters=null,ExecutionOptions? options=null){
        this.VerifyGeneration();
        var values=new Dictionary<string,object?>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string,object?> supplied=new(StringComparer.OrdinalIgnoreCase);if(parameters is not null){foreach(var entry in parameters){supplied.Add(entry.Key,entry.Value);}}
        foreach(var parameter in this.parameters){
            object? value=null;
            if(!supplied.TryGetValue(parameter.Name,out value)){throw new SqlQueryException("Missing parameter "+parameter.Name,137);}
            if(value is DBNull){value=null;}
            if(value is double real&&!double.IsFinite(real)||value is float single&&!float.IsFinite(single)){throw new SqlQueryException("SQL parameters cannot contain NaN or infinity.",8114);}
            if(value is null&&!parameter.IsNullable){throw new SqlQueryException("Parameter cannot be null: "+parameter.Name,515);}
            values.Add(parameter.Name,SqlValues.ConvertTo(value,parameter.Type));
        }
        return new(this.Columns,this.Enumerate(values,options??new()));
    }
    private IEnumerable<object?[]>Enumerate(IReadOnlyDictionary<string,object?> parameters,ExecutionOptions options){
        this.VerifyGeneration();
        using var scope=new ExecutionScope(options);
        scope.Codecs.Register<object?[]>(new SqlRowCodec());
        var context=new QueryExecution(scope,parameters,options);
        using var iterator=this.query.Rows(context,null).GetEnumerator();
        while(true){this.VerifyGeneration();bool next;try{next=iterator.MoveNext();}catch(DivideByZeroException){throw new SqlQueryException("Divide by zero error encountered.",8134);}catch(OverflowException exception){throw new SqlQueryException("Arithmetic overflow: "+exception.Message,8115);}catch(FormatException exception){throw new SqlQueryException("Conversion failed: "+exception.Message,245);}this.VerifyGeneration();if(!next){yield break;}scope.ThrowIfUnavailable();var row=iterator.Current;using var memory=scope.Budget.ReserveMemory(checked(96L+32L*row.Length+4L*new SqlRowCodec().GetEncodedLength(row)));yield return row;}
    }
    internal bool IsCurrent=>this.catalog.Generation==this.generation;
    private void VerifyGeneration(){if(!this.IsCurrent){throw new SqlQueryException("Catalog generation changed; prepare the SQL statement again.",3961);}}
}
internal sealed record BoundQuery(IReadOnlyList<SqlColumn>Columns,Func<QueryExecution,object?[]?,IEnumerable<object?[]>>Rows);
internal sealed record QueryExecution(ExecutionScope Scope,IReadOnlyDictionary<string,object?>Parameters,ExecutionOptions?Options=null){internal DateTime Timestamp{get;}=DateTime.Now;}
internal sealed record BindingColumn(SqlColumn Column,string? Qualifier);
internal sealed record BoundSource(IReadOnlyList<BindingColumn>Columns,Func<QueryExecution,object?[]?,IEnumerable<object?[]>>Rows);
internal sealed record EvalFrame(object?[]Row,object?[]?Outer,QueryExecution Execution,object?[]?Aggregates=null);
internal sealed record BoundScalar(SqlType Type,bool Nullable,Func<EvalFrame,object?>Value,SqlColumn? Origin=null){
    internal bool IsComputedFloatingPoint{get;init;}=Origin?.IsComputedFloatingPoint==true;
}
internal sealed record AggregateBinding(string Name,BoundScalar? Value,bool Distinct,SqlType Type);


