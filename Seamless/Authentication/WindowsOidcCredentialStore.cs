using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Seamless.Authentication;

/// <summary>現在のWindows利用者の資格情報マネージャー。独自の暗号・平文ファイルを使わない。</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsOidcCredentialStore:IOidcSessionCredentialStore{
    private static readonly SemaphoreSlim Workers=new(4,4);
    private const uint Generic=1,LocalMachine=2;
    private const int NotFound=1168,MaximumBytes=2560;
    private static readonly UTF8Encoding Utf8=new(false,true);
    public async Task<T> WithLockAsync<T>(OidcCredentialKey key,Func<CancellationToken,Task<T>> action,CancellationToken cancellationToken=default){
        EnsureWindows();ArgumentNullException.ThrowIfNull(action);var target=key.Target();await Workers.WaitAsync(cancellationToken).ConfigureAwait(false);
        try{
            return await Task.Factory.StartNew(()=>{
                // Mutexはスレッドへ所有権を持たせるため、取得から解放まで同じ専用スレッドに置く。
                var name="Seamless.OIDC."+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(target)));
                using var mutex=new Mutex(false,name,new NamedWaitHandleOptions{CurrentUserOnly=true,CurrentSessionOnly=false});var owned=false;
                try{
                    try{owned=WaitHandle.WaitAny([mutex,cancellationToken.WaitHandle],TimeSpan.FromSeconds(30))==0;}catch(AbandonedMutexException){owned=true;}
                    cancellationToken.ThrowIfCancellationRequested();if(!owned){throw new OidcAuthenticationException("CredentialStoreBusy","Another process is updating this saved sign-in.");}
                    return action(cancellationToken).GetAwaiter().GetResult();
                }finally{if(owned){mutex.ReleaseMutex();}}
            },CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default).ConfigureAwait(false);
        }finally{Workers.Release();}
    }
    public string? Read(OidcCredentialKey key){
        EnsureWindows();var target=key.Target();
        if(!CredRead(target,Generic,0,out var pointer)){var error=Marshal.GetLastWin32Error();if(error==NotFound){return null;}throw Unavailable(error);}
        byte[]? bytes=null;var blob=IntPtr.Zero;
        try{
            var credential=Marshal.PtrToStructure<Credential>(pointer);
            if(credential.Type!=Generic||credential.CredentialBlobSize is 0 or >MaximumBytes||credential.CredentialBlob==IntPtr.Zero){throw new OidcAuthenticationException("CredentialStoreInvalid","The saved credential does not have a supported format.");}
            bytes=new byte[credential.CredentialBlobSize];blob=credential.CredentialBlob;Marshal.Copy(blob,bytes,0,bytes.Length);return Utf8.GetString(bytes);
        }catch(DecoderFallbackException){throw new OidcAuthenticationException("CredentialStoreInvalid","The saved credential encoding is invalid.");}
        finally{if(bytes is not null){CryptographicOperations.ZeroMemory(bytes);if(blob!=IntPtr.Zero){Marshal.Copy(bytes,0,blob,bytes.Length);}}CredFree(pointer);}
    }
    public void Save(OidcCredentialKey key,string refreshToken){
        EnsureWindows();var target=key.Target();if(string.IsNullOrWhiteSpace(refreshToken)||refreshToken.Any(char.IsControl)||Utf8.GetByteCount(refreshToken)>MaximumBytes){throw new OidcAuthenticationException("CredentialTooLargeOrInvalid","The refresh credential does not fit the supported OS credential store format.");}
        var bytes=Utf8.GetBytes(refreshToken);var pointer=IntPtr.Zero;
        try{
            pointer=Marshal.AllocCoTaskMem(bytes.Length);Marshal.Copy(bytes,0,pointer,bytes.Length);
            var credential=new Credential{Type=Generic,TargetName=target,CredentialBlobSize=(uint)bytes.Length,CredentialBlob=pointer,Persist=LocalMachine,UserName="Seamless OIDC"};
            if(!CredWrite(ref credential,0)){throw Unavailable(Marshal.GetLastWin32Error());}
        }finally{CryptographicOperations.ZeroMemory(bytes);if(pointer!=IntPtr.Zero){Marshal.Copy(bytes,0,pointer,bytes.Length);Marshal.FreeCoTaskMem(pointer);}}
    }
    public void Delete(OidcCredentialKey key){
        EnsureWindows();if(!CredDelete(key.Target(),Generic,0)){var error=Marshal.GetLastWin32Error();if(error!=NotFound){throw Unavailable(error);}}
    }
    private static void EnsureWindows(){if(!OperatingSystem.IsWindows()){throw new PlatformNotSupportedException("This credential store requires Windows Credential Manager.");}}
    private static OidcAuthenticationException Unavailable(int error)=>new("CredentialStoreUnavailable","Windows Credential Manager failed ("+error+").");
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct Credential{
        public uint Flags,Type;
        [MarshalAs(UnmanagedType.LPWStr)]public string? TargetName;
        [MarshalAs(UnmanagedType.LPWStr)]public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist,AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)]public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)]public string? UserName;
    }
    [DllImport("advapi32.dll",EntryPoint="CredReadW",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]private static extern bool CredRead(string target,uint type,uint flags,out IntPtr credential);
    [DllImport("advapi32.dll",EntryPoint="CredWriteW",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]private static extern bool CredWrite(ref Credential credential,uint flags);
    [DllImport("advapi32.dll",EntryPoint="CredDeleteW",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]private static extern bool CredDelete(string target,uint type,uint flags);
    [DllImport("advapi32.dll",EntryPoint="CredFree")]private static extern void CredFree(IntPtr credential);
}
