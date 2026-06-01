using BenchmarkDotNet.Toolchains;
using BenchmarkDotNet.Toolchains.CsProj;

namespace Benchmark;

public static class Constants
{
    public static readonly IToolchain DefaultToolchain = CsProjCoreToolchain.NetCoreApp10_0;
    public static class DefineConstants
    {
        public const string USE_SYSTEM_LINQ = "USE_SYSTEM_LINQ";
    }

    public static class CustomBuildConfigurations
    {
        public const string SystemLinq = "SystemLinq";
    }
}
