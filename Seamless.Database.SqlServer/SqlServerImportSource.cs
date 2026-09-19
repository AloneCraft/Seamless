using System.Reflection;
using System.Text;

namespace Seamless.Database.SqlServer;

public sealed class SqlServerImportSource:global::Seamless.Database.IDatabaseImportSource,IDisposable {
    private readonly SqlConnection connection;
    private readonly IReadOnlySet<(string Schema,string Table,string Column)> encryptedColumns;
    private readonly IReadOnlySet<(string Schema,string Table)> memoryOptimizedTables;
    private readonly SqlTransaction transaction;
    private readonly IReadOnlyDictionary<string,GeneratedTableBinding> tables;
    private readonly Dictionary<string,long> rowsReadByTable=new(StringComparer.Ordinal);
    private bool disposed;

    public long RowsRead { get; private set; }
    public IReadOnlyDictionary<string,long> RowsReadByTable=>this.rowsReadByTable;

    public SqlServerImportSource(string connectionString,IEnumerable<GeneratedTableBinding> tables) {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(tables);
        this.tables=tables.ToDictionary(static table=>table.ObjectName,StringComparer.Ordinal);
        this.connection=new(connectionString);
        this.connection.Open();
        try {
            this.encryptedColumns=ReadEncryptedColumns(this.connection);
            this.memoryOptimizedTables=ReadMemoryOptimizedTables(this.connection);
            this.transaction=this.connection.BeginTransaction(SelectIsolationLevel(this.memoryOptimizedTables.Count>0));
        } catch {
            this.connection.Dispose();
            throw;
        }
    }

    public IEnumerable<TRecord> ReadTable<TRecord>(global::Seamless.Database.System.Keys.DatabaseObject key) {
        ObjectDisposedException.ThrowIf(this.disposed,this);
        if(!this.tables.TryGetValue(key.Name,out var table)) {
            throw new KeyNotFoundException($"SQL Server table binding '{key.Name}' was not found.");
        }
        return this.Read<TRecord>(table);
    }

    private IEnumerable<TRecord> Read<TRecord>(GeneratedTableBinding table) {
        if(!this.rowsReadByTable.TryAdd(table.ObjectName,0)) {
            throw new InvalidOperationException($"SQL Server table '{table.ObjectName}' was read more than once during one import.");
        }
        var constructor=FindConstructor<TRecord>(table.Columns.Count);
        using var command=this.connection.CreateCommand();
        command.Transaction=this.transaction;
        command.CommandText=CreateSelect(
            table,
            this.memoryOptimizedTables.Contains((table.SourceSchema,table.SourceTable)),
            this.encryptedColumns);
        using var reader=command.ExecuteReader(CommandBehavior.SequentialAccess);
        var parameters=constructor.GetParameters();
        while(reader.Read()) {
            var values=new object?[table.Columns.Count];
            for(var index=0;index<values.Length;index++) {
                if(reader.IsDBNull(index)) {
                    values[index]=null;
                    continue;
                }
                var value=reader.GetValue(index);
                try {
                    values[index]=ConvertValue(value,parameters[index].ParameterType);
                } catch(Exception exception) when(exception is InvalidCastException or FormatException or OverflowException) {
                    throw new InvalidOperationException(
                        $"SQL Server value '{table.SourceSchema}.{table.SourceTable}.{table.Columns[index].SourceName}' "
                        +$"could not be converted from '{value.GetType()}' to '{parameters[index].ParameterType}'.",
                        exception);
                }
            }
            var record=(TRecord)constructor.Invoke(values);
            this.RowsRead++;
            this.rowsReadByTable[table.ObjectName]++;
            yield return record;
        }
    }

    private static ConstructorInfo FindConstructor<TRecord>(int parameterCount) {
        var constructors=typeof(TRecord).GetConstructors(BindingFlags.Instance|BindingFlags.Public);
        return constructors.SingleOrDefault(constructor=>constructor.GetParameters().Length==parameterCount)
            ??throw new InvalidOperationException(
                $"Record type '{typeof(TRecord)}' does not have one public constructor with {parameterCount} parameters.");
    }

    internal static IsolationLevel SelectIsolationLevel(bool containsMemoryOptimizedTables)=>
        containsMemoryOptimizedTables?IsolationLevel.ReadCommitted:IsolationLevel.Snapshot;

    internal static string CreateSelect(GeneratedTableBinding table,bool isMemoryOptimized)=>
        CreateSelect(table,isMemoryOptimized,new HashSet<(string Schema,string Table,string Column)>());

    internal static string CreateSelect(
        GeneratedTableBinding table,
        bool isMemoryOptimized,
        IReadOnlySet<(string Schema,string Table,string Column)> encryptedColumns) {
        var builder=new StringBuilder("SELECT ");
        for(var index=0;index<table.Columns.Count;index++) {
            if(index>0) {
                builder.Append(',');
            }
            var column=Quote(table.Columns[index].SourceName);
            var isEncrypted=encryptedColumns.Contains((
                table.SourceSchema,
                table.SourceTable,
                table.Columns[index].SourceName));
            if(table.Columns[index].Type.Kind==DatabaseTypeKind.Binary&&!isEncrypted) {
                builder.Append($"CONVERT(varbinary(max),{column})");
            } else {
                builder.Append(column);
            }
        }
        builder.Append(" FROM ");
        builder.Append(Quote(table.SourceSchema));
        builder.Append('.');
        builder.Append(Quote(table.SourceTable));
        if(isMemoryOptimized) {
            builder.Append(" WITH (SNAPSHOT)");
        }
        builder.Append(';');
        return builder.ToString();
    }

    private static HashSet<(string Schema,string Table,string Column)> ReadEncryptedColumns(SqlConnection connection) {
        using var command=connection.CreateCommand();
        command.CommandText="""
            SELECT SCHEMA_NAME(table_definition.schema_id),table_definition.name,column_definition.name
            FROM sys.tables AS table_definition
            INNER JOIN sys.columns AS column_definition
                ON column_definition.object_id=table_definition.object_id
            WHERE column_definition.encryption_type IS NOT NULL;
            """;
        using var reader=command.ExecuteReader();
        var columns=new HashSet<(string Schema,string Table,string Column)>();
        while(reader.Read()) {
            columns.Add((reader.GetString(0),reader.GetString(1),reader.GetString(2)));
        }
        return columns;
    }

    private static HashSet<(string Schema,string Table)> ReadMemoryOptimizedTables(SqlConnection connection) {
        using var command=connection.CreateCommand();
        command.CommandText="""
            SELECT SCHEMA_NAME(schema_id),name
            FROM sys.tables
            WHERE is_memory_optimized=1;
            """;
        using var reader=command.ExecuteReader();
        var tables=new HashSet<(string Schema,string Table)>();
        while(reader.Read()) {
            tables.Add((reader.GetString(0),reader.GetString(1)));
        }
        return tables;
    }

    private static string Quote(string value)=>$"[{value.Replace("]","]]",StringComparison.Ordinal)}]";

    private static object ConvertValue(object value,Type destinationType) {
        var type=Nullable.GetUnderlyingType(destinationType)??destinationType;
        if(type.IsInstanceOfType(value)) {
            return value;
        }
        if(type==typeof(DateOnly)&&value is DateTime dateTime) {
            return DateOnly.FromDateTime(dateTime);
        }
        if(type==typeof(TimeOnly)&&value is TimeSpan timeSpan) {
            return TimeOnly.FromTimeSpan(timeSpan);
        }
        return Convert.ChangeType(value,type,global::System.Globalization.CultureInfo.InvariantCulture);
    }

    public void Dispose() {
        if(this.disposed) {
            return;
        }
        this.disposed=true;
        try {
            this.transaction.Rollback();
        } finally {
            this.transaction.Dispose();
            this.connection.Dispose();
        }
    }
}
