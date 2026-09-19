namespace Seamless.Database.SqlServer;

internal static class SqlServerViewCatalogReader {
    private const string Query="""
        SELECT
            schema_definition.name AS SchemaName,
            view_definition.name AS ViewName,
            column_definition.column_id AS ColumnOrdinal,
            column_definition.name AS ColumnName,
            COALESCE(system_type.name,declared_type.name) AS SystemTypeName,
            column_definition.max_length AS MaximumLength,
            column_definition.precision AS PrecisionValue,
            column_definition.scale AS ScaleValue,
            column_definition.is_nullable AS IsNullable,
            unique_index.name AS UniqueKeyName,
            key_column.key_ordinal AS UniqueKeyOrdinal,
            key_column.is_descending_key AS UniqueKeyDescending,
            OBJECT_DEFINITION(view_definition.object_id) AS Definition
        FROM sys.views AS view_definition
        INNER JOIN sys.schemas AS schema_definition
            ON schema_definition.schema_id=view_definition.schema_id
        INNER JOIN sys.columns AS column_definition
            ON column_definition.object_id=view_definition.object_id
        INNER JOIN sys.types AS declared_type
            ON declared_type.user_type_id=column_definition.user_type_id
        LEFT JOIN sys.types AS system_type
            ON system_type.user_type_id=column_definition.system_type_id
            AND system_type.user_type_id=system_type.system_type_id
        OUTER APPLY (
            SELECT TOP (1)
                index_definition.index_id,
                index_definition.name
            FROM sys.indexes AS index_definition
            WHERE index_definition.object_id=view_definition.object_id
                AND index_definition.is_unique=1
                AND index_definition.is_hypothetical=0
                AND index_definition.is_disabled=0
            ORDER BY
                CASE WHEN index_definition.type=1 THEN 0 ELSE 1 END,
                index_definition.index_id
        ) AS unique_index
        LEFT JOIN sys.index_columns AS key_column
            ON key_column.object_id=view_definition.object_id
            AND key_column.index_id=unique_index.index_id
            AND key_column.column_id=column_definition.column_id
            AND key_column.key_ordinal>0
        WHERE view_definition.is_ms_shipped=0
        ORDER BY schema_definition.name,view_definition.name,column_definition.column_id;
        """;
    private const string DependencyQuery="""
        SELECT source_schema.name,source_view.name,dependency.referenced_schema_name,
            dependency.referenced_entity_name
        FROM sys.sql_expression_dependencies AS dependency
        INNER JOIN sys.views AS source_view
            ON source_view.object_id=dependency.referencing_id
        INNER JOIN sys.schemas AS source_schema
            ON source_schema.schema_id=source_view.schema_id
        WHERE source_view.is_ms_shipped=0
            AND dependency.referenced_entity_name IS NOT NULL
        ORDER BY source_schema.name,source_view.name,dependency.referenced_schema_name,
            dependency.referenced_entity_name;
        """;

    public static async Task<IReadOnlyList<SqlServerViewDefinition>> ReadAsync(
        SqlConnection connection,
        CancellationToken cancellationToken) {
        var views=new Dictionary<(string Schema,string View),ViewBuilder>();
        await using(var command=new SqlCommand(Query,connection) {
            CommandType=CommandType.Text,
        }) {
            await using var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
            var schemaName=reader.GetString(0);
            var viewName=reader.GetString(1);
            var key=(schemaName,viewName);
            if(!views.TryGetValue(key,out var view)) {
                view=new(schemaName,viewName);
                views.Add(key,view);
            }
            var columnName=reader.GetString(3);
            if(!reader.IsDBNull(12)) {
                view.Definition=reader.GetString(12);
            }
            view.Columns.Add(new(
                reader.GetInt32(2)-1,
                columnName,
                SqlServerTypeMapper.Map(
                    reader.GetString(4),
                    reader.GetBoolean(8),
                    reader.GetInt16(5),
                    reader.GetByte(6),
                    reader.GetByte(7)),
                false));
            if(!reader.IsDBNull(9)&&!reader.IsDBNull(10)) {
                view.UniqueKeyName=reader.GetString(9);
                view.UniqueKeyColumns.Add((
                    Convert.ToInt32(reader.GetValue(10),global::System.Globalization.CultureInfo.InvariantCulture),
                    new(columnName,reader.GetBoolean(11))));
            }
            }
        }
        await using(var dependencyCommand=new SqlCommand(DependencyQuery,connection)) {
            await using var dependencyReader=await dependencyCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while(await dependencyReader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
                if(views.TryGetValue((dependencyReader.GetString(0),dependencyReader.GetString(1)),out var view)) {
                    var schema=dependencyReader.IsDBNull(2)?string.Empty:dependencyReader.GetString(2);
                    var name=dependencyReader.GetString(3);
                    view.Dependencies.Add(schema.Length==0?name:$"{schema}.{name}");
                }
            }
        }
        return views.Values
            .OrderBy(static view=>view.SchemaName,StringComparer.Ordinal)
            .ThenBy(static view=>view.ViewName,StringComparer.Ordinal)
            .Select(static view=>view.Build())
            .ToArray();
    }

    private sealed class ViewBuilder(string schemaName,string viewName) {
        public string SchemaName { get; }=schemaName;
        public string ViewName { get; }=viewName;
        public List<ColumnCatalog> Columns { get; }=[];
        public string? UniqueKeyName { get; set; }
        public List<(int Ordinal,KeyColumnCatalog Column)> UniqueKeyColumns { get; }=[];
        public string? Definition { get; set; }
        public HashSet<string> Dependencies { get; }=new(StringComparer.Ordinal);

        public SqlServerViewDefinition Build() {
            UniqueKeyCatalog? uniqueKey=null;
            if(this.UniqueKeyName is not null) {
                uniqueKey=new(
                    this.UniqueKeyName,
                    this.UniqueKeyColumns
                        .OrderBy(static value=>value.Ordinal)
                        .Select(static value=>value.Column)
                        .ToArray());
            }
            return new(
                this.SchemaName,
                new(
                    this.ViewName,
                    this.Columns.OrderBy(static value=>value.Ordinal).ToArray(),
                    uniqueKey,
                    this.Definition,
                    this.Dependencies.OrderBy(static value=>value,StringComparer.Ordinal).ToArray()));
        }
    }
}

internal sealed record SqlServerViewDefinition(string SchemaName,ViewCatalog View);
