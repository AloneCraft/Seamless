namespace Seamless.Database.Rdbms;

public sealed record GeneratedRoutineBinding(
    string SourceDatabaseName,
    string SourceSchemaName,
    string SourceName,
    RoutineKind Kind,
    string DeclaringTypeName,
    string MethodName,
    string SourceFile,
    string CSharpSource,
    bool IsStub);
