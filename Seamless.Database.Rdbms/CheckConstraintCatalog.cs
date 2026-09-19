namespace Seamless.Database.Rdbms;

public sealed record CheckConstraintCatalog(
    string Name,
    string Definition,
    bool IsDisabled);
