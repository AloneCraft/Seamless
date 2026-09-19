namespace Seamless.Database;

/// <summary>既存データベースからテーブル行を取り込むための拡張点。</summary>
public interface IDatabaseImportSource {
    IEnumerable<TRecord> ReadTable<TRecord>(global::Seamless.Database.System.Keys.DatabaseObject key);
}
