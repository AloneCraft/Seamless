using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Seamless.Management.Server;

internal static class ManagementPlacement{
    internal static string DirectoryPath(string path,bool allowShortcut=false){
        var link=new DirectoryInfo(path);
        var target=link.LinkTarget;
        var shortcut=OperatingSystem.IsWindows()?path+".lnk":null;
        if(shortcut is not null&&File.Exists(shortcut)){
            if(!allowShortcut){throw new IOException("この場所ではショートカットを使用できません。"+shortcut);}
            if(target is not null||File.Exists(path)||Directory.Exists(path)){
                throw new IOException("配置が重複しています。"+path+" / "+shortcut);
            }
            SourceWorkspace.CheckLinks(shortcut);
            target=ReadShortcut(shortcut);
        }
        if(target is null){
            SourceWorkspace.CheckLinks(path);
            if(File.Exists(path)){throw new IOException("フォルダーを指定してください。"+path);}
            return path;
        }
        var resolved=Path.TrimEndingDirectorySeparator(Path.GetFullPath(target,Path.GetDirectoryName(path)!));
        SourceWorkspace.CheckLinks(resolved);
        if(!Directory.Exists(resolved)){throw new DirectoryNotFoundException("保存先に接続できません。"+path+" → "+resolved);}
        if(Overlaps(path,resolved)){throw new IOException("配置の内側または親を指すリンクは使用できません。"+path+" → "+resolved);}
        return resolved;
    }

    private static string ReadShortcut(string path){
        if(!OperatingSystem.IsWindows()){throw new PlatformNotSupportedException();}
        var type=Type.GetTypeFromProgID("WScript.Shell",throwOnError:true)!;
        dynamic shell=Activator.CreateInstance(type)!;
        try{
            dynamic shortcut=shell.CreateShortcut(path);
            try{
                string target=shortcut.TargetPath;
                if(!Path.IsPathFullyQualified(target)||target.StartsWith(@"\\",StringComparison.Ordinal)||!string.IsNullOrEmpty((string)shortcut.Arguments)){
                    throw new IOException("ローカルフォルダーを指すショートカットを指定してください。"+path);
                }
                return target;
            }finally{Marshal.FinalReleaseComObject(shortcut);}
        }finally{Marshal.FinalReleaseComObject(shell);}
    }

    internal static string GenerationPath(string root,string relative){
        var parts=relative.Split(['/', '\\'],StringSplitOptions.None);
        if(parts is not ["generations",var name,var version]||!IsEntryName(name)||
            !global::System.Text.RegularExpressions.Regex.IsMatch(version,@"\A(v[0-9]{6,19}(-[0-9a-f]{32})?|\.pending-[0-9a-f]{32})\z")){
            return Path.Combine(root,relative);
        }
        var path=Path.Combine(DirectoryPath(Path.Combine(root,"generations",name),allowShortcut:true),version);
        SourceWorkspace.CheckLinks(path);
        return path;
    }

    internal static bool IsEntryName(string name)=>name.Length>0&&name!="."&&name!=".."&&
        !name.Equals("system",StringComparison.OrdinalIgnoreCase)&&!name.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase)&&
        !name.EndsWith('.')&&!name.EndsWith(' ')&&!name.Any(c=>char.IsControl(c)||"<>:\"/\\|?*".Contains(c));

    internal static string VersionDirectory(long version)=>"v"+version.ToString("D6",global::System.Globalization.CultureInfo.InvariantCulture);

    internal static bool TryVersionDirectory(string name,out long version){
        version=0;
        return name.Length>=7&&name[0]=='v'&&long.TryParse(name.AsSpan(1),global::System.Globalization.NumberStyles.None,
            global::System.Globalization.CultureInfo.InvariantCulture,out version)&&version>0&&name==VersionDirectory(version);
    }

    private const string PlacementIdentity=".seamless-generation";

    internal static void CheckData(string generation){
        var paths=DataPaths(generation);
        if(!Directory.Exists(paths[0].Path)){throw new DirectoryNotFoundException("公開世代のデータがありません。"+paths[0].Path);}
        var identityPath=Path.Combine(generation,PlacementIdentity);
        SourceWorkspace.CheckLinks(identityPath);
        var identity=File.Exists(identityPath)?File.ReadAllText(identityPath):null;
        foreach(var (role,logical,path) in paths){
            if(identity is null&&logical==path){continue;}
            var marker=Path.Combine(path,PlacementIdentity);
            SourceWorkspace.CheckLinks(marker);
            if(identity is null||!File.Exists(marker)||File.ReadAllText(marker)!=identity+"/"+role){
                throw new IOException("公開世代と保存先の識別情報が一致しません。"+logical+" → "+path);
            }
        }
    }

    internal static void SealData(string generation){
        var paths=DataPaths(generation);
        var identity=Guid.NewGuid().ToString("N");
        foreach(var (role,_,path) in paths){
            Directory.CreateDirectory(path);
            SourceWorkspace.Atomic(Path.Combine(path,PlacementIdentity),identity+"/"+role);
        }
        SourceWorkspace.Atomic(Path.Combine(generation,PlacementIdentity),identity);
    }

    private static (string Role,string Logical,string Path)[] DataPaths(string generation){
        var data=DirectoryPath(Path.Combine(generation,"data"));
        var store=DirectoryPath(Path.Combine(data,"store-v2"));
        var wal=DirectoryPath(Path.Combine(data,"wal-v2"));
        if(Overlaps(store,wal)||Overlaps(store,generation)&&store!=Path.Combine(generation,"data","store-v2")
            ||Overlaps(wal,generation)&&wal!=Path.Combine(generation,"data","wal-v2")){
            throw new IOException("データとWALには独立した保存先を指定してください。"+generation);
        }
        return [("data",Path.Combine(generation,"data"),data),("store-v2",Path.Combine(data,"store-v2"),store),("wal-v2",Path.Combine(data,"wal-v2"),wal)];
    }

    internal static bool Overlaps(string first,string second)=>Contains(first,second)||Contains(second,first);

    private static bool Contains(string parent,string child){
        var relative=Path.GetRelativePath(parent,child);
        return !Path.IsPathRooted(relative)&&relative!=".."&&!relative.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal);
    }

    internal static bool HasDatabaseEntry(string root,DefinitionVersion version){
        var parts=version.StoragePath.Split(['/', '\\'],StringSplitOptions.None);
        string name;
        bool shortcut;
        if(parts is ["generations",var named,_]&&IsEntryName(named)){
            name=named;
            shortcut=OperatingSystem.IsWindows();
        }else if(parts is ["generations",var legacy]&&Guid.TryParseExact(legacy,"N",out _)){
            name=legacy;
            shortcut=false;
        }else{return true;}
        var path=Path.Combine(root,"generations",name);
        return Directory.Exists(path)||File.Exists(path)||new DirectoryInfo(path).LinkTarget is not null
            ||shortcut&&File.Exists(path+".lnk");
    }

    internal static void InheritData(string prior,string next){
        CheckData(prior);
        var oldData=Path.Combine(prior,"data");
        var newData=Path.Combine(next,"data");
        var oldTarget=DirectoryPath(oldData);
        if(oldTarget!=oldData){CreateNextLink(oldTarget,newData,next);}
        var newTarget=DirectoryPath(newData);
        foreach(var name in new[]{"store-v2","wal-v2"}){
            var previous=Path.Combine(oldTarget,name);
            var target=DirectoryPath(previous);
            if(target!=previous){CreateNextLink(target,Path.Combine(newTarget,name),next);}
        }
    }

    private static void CreateNextLink(string previous,string link,string generation){
        var destination=Path.Combine(Path.GetDirectoryName(previous)!,
            Path.GetFileName(previous)+"-"+Path.GetFileName(generation)+"-"+Guid.NewGuid().ToString("N"));
        SourceWorkspace.CheckLinks(destination);
        Directory.CreateDirectory(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        CreateDirectoryLink(link,destination);
    }

    internal static void CreateDirectoryLink(string link,string destination){
        if(Directory.Exists(link)||File.Exists(link)||new DirectoryInfo(link).LinkTarget is not null){
            throw new IOException("リンクの作成先は未使用の場所を指定してください。"+link);
        }
        if(!OperatingSystem.IsWindows()){
            Directory.CreateSymbolicLink(link,destination);
            return;
        }
        // 次世代の配置には、通常ユーザーでも作れるWindowsのジャンクションを使う。
        var substitute=Encoding.Unicode.GetBytes(@"\??\"+destination);
        var print=Encoding.Unicode.GetBytes(destination);
        var payload=new byte[checked(20+substitute.Length+print.Length)];
        BinaryPrimitives.WriteUInt32LittleEndian(payload,0xA0000003); // IO_REPARSE_TAG_MOUNT_POINT
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4),checked((ushort)(payload.Length-8)));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(10),checked((ushort)substitute.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(12),checked((ushort)(substitute.Length+2)));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(14),checked((ushort)print.Length));
        substitute.CopyTo(payload,16);
        print.CopyTo(payload,18+substitute.Length);
        Directory.CreateDirectory(link);
        try{
            using var directory=CreateFile(link,0x40000000,0,IntPtr.Zero,3,0x02200000,IntPtr.Zero);
            if(directory.IsInvalid||!DeviceIoControl(directory,0x000900A4,payload,payload.Length,IntPtr.Zero,0,out _,IntPtr.Zero)){
                throw new IOException("ジャンクションを作成できません。"+link,new Win32Exception(Marshal.GetLastWin32Error()));
            }
        }catch{
            if(new DirectoryInfo(link).LinkTarget is null){Directory.Delete(link);}
            throw;
        }
    }

    [DllImport("kernel32.dll",EntryPoint="CreateFileW",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern SafeFileHandle CreateFile(string file,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);

    [DllImport("kernel32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle file,uint control,byte[] input,int inputSize,IntPtr output,int outputSize,out int returned,IntPtr overlapped);
}
