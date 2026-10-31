namespace Seamless.Database.Rdbms;

public sealed record GeneratedColumnBinding(
    string SourceName,
    string PropertyName,
    DatabaseType Type,
    bool IsSynthetic=false);
