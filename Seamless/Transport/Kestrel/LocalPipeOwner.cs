using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace Seamless.Transport.Kestrel;

/// <summary>CurrentUserOnlyで作成した接続のOS所有者を照合する。</summary>
public sealed class LocalPipeOwner{
    public string Name{get;}
    private readonly string? windowsSid;
    private LocalPipeOwner(string name,string? windowsSid){this.Name=name;this.windowsSid=windowsSid;}
    public static LocalPipeOwner Capture(){
        if(OperatingSystem.IsWindows()){
            using var impersonated=WindowsIdentity.GetCurrent(true);
            if(impersonated is not null){throw new InvalidOperationException("ローカル所有者の入口は偽装していないホストから作成してください。");}
            using var owner=WindowsIdentity.GetCurrent();
            return new(owner.Name,owner.User?.Value??throw new UnauthorizedAccessException("OS所有者のSIDを取得できません。"));
        }
        if(OperatingSystem.IsLinux()){return new(Environment.UserName,null);}
        throw new PlatformNotSupportedException("ローカル所有者の入口はWindowsとLinuxが対象です。");
    }
    /// <summary>最初のフレームを読み終えたCurrentUserOnlyの接続を検査する。</summary>
    public void Verify(NamedPipeServerStream pipe){
        if(OperatingSystem.IsWindows()){this.VerifyWindows(pipe);return;}
        if(OperatingSystem.IsLinux()){
            if(string.IsNullOrEmpty(pipe.GetImpersonationUserName())){throw new UnauthorizedAccessException("IPCのOS接続元を取得できません。");}
            return;
        }
        throw new PlatformNotSupportedException();
    }
    [SupportedOSPlatform("windows")]
    private void VerifyWindows(NamedPipeServerStream pipe){
        string? peerSid=null;
        pipe.RunAsClient(()=>{using var peer=WindowsIdentity.GetCurrent(true);peerSid=peer?.User?.Value;});
        if(peerSid is null||peerSid!=this.windowsSid){throw new UnauthorizedAccessException("IPCの接続元はサーバー所有者と異なります。");}
    }
}
