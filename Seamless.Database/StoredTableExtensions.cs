using System.ComponentModel;
namespace Seamless.Database;

/// <summary>
/// 生成コード(FLinq・Gateway)が保存された表から行を読む入口。表のプロパティの型が基底の Table になったので(関係代入)、
/// 基底の型から呼べるようにする。中身がこのDBの保存された表でなければ例外。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class StoredTableExtensions{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static DatabaseRows<TKey,TRecord> GetQueryRows<TKey,TRecord>(this Table<TKey,TRecord> table) where TKey:struct where TRecord:struct=>
        Stored(table).GetQueryRows();

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static global::Seamless.Transactions.Collections.TransactionRows<TKey,TRecord> GetGatewayTransactionRows<TKey,TRecord>(this Table<TKey,TRecord> table) where TKey:struct where TRecord:struct=>
        Stored(table).GetGatewayTransactionRows();

    private static StoredTable<TKey,TRecord> Stored<TKey,TRecord>(Table<TKey,TRecord> table) where TKey:struct where TRecord:struct=>
        table as StoredTable<TKey,TRecord>??throw new InvalidOperationException("Only a stored table of a database has stored rows.");
}
