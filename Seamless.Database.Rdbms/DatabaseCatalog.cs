namespace Seamless.Database.Rdbms;

public sealed record DatabaseCatalog(string Name,IReadOnlyList<SchemaCatalog> Schemas,string? DefaultCollation=null);
