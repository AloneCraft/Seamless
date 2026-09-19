namespace Seamless.Database.Rdbms;

public sealed record RelationalAlgebraPlan(
    IReadOnlyList<RelationalAlgebraNode> Operations);
