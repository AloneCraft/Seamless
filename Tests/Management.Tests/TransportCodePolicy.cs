using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class TransportCodePolicy{
    [Theory]
    [InlineData("return new Seamless.Transport.Kestrel.ConnectionQuota(1).Maximum;")]
    [InlineData("Seamless.Transport.Kestrel.KestrelTcpHost? value=null;return 1;")]
    [InlineData("Seamless.Transport.Kestrel.LengthPrefixedPipe? value=null;return 1;")]
    public void ServerInfrastructureIsNotAvailableToDatabaseCode(string body){
        var source=new SourceSnapshot("1",[new("Query.cs","public static class Q{public static int Run(){"+body+"}}")],[]);var compiled=new RuntimeCompiler().Compile(source,entryType:"Q",entryMember:"Run");Assert.Empty(compiled.AssemblyBytes);Assert.Contains(compiled.Result.Diagnostics,item=>item.Code=="ManagementCodePolicy");
    }
}
