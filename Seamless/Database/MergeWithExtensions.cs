using Seamless.Collections.Tsavorite;
using Tsavorite.core;
using Seamless.Database;

namespace Seamless.Linq;

public static class SetMergeWithExtensions {
    /// <summary>
    /// 複数入力と照合し、存在すれば更新、存在しなければ作成を一括実行する。
    /// 照合・追加・更新の判定は呼び出し開始時の状態で固定され、循環キー置換を許容し、呼出単位の原子性を保証する。
    /// </summary>
    public static (long Updated, long Changed, long Added) MergeWith<TKey, TRecord, TInner, TMatchKey>(
        this Table<TKey, TRecord> outer,
        IEnumerable<TInner> inner,
        Func<TRecord, TMatchKey> outerKeySelector,
        Func<TInner, TMatchKey> innerKeySelector,
        Func<TMatchKey, TInner, TRecord> create,
        Func<TRecord, TInner, TRecord> update)
        where TKey : struct
        where TRecord : struct
        where TMatchKey : notnull {
        ArgumentNullException.ThrowIfNull(outer);
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(outerKeySelector);
        ArgumentNullException.ThrowIfNull(innerKeySelector);
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(update);
        // [暗黙の取引] db 側の表を取引の外で使うときは、照合から反映までを1つの暗黙の取引で行う。
        if(outer is StoredTable<TKey,TRecord> stored){return stored.Database.InImplicitTransaction(()=>MergeWithCore(outer,inner,outerKeySelector,innerKeySelector,create,update));}
        return MergeWithCore(outer,inner,outerKeySelector,innerKeySelector,create,update);
    }

    private static (long Updated, long Changed, long Added) MergeWithCore<TKey, TRecord, TInner, TMatchKey>(
        Table<TKey, TRecord> outer,IEnumerable<TInner> inner,Func<TRecord, TMatchKey> outerKeySelector,Func<TInner, TMatchKey> innerKeySelector,
        Func<TMatchKey, TInner, TRecord> create,Func<TRecord, TInner, TRecord> update)
        where TKey : struct where TRecord : struct where TMatchKey : notnull {

        // 1. 開始時可視状態のスナップショット取得
        // outer 内で同一 matchKey に同一レコードがある場合は重複除去、異なるレコードがある場合はエラー
        var outerSnapshot = new Dictionary<TMatchKey, TRecord>();
        var outerKeys = new Dictionary<TMatchKey, TKey>();
        var untouchedOriginalKeys = new global::System.Collections.Generic.HashSet<TKey>();

        foreach (var row in outer) {
            var mKey = outerKeySelector(row);
            var rowKey = outer.GetKey(row);
            untouchedOriginalKeys.Add(rowKey);

            if (outerSnapshot.TryGetValue(mKey, out var existing)) {
                if (!EqualityComparer<TRecord>.Default.Equals(existing, row)) {
                    throw new InvalidOperationException($"Duplicate matching key '{mKey}' with different records found in outer table.");
                }
            } else {
                outerSnapshot[mKey] = row;
                outerKeys[mKey] = rowKey;
            }
        }

        // 2. inner の重複除去
        // 同一 matchKey に同一レコードは重複除去、異なるレコードはエラー
        var innerMap = new Dictionary<TMatchKey, TInner>();
        foreach (var item in inner) {
            var mKey = innerKeySelector(item);
            if (innerMap.TryGetValue(mKey, out var existingItem)) {
                if (!EqualityComparer<TInner>.Default.Equals(existingItem, item)) {
                    throw new InvalidOperationException($"Duplicate matching key '{mKey}' with different input records found in inner sequence.");
                }
            } else {
                innerMap[mKey] = item;
            }
        }

        // 3. 照合・計算・計画作成
        long updatedCount = 0;
        long changedCount = 0;
        long addedCount = 0;

        var plannedUpdates = new List<(TKey OldKey, TKey NewKey, TRecord NewRecord)>();
        var plannedAdds = new List<(TKey NewKey, TRecord NewRecord)>();
        var modifiedOldKeys = new global::System.Collections.Generic.HashSet<TKey>();

        foreach (var (mKey, input) in innerMap) {
            if (outerSnapshot.TryGetValue(mKey, out var existingRecord)) {
                // Update
                var oldKey = outerKeys[mKey];
                var nextRecord = update(existingRecord, input);
                var newKey = outer.GetKey(nextRecord);

                updatedCount++;
                var isChanged = !EqualityComparer<TRecord>.Default.Equals(existingRecord, nextRecord);
                if (isChanged || !KeysEqual(outer,oldKey, newKey)) {
                    changedCount++;
                }

                plannedUpdates.Add((oldKey, newKey, nextRecord));
                modifiedOldKeys.Add(oldKey);
                untouchedOriginalKeys.Remove(oldKey);
            } else {
                // Create
                var createdRecord = create(mKey, input);
                var newKey = outer.GetKey(createdRecord);

                addedCount++;
                plannedAdds.Add((newKey, createdRecord));
            }
        }

        // 4. 最終主キーの一意性検査（変更対象外の既存行 + 更新/追加後の新キー）
        var finalKeySet = new global::System.Collections.Generic.HashSet<TKey>(untouchedOriginalKeys);
        foreach (var (oldKey, newKey, _) in plannedUpdates) {
            if (!finalKeySet.Add(newKey)) {
                throw new InvalidOperationException($"Destination key conflict during MergeWith: key '{newKey}' already exists or is duplicated.");
            }
        }
        foreach (var (newKey, _) in plannedAdds) {
            if (!finalKeySet.Add(newKey)) {
                throw new InvalidOperationException($"Destination key conflict during MergeWith: newly added key '{newKey}' already exists or is duplicated.");
            }
        }

        // 5. 原子的反映
        // 一時キー削除を先行して行うことで、循環キー置換（1 -> 2, 2 -> 1 等）を安全に実行
        foreach (var (oldKey, _, _) in plannedUpdates) {
            outer.Remove(oldKey);
        }

        foreach (var (_, newKey, newRecord) in plannedUpdates) {
            outer[newKey] = newRecord;
        }

        foreach (var (newKey, newRecord) in plannedAdds) {
            outer[newKey] = newRecord;
        }

        return (updatedCount, changedCount, addedCount);
    }

    private static bool KeysEqual<TKey,TRecord>(Table<TKey,TRecord> table,TKey first,TKey second) where TKey:struct where TRecord:struct=>
        table is StoredTable<TKey,TRecord> stored?stored.KeysEqual(first,second):EqualityComparer<TKey>.Default.Equals(first,second);
}
