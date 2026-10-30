namespace Seamless.Database.Rdbms;

public sealed record RoutineCatalog(
    string SchemaName,
    string Name,
    RoutineKind Kind,
    DatabaseType? ReturnType,
    IReadOnlyList<ParameterCatalog> Parameters,
    IReadOnlyList<ColumnCatalog> ResultColumns,
    string? Definition,
    bool HasMutation,
    int ResultSetCount,
    int ReturnCount,
    RelationalAlgebraPlan? RelationalAlgebra=null){
    public string?SourceObjectType{get;init;}
    public GeneratedRoutineImplementation?Implementation{get;init;}
    public string?CompilationDiagnostic{get;init;}
}
