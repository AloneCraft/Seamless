namespace Seamless.Database.Rdbms;

public sealed record ParameterCatalog(
    int Ordinal,
    string Name,
    DatabaseType Type,
    bool IsOutput,
    bool IsTableValued=false);
