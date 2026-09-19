namespace Seamless.Database.Rdbms;

public sealed record GeneratedTableBinding(
    string ObjectName,
    string SourceSchema,
    string SourceTable,
    IReadOnlyList<GeneratedColumnBinding> Columns,
    GeneratedRelationKind Kind=GeneratedRelationKind.Table);
