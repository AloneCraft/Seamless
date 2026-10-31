namespace Seamless.Database.SqlServer;

internal sealed class SqlServerCatalogMetadata {
    private readonly IReadOnlyDictionary<(string Schema,string Table),SqlServerTableMetadata> tables;

    public SqlServerCatalogMetadata(
        IReadOnlyDictionary<(string Schema,string Table),SqlServerTableMetadata> tables,
        IReadOnlyList<RoutineCatalog> routines) {
        this.tables=tables;
        this.Routines=routines;
    }

    public IReadOnlyList<RoutineCatalog> Routines { get; }

    public bool TryGetValue((string Schema,string Table) key,out SqlServerTableMetadata value)=>
        this.tables.TryGetValue(key,out value!);
}

internal sealed record SqlServerTableMetadata(
    IReadOnlyList<UniqueKeyCatalog> UniqueKeys,
    IReadOnlyList<ForeignKeyCatalog> ForeignKeys,
    IReadOnlyList<CheckConstraintCatalog> CheckConstraints,
    IReadOnlyList<TriggerCatalog> Triggers);

internal static class SqlServerMetadataReader {
    private const string UniqueKeyQuery="""
        SELECT schema_definition.name,table_definition.name,index_definition.name,
            index_column.key_ordinal,column_definition.name,index_column.is_descending_key
        FROM sys.tables AS table_definition
        INNER JOIN sys.schemas AS schema_definition ON schema_definition.schema_id=table_definition.schema_id
        INNER JOIN sys.indexes AS index_definition ON index_definition.object_id=table_definition.object_id
        INNER JOIN sys.index_columns AS index_column
            ON index_column.object_id=index_definition.object_id AND index_column.index_id=index_definition.index_id
            AND index_column.is_included_column=0
        INNER JOIN sys.columns AS column_definition
            ON column_definition.object_id=index_column.object_id AND column_definition.column_id=index_column.column_id
        WHERE table_definition.is_ms_shipped=0 AND index_definition.is_unique=1
            AND index_definition.is_primary_key=0 AND index_definition.is_hypothetical=0
            AND index_definition.is_disabled=0
        ORDER BY schema_definition.name,table_definition.name,index_definition.name,index_column.key_ordinal;
        """;

    private const string ForeignKeyQuery="""
        SELECT parent_schema.name,parent_table.name,foreign_key.name,
            referenced_schema.name,referenced_table.name,parent_column.name,referenced_column.name,
            foreign_key_column.constraint_column_id,foreign_key.delete_referential_action_desc,
            foreign_key.update_referential_action_desc
        FROM sys.foreign_keys AS foreign_key
        INNER JOIN sys.foreign_key_columns AS foreign_key_column
            ON foreign_key_column.constraint_object_id=foreign_key.object_id
        INNER JOIN sys.tables AS parent_table ON parent_table.object_id=foreign_key.parent_object_id
        INNER JOIN sys.schemas AS parent_schema ON parent_schema.schema_id=parent_table.schema_id
        INNER JOIN sys.columns AS parent_column
            ON parent_column.object_id=foreign_key_column.parent_object_id
            AND parent_column.column_id=foreign_key_column.parent_column_id
        INNER JOIN sys.tables AS referenced_table ON referenced_table.object_id=foreign_key.referenced_object_id
        INNER JOIN sys.schemas AS referenced_schema ON referenced_schema.schema_id=referenced_table.schema_id
        INNER JOIN sys.columns AS referenced_column
            ON referenced_column.object_id=foreign_key_column.referenced_object_id
            AND referenced_column.column_id=foreign_key_column.referenced_column_id
        WHERE parent_table.is_ms_shipped=0
        ORDER BY parent_schema.name,parent_table.name,foreign_key.name,foreign_key_column.constraint_column_id;
        """;

    private const string CheckQuery="""
        SELECT schema_definition.name,table_definition.name,check_definition.name,
            check_definition.definition,check_definition.is_disabled
        FROM sys.check_constraints AS check_definition
        INNER JOIN sys.tables AS table_definition ON table_definition.object_id=check_definition.parent_object_id
        INNER JOIN sys.schemas AS schema_definition ON schema_definition.schema_id=table_definition.schema_id
        WHERE table_definition.is_ms_shipped=0
        ORDER BY schema_definition.name,table_definition.name,check_definition.name;
        """;

    private const string TriggerQuery="""
        SELECT schema_definition.name,table_definition.name,trigger_definition.name,
            module_definition.definition,trigger_definition.is_instead_of_trigger,trigger_definition.is_disabled
        FROM sys.triggers AS trigger_definition
        INNER JOIN sys.tables AS table_definition ON table_definition.object_id=trigger_definition.parent_id
        INNER JOIN sys.schemas AS schema_definition ON schema_definition.schema_id=table_definition.schema_id
        LEFT JOIN sys.sql_modules AS module_definition ON module_definition.object_id=trigger_definition.object_id
        WHERE trigger_definition.parent_class=1 AND table_definition.is_ms_shipped=0
        ORDER BY schema_definition.name,table_definition.name,trigger_definition.name;
        """;

    public static async Task<SqlServerCatalogMetadata> ReadAsync(
        SqlConnection connection,
        CancellationToken cancellationToken,bool analyzeRoutines=true) {
        var tables=new Dictionary<(string Schema,string Table),TableMetadataBuilder>();
        await ReadUniqueKeysAsync(connection,tables,cancellationToken).ConfigureAwait(false);
        await ReadForeignKeysAsync(connection,tables,cancellationToken).ConfigureAwait(false);
        await ReadChecksAsync(connection,tables,cancellationToken).ConfigureAwait(false);
        await ReadTriggersAsync(connection,tables,cancellationToken).ConfigureAwait(false);
        var routines=await SqlServerRoutineCatalogReader.ReadAsync(connection,cancellationToken,analyzeRoutines).ConfigureAwait(false);
        var result=tables.ToDictionary(
            static pair=>pair.Key,
            static pair=>pair.Value.Build());
        return new(result,routines);
    }

    private static async Task ReadUniqueKeysAsync(
        SqlConnection connection,
        Dictionary<(string Schema,string Table),TableMetadataBuilder> tables,
        CancellationToken cancellationToken) {
        await using var command=new SqlCommand(UniqueKeyQuery,connection);
        await using var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
            var table=GetTable(tables,reader.GetString(0),reader.GetString(1));
            var name=reader.GetString(2);
            if(!table.UniqueKeys.TryGetValue(name,out var key)) {
                key=new(name);
                table.UniqueKeys.Add(name,key);
            }
            key.Columns.Add((Convert.ToInt32(reader.GetValue(3),global::System.Globalization.CultureInfo.InvariantCulture),new(reader.GetString(4),reader.GetBoolean(5))));
        }
    }

    private static async Task ReadForeignKeysAsync(
        SqlConnection connection,
        Dictionary<(string Schema,string Table),TableMetadataBuilder> tables,
        CancellationToken cancellationToken) {
        await using var command=new SqlCommand(ForeignKeyQuery,connection);
        await using var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
            var table=GetTable(tables,reader.GetString(0),reader.GetString(1));
            var name=reader.GetString(2);
            if(!table.ForeignKeys.TryGetValue(name,out var key)) {
                key=new(name,reader.GetString(3),reader.GetString(4),reader.GetString(8),reader.GetString(9));
                table.ForeignKeys.Add(name,key);
            }
            key.Columns.Add((reader.GetString(5),reader.GetString(6),reader.GetInt32(7)));
        }
    }

    private static async Task ReadChecksAsync(
        SqlConnection connection,
        Dictionary<(string Schema,string Table),TableMetadataBuilder> tables,
        CancellationToken cancellationToken) {
        await using var command=new SqlCommand(CheckQuery,connection);
        await using var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
            GetTable(tables,reader.GetString(0),reader.GetString(1)).CheckConstraints.Add(new(
                reader.GetString(2),reader.GetString(3),reader.GetBoolean(4)));
        }
    }

    private static async Task ReadTriggersAsync(
        SqlConnection connection,
        Dictionary<(string Schema,string Table),TableMetadataBuilder> tables,
        CancellationToken cancellationToken) {
        await using var command=new SqlCommand(TriggerQuery,connection);
        await using var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
            GetTable(tables,reader.GetString(0),reader.GetString(1)).Triggers.Add(new(
                reader.GetString(2),reader.IsDBNull(3)?string.Empty:reader.GetString(3),
                reader.GetBoolean(4),reader.GetBoolean(5)));
        }
    }

    private static TableMetadataBuilder GetTable(
        Dictionary<(string Schema,string Table),TableMetadataBuilder> tables,
        string schema,
        string table) {
        var key=(schema,table);
        if(!tables.TryGetValue(key,out var result)) {
            result=new();
            tables.Add(key,result);
        }
        return result;
    }

    private sealed class TableMetadataBuilder {
        public Dictionary<string,UniqueKeyBuilder> UniqueKeys { get; }=new(StringComparer.Ordinal);
        public Dictionary<string,ForeignKeyBuilder> ForeignKeys { get; }=new(StringComparer.Ordinal);
        public List<CheckConstraintCatalog> CheckConstraints { get; }=[];
        public List<TriggerCatalog> Triggers { get; }=[];

        public SqlServerTableMetadata Build()=>new(
            this.UniqueKeys.Values.OrderBy(static value=>value.Name,StringComparer.Ordinal)
                .Select(static value=>new UniqueKeyCatalog(value.Name,value.Columns.OrderBy(static item=>item.Ordinal)
                    .Select(static item=>item.Column).ToArray())).ToArray(),
            this.ForeignKeys.Values.OrderBy(static value=>value.Name,StringComparer.Ordinal)
                .Select(static value=>new ForeignKeyCatalog(value.Name,value.ReferencedSchema,value.ReferencedTable,
                    value.Columns.OrderBy(static item=>item.Ordinal).Select(static item=>(item.Column,item.ReferencedColumn)).ToArray(),
                    value.DeleteAction,value.UpdateAction)).ToArray(),
            this.CheckConstraints.OrderBy(static value=>value.Name,StringComparer.Ordinal).ToArray(),
            this.Triggers.OrderBy(static value=>value.Name,StringComparer.Ordinal).ToArray());
    }

    private sealed class UniqueKeyBuilder(string name) {
        public string Name { get; }=name;
        public List<(int Ordinal,KeyColumnCatalog Column)> Columns { get; }=[];
    }

    private sealed class ForeignKeyBuilder(string name,string referencedSchema,string referencedTable,string deleteAction,string updateAction) {
        public string Name { get; }=name;
        public string ReferencedSchema { get; }=referencedSchema;
        public string ReferencedTable { get; }=referencedTable;
        public string DeleteAction { get; }=deleteAction;
        public string UpdateAction { get; }=updateAction;
        public List<(string Column,string ReferencedColumn,int Ordinal)> Columns { get; }=[];
    }
}
