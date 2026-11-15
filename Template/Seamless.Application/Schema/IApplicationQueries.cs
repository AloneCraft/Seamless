namespace Seamless.Application.Schema;

// 共有するのは公開契約。サーバー内部の処理本体はServerSchemaへ置く。
public interface IApplicationQueries{
    IEnumerable<Tables.Item> Read(long minimumId);
    long Aggregate(long minimumId);
    int Insert(long id);
    int Fail(long id);
}
