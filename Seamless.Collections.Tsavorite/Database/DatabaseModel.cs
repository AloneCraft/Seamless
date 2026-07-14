namespace Seamless.Database;

/// <summary>宣言コンテナが公開するデータベースオブジェクトの種別。</summary>
public enum DatabaseObjectKind {
    Table,
    View,
    TableFunction,
    ScalarFunction,
}

/// <summary>source generator が宣言メンバーから生成する静的メタデータ。SQL 名の解決と将来のバックアップ取り込みに使う。</summary>
public sealed record DatabaseObjectDeclaration(DatabaseObjectKind Kind,string MemberName,string SqlName,string RecordTypeName);

/// <summary>
/// 既存データベース(例: AdventureWorks2008R2 の本物バックアップ)からテーブル行を取り込むための拡張点。
/// 現時点ではリポジトリ内データのみで動作し、外部 SQL Server やネットワークには依存しない。
/// </summary>
public interface IDatabaseImportSource {
    /// <summary>SQL 互換名(例: "Sales.Customer")のテーブル行を列挙する。未知のテーブルは空列挙を返す。</summary>
    IEnumerable<TRecord> ReadTable<TRecord>(string sqlName);
}
