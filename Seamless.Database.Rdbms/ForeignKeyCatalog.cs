namespace Seamless.Database.Rdbms;

public sealed record ForeignKeyCatalog(
    string Name,
    string ReferencedSchema,
    string ReferencedTable,
    IReadOnlyList<(string Column,string ReferencedColumn)> Columns,
    string DeleteAction,
    string UpdateAction);
