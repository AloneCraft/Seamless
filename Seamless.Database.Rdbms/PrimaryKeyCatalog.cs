namespace Seamless.Database.Rdbms;

public sealed record PrimaryKeyCatalog(string Name,IReadOnlyList<KeyColumnCatalog> Columns,byte? IndexType=null);
