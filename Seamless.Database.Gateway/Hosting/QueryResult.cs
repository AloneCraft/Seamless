namespace Seamless.Database.Gateway.Hosting;

public sealed class QueryResult{
    private readonly Lazy<QueryResult>? deferred;
    private readonly IReadOnlyList<ColumnMetadata>? columns;
    private readonly IEnumerable<object?[]>? rows;
    private readonly int affected;
    private readonly string? information;
    public QueryResult(IReadOnlyList<ColumnMetadata>columns,IEnumerable<object?[]>rows,int rowsAffected){this.columns=columns;this.rows=rows;this.affected=rowsAffected;}
    private QueryResult(Func<QueryResult> execute){this.deferred=new(execute);}
    private QueryResult(string information):this(Array.Empty<ColumnMetadata>(),Array.Empty<object?[]>(),0){this.information=information;}
    public static QueryResult Message(string information){ArgumentNullException.ThrowIfNull(information);return new(information);}
    public static QueryResult Defer(Func<QueryResult> execute)=>new(execute);
    public static QueryResult Empty=>new(Array.Empty<ColumnMetadata>(),Array.Empty<object?[]>(),0);
    public static QueryResult NonQuery(int rowsAffected)=>new(Array.Empty<ColumnMetadata>(),Array.Empty<object?[]>(),rowsAffected);
    public string?Information=>this.deferred is null?this.information:this.deferred.Value.Information;
    public IReadOnlyList<ColumnMetadata>Columns=>this.deferred?.Value.Columns??this.columns!;
    public IEnumerable<object?[]>Rows=>this.deferred?.Value.Rows??this.rows!;
    public int RowsAffected=>this.deferred?.Value.RowsAffected??this.affected;
}
