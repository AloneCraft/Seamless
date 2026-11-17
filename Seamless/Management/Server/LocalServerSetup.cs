using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text.Json;

namespace Seamless.Management.Server;

/// <summary>利用者が明示実行した新規ローカル領域だけを初期化する。</summary>
public static class LocalServerSetup{
    public static void Initialize(string root,string admin,string password,string configuration){
        root=Path.GetFullPath(root);configuration=Path.GetFullPath(configuration);
        if(File.Exists(configuration)){throw new IOException("起動設定が既にあります。既存の設定を使用するか、別の設定先を指定してください。");}
        ManagementStore.ValidateAccountName(admin);if(password.Length<12){throw new ManagementException("Validation","パスワードは12文字以上にしてください。");}
        if(Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any()){throw new IOException("既存の管理領域は初期化できません。新しい空の専用領域を指定してください。");}
        using var key=RSA.Create(2048);var request=new CertificateRequest("CN=localhost",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        var san=new SubjectAlternativeNameBuilder();san.AddDnsName("localhost");san.AddIpAddress(System.Net.IPAddress.Loopback);request.CertificateExtensions.Add(san.Build());request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));
        using var certificate=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),DateTimeOffset.UtcNow.AddYears(1));
        Directory.CreateDirectory(root);
        if(OperatingSystem.IsWindows()){
            var security=new DirectorySecurity();var identity=WindowsIdentity.GetCurrent().User??throw new IOException("利用者を特定できません。");security.SetAccessRuleProtection(true,false);
            foreach(var sid in new[]{identity,new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null),new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null)}){security.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));}
            new DirectoryInfo(root).SetAccessControl(security);
        }else{File.SetUnixFileMode(root,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);}
        using(var store=ManagementStore.Initialize(root,admin,password)){}
        var path=Path.Combine(root,"server.pfx");using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(certificate.Export(X509ContentType.Pfx));file.Flush(true);}
        if(!OperatingSystem.IsWindows()){File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite);}
        Register(root,path,configuration,admin);
    }
    public static void Register(string root,string certificate,string configuration,string? localUser=null){
        if(localUser is not null){ManagementStore.ValidateAccountName(localUser);}
        root=Path.GetFullPath(root);certificate=Path.GetFullPath(certificate);configuration=Path.GetFullPath(configuration);
        if(File.Exists(configuration)){throw new IOException("起動設定が既にあります。上書きは行いません。");}
        if(!File.Exists(Path.Combine(root,"management.format"))){throw new IOException("初期化済みの管理領域を指定してください。");}
        if(!File.Exists(certificate)){throw new IOException("サーバー証明書がありません。");}
        Directory.CreateDirectory(Path.GetDirectoryName(configuration)!);
        using var file=new FileStream(configuration,FileMode.CreateNew,FileAccess.Write,FileShare.None);JsonSerializer.Serialize(file,new ManagementLaunchConfiguration(root,certificate,LocalUser:localUser),ManagementJson.Options);file.Flush(true);
    }
}
