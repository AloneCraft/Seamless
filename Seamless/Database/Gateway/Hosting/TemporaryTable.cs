using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Hosting;

// 接続ごとの一時表(#名前)。tempdb 相当で、データベースが読み取り専用でも作れる。
internal sealed class TemporaryTable(IReadOnlyList<SqlColumn> columns){
    public IReadOnlyList<SqlColumn> Columns{get;}=columns;
    public List<object?[]> Rows{get;}=[];
}
