namespace Seamless.Database.Rdbms;

public sealed record RelationalAlgebraNode(
    RelationalAlgebraOperator Operator,
    string? RelationName,
    IReadOnlyList<RelationalAlgebraNode> Inputs);
