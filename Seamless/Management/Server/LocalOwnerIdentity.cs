using System.IO.Pipes;
using Seamless.Transport.Kestrel;

namespace Seamless.Management.Server;

internal sealed class LocalOwnerIdentity{
    private readonly LocalPipeOwner owner;
    private LocalOwnerIdentity(LocalPipeOwner owner)=>this.owner=owner;
    public static LocalOwnerIdentity Capture()=>new(LocalPipeOwner.Capture());
    public UserAccount Account()=>new(this.owner.Name,"",[new("*",ManagementPermission.All)]);
    public void Verify(NamedPipeServerStream pipe)=>this.owner.Verify(pipe);
}
