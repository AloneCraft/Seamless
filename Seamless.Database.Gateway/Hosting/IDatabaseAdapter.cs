namespace Seamless.Database.Gateway.Hosting;

public interface IDatabaseAdapter{
    string DatabaseName{get;}
    IReadOnlyList<TableMetadata>GetTables();
    IReadOnlyList<DatabaseViewDefinition>GetViews()=>[];
    IReadOnlyList<DatabaseRoutineDefinition>GetRoutines()=>[];
    TableMetadata? FindTable(string name);
    QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columnNames,Func<object,bool>? predicate,SessionContext session);
    int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?>values,SessionContext session);
    int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?>values,Func<object,bool>? predicate,SessionContext session);
    int ExecuteDelete(string tableName,Func<object,bool>? predicate,SessionContext session);
    object? GetRecordValue(string table,string column,object row){var property=row.GetType().GetProperty(column)??throw new InvalidOperationException("Record column mapping is unavailable.");return property.GetValue(row);}
    int ExecuteUpdateExpressions(string table,IReadOnlyDictionary<string,Func<object,object?>> values,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException("This adapter does not support per-row update expressions.");
    void BeginTransaction(SessionContext session);
    void CommitTransaction(SessionContext session);
    void RollbackTransaction(SessionContext session);
}
