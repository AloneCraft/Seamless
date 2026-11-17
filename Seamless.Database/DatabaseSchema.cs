namespace Seamless.Database;

public abstract class DatabaseSchema {
    private readonly DatabaseContainer database;

    protected DatabaseSchema(DatabaseContainer database,string name) {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        this.database=database;
        this.Name=name;
    }

    public string Name { get; }
    protected DatabaseContainer Database=>this.database;

    protected StoredTable<TRecord> Table<TRecord>([CallerMemberName] string name="")
        where TRecord:notnull=>
        this.database.GetTable<TRecord>($"{this.Name}.{name}");

    protected StoredTable<TKey,TRecord> Table<TKey,TRecord>(
        Func<TRecord,TKey> keySelector,
        [CallerMemberName] string name="")
        where TKey:struct
        where TRecord:struct=>this.database.GetTable(keySelector,$"{this.Name}.{name}");

    protected MaterializedView<TKey,TRecord> MaterializedView<TKey,TRecord>(
        Func<TRecord,TKey> keySelector,
        [CallerMemberName] string name="")
        where TKey:struct
        where TRecord:struct=>this.database.GetMaterializedView(keySelector,$"{this.Name}.{name}");

    protected StoredProcedure<TResult> StoredProcedure<TResult>(
        Func<TResult> definition,
        [CallerMemberName] string name="")=>
        this.database.CreateStoredProcedure(definition,$"{this.Name}.{name}");
}
