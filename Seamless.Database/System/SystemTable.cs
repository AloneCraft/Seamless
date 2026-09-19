namespace Seamless.Database.System;

public sealed class SystemTable<TKey,TRecord>:global::System.Collections.Generic.IReadOnlyCollection<TRecord>
    where TKey:struct
    where TRecord:struct {
    private readonly TRecord[] rows;
    private readonly global::System.Collections.Generic.Dictionary<TKey,TRecord> records;
    private readonly global::System.Collections.Generic.IReadOnlyCollection<TKey> keys;

    internal SystemTable(
        global::System.Collections.Generic.IEnumerable<TRecord> rows,
        global::System.Func<TRecord,TKey> keySelector) {
        global::System.ArgumentNullException.ThrowIfNull(rows);
        global::System.ArgumentNullException.ThrowIfNull(keySelector);

        this.rows=new global::System.Collections.Generic.List<TRecord>(rows).ToArray();
        this.records=new(this.rows.Length);
        var keyValues = new TKey[this.rows.Length];
        for(var index=0;index<this.rows.Length;index++) {
            var row = this.rows[index];
            var key = keySelector(row);
            if(!this.records.TryAdd(key,row)) {
                throw new global::System.ArgumentException("The system table contains a duplicate key.",nameof(rows));
            }
            keyValues[index]=key;
        }
        this.keys=global::System.Array.AsReadOnly(keyValues);
    }

    public int Count=>this.rows.Length;
    public global::System.Collections.Generic.IReadOnlyCollection<TKey> Keys=>this.keys;
    public TRecord this[TKey key]=>this.records[key];
    public bool ContainsKey(TKey key)=>this.records.ContainsKey(key);
    public bool TryGetValue(TKey key,out TRecord record)=>this.records.TryGetValue(key,out record);
    public global::System.Collections.Generic.IEnumerator<TRecord> GetEnumerator()=>
        ((global::System.Collections.Generic.IEnumerable<TRecord>)this.rows).GetEnumerator();
    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=>this.GetEnumerator();
}
