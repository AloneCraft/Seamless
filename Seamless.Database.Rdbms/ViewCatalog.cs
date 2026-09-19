namespace Seamless.Database.Rdbms;

public sealed record ViewCatalog(
    string Name,
    IReadOnlyList<ColumnCatalog> Columns,
    UniqueKeyCatalog? UniqueKey,
    string? Definition=null,
    IReadOnlyList<string>? Dependencies=null);
