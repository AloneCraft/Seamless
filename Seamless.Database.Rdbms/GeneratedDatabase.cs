namespace Seamless.Database.Rdbms;

public sealed record GeneratedDatabase(
    string SourceDatabaseName,
    string DatabaseName,
    string SchemaFingerprint,
    IReadOnlyList<GeneratedSourceFile> Files,
    IReadOnlyList<GeneratedTableBinding> Tables);
