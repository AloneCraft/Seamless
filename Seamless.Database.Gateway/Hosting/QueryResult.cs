namespace Seamless.Database.Gateway.Hosting;

public sealed class QueryResult{
    public QueryResult(IReadOnlyList<ColumnMetadata>columns,IReadOnlyList<object?[]>rows,int rowsAffected){
        this.Columns=columns;
        this.Rows=rows;
        this.RowsAffected=rowsAffected;
    }

    public static QueryResult Empty=>new(Array.Empty<ColumnMetadata>(),Array.Empty<object?[]>(),0);

    public static QueryResult NonQuery(int rowsAffected)=>new(Array.Empty<ColumnMetadata>(),Array.Empty<object?[]>(),rowsAffected);

    public IReadOnlyList<ColumnMetadata>Columns{get;}
    public IReadOnlyList<object?[]>Rows{get;}
    public int RowsAffected{get;}
}
