using System.Runtime.InteropServices;
using System.Text;

namespace Seamless.Management.Server;

/// <summary>外部DB のパスワードの置き場所。Manager を動かす機械の Windows 資格情報マネージャー(外部DBの取り込みの決定2)。</summary>
internal interface IExternalCredentialStore{
    string? Read(string target);
    void Write(string target,string userName,string password);
}

/// <summary>
/// Windows 資格情報マネージャー(汎用の資格情報)。名前は「Seamless.Manager/製品/サーバー/利用者」。
/// 保存は Manager を動かしている Windows の利用者のもので、ほかの Windows の利用者からは読めない。
/// </summary>
internal sealed partial class WindowsCredentialStore:IExternalCredentialStore{
    private const int GenericCredential=1;
    private const int PersistLocalMachine=2;
    private const int NotFound=1168;

    public string? Read(string target){
        if(!OperatingSystem.IsWindows()){throw new ManagementException("Unsupported","パスワードの保存は Windows の資格情報マネージャーだけに対応しています。");}
        if(!CredRead(target,GenericCredential,0,out var pointer)){
            var error=Marshal.GetLastPInvokeError();
            if(error==NotFound){return null;}
            throw new ManagementException("Credential","資格情報マネージャーを読めません(エラー "+error+")。");
        }
        try{
            var credential=Marshal.PtrToStructure<Credential>(pointer);
            if(credential.CredentialBlobSize==0||credential.CredentialBlob==IntPtr.Zero){return "";}
            return Marshal.PtrToStringUni(credential.CredentialBlob,checked((int)credential.CredentialBlobSize/2));
        }finally{CredFree(pointer);}
    }

    public void Write(string target,string userName,string password){
        if(!OperatingSystem.IsWindows()){throw new ManagementException("Unsupported","パスワードの保存は Windows の資格情報マネージャーだけに対応しています。");}
        var bytes=Encoding.Unicode.GetBytes(password);
        var blob=Marshal.AllocHGlobal(Math.Max(1,bytes.Length));
        var targetName=Marshal.StringToHGlobalUni(target);var user=Marshal.StringToHGlobalUni(userName);
        try{
            Marshal.Copy(bytes,0,blob,bytes.Length);
            var credential=new Credential{Type=GenericCredential,TargetName=targetName,CredentialBlobSize=(uint)bytes.Length,CredentialBlob=blob,Persist=PersistLocalMachine,UserName=user};
            if(!CredWrite(ref credential,0)){throw new ManagementException("Credential","資格情報マネージャーへ保存できません(エラー "+Marshal.GetLastPInvokeError()+")。");}
        }finally{
            // パスワードの写しを残さない。
            Marshal.Copy(new byte[bytes.Length],0,blob,bytes.Length);Array.Clear(bytes);
            Marshal.FreeHGlobal(blob);Marshal.FreeHGlobal(targetName);Marshal.FreeHGlobal(user);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Credential{
        public uint Flags;public uint Type;public IntPtr TargetName;public IntPtr Comment;public long LastWritten;
        public uint CredentialBlobSize;public IntPtr CredentialBlob;public uint Persist;public uint AttributeCount;public IntPtr Attributes;public IntPtr TargetAlias;public IntPtr UserName;
    }
    [LibraryImport("advapi32.dll",EntryPoint="CredReadW",SetLastError=true,StringMarshalling=StringMarshalling.Utf16)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredRead(string target,int type,int flags,out IntPtr credential);
    [LibraryImport("advapi32.dll",EntryPoint="CredWriteW",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredWrite(ref Credential credential,int flags);
    [LibraryImport("advapi32.dll",EntryPoint="CredFree")]
    private static partial void CredFree(IntPtr buffer);
}
