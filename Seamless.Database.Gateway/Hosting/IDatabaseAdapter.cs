namespace Seamless.Database.Gateway.Hosting;

public interface IDatabaseAdapter{
    string DatabaseName{get;}
    IReadOnlyList<TableMetadata>GetTables();
    TableMetadata? FindTable(string name);
    QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columnNames,Func<object,bool>? predicate,SessionContext session);
    int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?>values,SessionContext session);
    int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?>values,Func<object,bool>? predicate,SessionContext session);
    int ExecuteDelete(string tableName,Func<object,bool>? predicate,SessionContext session);
    void BeginTransaction(SessionContext session);
    void CommitTransaction(SessionContext session);
    void RollbackTransaction(SessionContext session);
}
