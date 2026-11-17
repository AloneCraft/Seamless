namespace Management.Tests;

internal static class TestProcessEntryPoint{
    public static Task<int> Main(string[] arguments){
        if(arguments.Length>0&&arguments[0] is "gateway-process-kill" or "--transport-probe" or "--record-owned-tls-client" or "--record-owned-ipc-client"){
            return global::Seamless.Database.Gateway.Tests.GatewayCrashEntryPoint.Main(arguments);
        }
        return Task.FromResult(CrashProbe.Main(arguments));
    }
}
