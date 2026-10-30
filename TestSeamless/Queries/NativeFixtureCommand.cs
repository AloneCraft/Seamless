using MemoryPack;
namespace TestSeamless.Queries;
[MemoryPackable]public sealed partial record NativeFixtureCommand(int Id,string Name);
[MemoryPackable]public sealed partial record NativeFixtureRow(int Id,string Name);
