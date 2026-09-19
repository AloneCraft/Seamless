namespace Seamless.Database.Rdbms;

public sealed record DatabaseType(
    DatabaseTypeKind Kind,
    bool IsNullable,
    int? MaximumLength=null,
    byte? Precision=null,
    byte? Scale=null,
    global::System.Type? RuntimeType=null);
