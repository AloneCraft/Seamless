namespace Seamless.Database.Rdbms;

public sealed record ColumnCatalog(
    int Ordinal,
    string Name,
    DatabaseType Type,
    bool IsIdentity,
    string? DefaultDefinition=null,
    string? ComputedDefinition=null,
    bool IsSynthetic=false);
