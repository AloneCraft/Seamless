namespace Seamless.Database.SqlServer;

public sealed class SqlServerCatalogReader(string connectionString) {
    private const string TableQuery = """
        SELECT schema_definition.name,table_definition.name,column_definition.column_id,
            column_definition.name,COALESCE(system_type.name,declared_type.name),
            column_definition.max_length,column_definition.precision,column_definition.scale,
            column_definition.is_nullable,column_definition.is_identity,
            default_definition.definition,computed_definition.definition,
            primary_key.name,primary_key.key_ordinal,primary_key.is_descending_key,
            column_definition.encryption_type
        FROM sys.tables AS table_definition
        INNER JOIN sys.schemas AS schema_definition ON schema_definition.schema_id=table_definition.schema_id
        INNER JOIN sys.columns AS column_definition ON column_definition.object_id=table_definition.object_id
        INNER JOIN sys.types AS declared_type ON declared_type.user_type_id=column_definition.user_type_id
        LEFT JOIN sys.types AS system_type
            ON system_type.user_type_id=declared_type.system_type_id
            AND system_type.user_type_id=system_type.system_type_id
        LEFT JOIN sys.default_constraints AS default_definition ON default_definition.object_id=column_definition.default_object_id
        LEFT JOIN sys.computed_columns AS computed_definition
            ON computed_definition.object_id=column_definition.object_id
            AND computed_definition.column_id=column_definition.column_id
        LEFT JOIN (
            SELECT index_definition.object_id,index_definition.name,index_column.column_id,
                index_column.key_ordinal,index_column.is_descending_key
            FROM sys.indexes AS index_definition
            INNER JOIN sys.index_columns AS index_column
                ON index_column.object_id=index_definition.object_id
                AND index_column.index_id=index_definition.index_id
            WHERE index_definition.is_primary_key=1 AND index_column.is_included_column=0
        ) AS primary_key
            ON primary_key.object_id=column_definition.object_id
            AND primary_key.column_id=column_definition.column_id
        WHERE table_definition.is_ms_shipped=0
        ORDER BY schema_definition.name,table_definition.name,column_definition.column_id;
        """;

    public async Task<DatabaseCatalog> ReadAsync(CancellationToken cancellationToken=default) {
        await using var connection=new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var databaseName=await ReadDatabaseNameAsync(connection,cancellationToken).ConfigureAwait(false);
        var tables=new Dictionary<(string Schema,string Table),TableBuilder>();
        await ReadTablesAsync(connection,tables,cancellationToken).ConfigureAwait(false);
        var views=await SqlServerViewCatalogReader.ReadAsync(connection,cancellationToken).ConfigureAwait(false);
        var metadata=await SqlServerMetadataReader.ReadAsync(connection,cancellationToken).ConfigureAwait(false);
        var tableCatalogs=tables.ToDictionary(
            static pair=>pair.Key,
            pair=>pair.Value.Build(metadata.TryGetValue(pair.Key,out var value)?value:null));
        var schemaNames=tableCatalogs.Keys.Select(static key=>key.Schema)
            .Concat(views.Select(static view=>view.SchemaName))
            .Concat(metadata.Routines.Select(static routine=>routine.SchemaName))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value=>value,StringComparer.Ordinal);
        var schemas=schemaNames.Select(schemaName=>new SchemaCatalog(
                schemaName,
                tableCatalogs.Where(pair=>pair.Key.Schema==schemaName)
                    .OrderBy(static pair=>pair.Key.Table,StringComparer.Ordinal)
                    .Select(static pair=>pair.Value).ToArray(),
                views.Where(view=>view.SchemaName==schemaName)
                    .Select(static view=>view.View).ToArray(),
                metadata.Routines.Where(routine=>routine.SchemaName==schemaName).ToArray()))
            .ToArray();
        return new(databaseName,schemas);
    }

    private static async Task ReadTablesAsync(
        SqlConnection connection,
        Dictionary<(string Schema,string Table),TableBuilder> tables,
        CancellationToken cancellationToken) {
        await using var command=new SqlCommand(TableQuery,connection);
        await using var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
            var schemaName=reader.GetString(0);
            var tableName=reader.GetString(1);
            var key=(schemaName,tableName);
            if(!tables.TryGetValue(key,out var table)) {
                table=new(schemaName,tableName);
                tables.Add(key,table);
            }
            var columnName=reader.GetString(3);
            table.Columns.Add(new(
                reader.GetInt32(2)-1,columnName,
                SqlServerTypeMapper.Map(reader.GetString(4),reader.GetBoolean(8),reader.GetInt16(5),
                    reader.GetByte(6),reader.GetByte(7),!reader.IsDBNull(15)),
                reader.GetBoolean(9),reader.IsDBNull(10)?null:reader.GetString(10),
                reader.IsDBNull(11)?null:reader.GetString(11)));
            if(!reader.IsDBNull(12)) {
                table.PrimaryKeyName=reader.GetString(12);
                table.PrimaryKeyColumns.Add((Convert.ToInt32(reader.GetValue(13),global::System.Globalization.CultureInfo.InvariantCulture),
                    new(columnName,reader.GetBoolean(14))));
            }
        }
    }

    private static async Task<string> ReadDatabaseNameAsync(SqlConnection connection,CancellationToken cancellationToken) {
        await using var command=new SqlCommand("SELECT DB_NAME();",connection);
        return (string)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    private sealed class TableBuilder(string schemaName,string tableName) {
        public string SchemaName { get; }=schemaName;
        public string TableName { get; }=tableName;
        public List<ColumnCatalog> Columns { get; }=[];
        public string? PrimaryKeyName { get; set; }
        public List<(int Ordinal,KeyColumnCatalog Column)> PrimaryKeyColumns { get; }=[];

        public TableCatalog Build(SqlServerTableMetadata? metadata) {
            PrimaryKeyCatalog? primaryKey=null;
            if(this.PrimaryKeyName is not null) {
                primaryKey=new(this.PrimaryKeyName,this.PrimaryKeyColumns.OrderBy(static value=>value.Ordinal)
                    .Select(static value=>value.Column).ToArray());
            }
            return new(this.TableName,this.Columns.OrderBy(static value=>value.Ordinal).ToArray(),primaryKey,
                metadata?.UniqueKeys,metadata?.ForeignKeys,metadata?.CheckConstraints,metadata?.Triggers);
        }
    }
}
