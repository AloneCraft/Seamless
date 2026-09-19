namespace Seamless.Database.System.Records;

public readonly record struct DatabaseObject(
    global::Seamless.Database.System.Keys.DatabaseObject Key,
    global::Seamless.Database.System.DatabaseObjectKind Kind,
    global::System.Type RecordType);
