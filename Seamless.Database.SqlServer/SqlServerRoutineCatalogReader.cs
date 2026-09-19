namespace Seamless.Database.SqlServer;

internal static class SqlServerRoutineCatalogReader {
    private const string RoutineQuery="""
        SELECT routine_definition.object_id,schema_definition.name,routine_definition.name,
            routine_definition.type,OBJECT_DEFINITION(routine_definition.object_id)
        FROM sys.objects AS routine_definition
        INNER JOIN sys.schemas AS schema_definition ON schema_definition.schema_id=routine_definition.schema_id
        WHERE routine_definition.is_ms_shipped=0
            AND routine_definition.type IN ('IF','TF','FN','FS','FT','P','PC')
        ORDER BY schema_definition.name,routine_definition.name,routine_definition.object_id;
        """;

    private const string ParameterQuery="""
        SELECT parameter_definition.object_id,parameter_definition.parameter_id,parameter_definition.name,
            COALESCE(system_type.name,declared_type.name),parameter_definition.max_length,
            parameter_definition.precision,parameter_definition.scale,parameter_definition.is_output,
            parameter_definition.is_nullable,declared_type.is_table_type
        FROM sys.parameters AS parameter_definition
        INNER JOIN sys.objects AS routine_definition ON routine_definition.object_id=parameter_definition.object_id
        INNER JOIN sys.types AS declared_type ON declared_type.user_type_id=parameter_definition.user_type_id
        LEFT JOIN sys.types AS system_type
            ON system_type.user_type_id=declared_type.system_type_id
            AND system_type.user_type_id=system_type.system_type_id
        WHERE routine_definition.is_ms_shipped=0
            AND routine_definition.type IN ('IF','TF','FN','FS','FT','P','PC')
        ORDER BY parameter_definition.object_id,parameter_definition.parameter_id;
        """;

    private const string ResultQuery="""
        SELECT routine_definition.object_id,result_column.column_id,result_column.name,
            COALESCE(system_type.name,declared_type.name),result_column.max_length,
            result_column.precision,result_column.scale,result_column.is_nullable
        FROM sys.objects AS routine_definition
        INNER JOIN sys.columns AS result_column ON result_column.object_id=routine_definition.object_id
        INNER JOIN sys.types AS declared_type ON declared_type.user_type_id=result_column.user_type_id
        LEFT JOIN sys.types AS system_type
            ON system_type.user_type_id=declared_type.system_type_id
            AND system_type.user_type_id=system_type.system_type_id
        WHERE routine_definition.is_ms_shipped=0 AND routine_definition.type IN ('IF','TF','FT')
        ORDER BY routine_definition.object_id,result_column.column_id;
        """;

    public static async Task<IReadOnlyList<RoutineCatalog>> ReadAsync(
        SqlConnection connection,
        CancellationToken cancellationToken) {
        var builders=new Dictionary<int,RoutineBuilder>();
        await using(var command=new SqlCommand(RoutineQuery,connection)) {
            await using var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
                var objectId=reader.GetInt32(0);
                builders.Add(objectId,new(objectId,reader.GetString(1),reader.GetString(2),
                    MapKind(reader.GetString(3).Trim()),reader.IsDBNull(4)?null:reader.GetString(4)));
            }
        }
        await using(var command=new SqlCommand(ParameterQuery,connection)) {
            await using var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
                if(!builders.TryGetValue(reader.GetInt32(0),out var routine)) {
                    continue;
                }
                var parameterId=reader.GetInt32(1);
                var isTableValued=reader.GetBoolean(9);
                var type=isTableValued
                    ?new DatabaseType(DatabaseTypeKind.Object,true)
                    :SqlServerTypeMapper.Map(reader.GetString(3),reader.GetBoolean(8),reader.GetInt16(4),
                        reader.GetByte(5),reader.GetByte(6));
                if(parameterId==0) {
                    routine.ReturnType=type;
                } else {
                    routine.Parameters.Add(new(parameterId,reader.IsDBNull(2)?$"parameter{parameterId}":reader.GetString(2),
                        type,reader.GetBoolean(7),isTableValued));
                }
            }
        }
        await using(var command=new SqlCommand(ResultQuery,connection)) {
            await using var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
                if(!builders.TryGetValue(reader.GetInt32(0),out var routine)) {
                    continue;
                }
                routine.ResultColumns.Add(new(reader.GetInt32(1)-1,reader.GetString(2),
                    SqlServerTypeMapper.Map(reader.GetString(3),reader.GetBoolean(7),reader.GetInt16(4),
                        reader.GetByte(5),reader.GetByte(6)),false));
            }
        }
        foreach(var routine in builders.Values) {
            if(routine.Definition is null) {
                continue;
            }
            var analysis=SqlServerRoutineAnalyzer.Analyze(routine.Definition);
            routine.HasMutation=analysis.HasMutation;
            routine.ResultSetCount=analysis.ResultSetCount;
            routine.ReturnCount=analysis.ReturnCount;
            routine.RelationalAlgebra=analysis.RelationalAlgebra;
        }
        return builders.Values.OrderBy(static value=>value.SchemaName,StringComparer.Ordinal)
            .ThenBy(static value=>value.Name,StringComparer.Ordinal)
            .Select(static value=>value.Build()).ToArray();
    }

    private static RoutineKind MapKind(string type)=>type switch {
        "IF" or "TF" or "FT"=>RoutineKind.TableFunction,
        "FN" or "FS"=>RoutineKind.ScalarFunction,
        "P" or "PC"=>RoutineKind.StoredProcedure,
        _=>throw new ArgumentOutOfRangeException(nameof(type),type,"Unsupported SQL Server routine type."),
    };

    private sealed class RoutineBuilder(int objectId,string schemaName,string name,RoutineKind kind,string? definition) {
        public int ObjectId { get; }=objectId;
        public string SchemaName { get; }=schemaName;
        public string Name { get; }=name;
        public RoutineKind Kind { get; }=kind;
        public string? Definition { get; }=definition;
        public DatabaseType? ReturnType { get; set; }
        public List<ParameterCatalog> Parameters { get; }=[];
        public List<ColumnCatalog> ResultColumns { get; }=[];
        public bool HasMutation { get; set; }
        public int ResultSetCount { get; set; }
        public int ReturnCount { get; set; }
        public RelationalAlgebraPlan? RelationalAlgebra { get; set; }

        public RoutineCatalog Build()=>new(this.SchemaName,this.Name,this.Kind,this.ReturnType,
            this.Parameters.OrderBy(static value=>value.Ordinal).ToArray(),
            this.ResultColumns.OrderBy(static value=>value.Ordinal).ToArray(),this.Definition,
            this.HasMutation,this.ResultSetCount,this.ReturnCount,this.RelationalAlgebra);
    }
}
