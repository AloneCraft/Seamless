namespace Seamless.Database.Rdbms;

public sealed record TriggerCatalog(
    string Name,
    string Definition,
    bool IsInsteadOf,
    bool IsDisabled);
