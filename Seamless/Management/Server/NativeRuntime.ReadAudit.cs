namespace Seamless.Management.Server;

internal sealed partial class NativeRuntime{
    internal Action<ReadAuditObservation>? ReadCompleted{get;set;}
}
