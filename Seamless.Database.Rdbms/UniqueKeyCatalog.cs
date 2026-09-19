namespace Seamless.Database.Rdbms;

public sealed record UniqueKeyCatalog(string Name,IReadOnlyList<KeyColumnCatalog> Columns);
