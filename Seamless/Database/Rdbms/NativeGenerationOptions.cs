namespace Seamless.Database.Rdbms;

public sealed record NativeGenerationOptions{
    public int InlineTextByteLimit{get;init;}=512;
    public int InlineRecordByteLimit{get;init;}=4096;
}
