using Seamless.Management.Server;

namespace Management.Tests;

public sealed class ManagementPlacementTests{
    [Fact]
    public void WindowsFolderShortcutIsLimitedToThePlacementEntrances(){
        if(!OperatingSystem.IsWindows()){return;}
        var work=Path.GetFullPath(Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"shortcut-"+Guid.NewGuid().ToString("N")));
        var root=Path.Combine(work,"management");
        var destination=Path.Combine(work,"backup-target");
        var entry=Path.Combine(root,"backups");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(destination);

        var type=Type.GetTypeFromProgID("WScript.Shell",throwOnError:true)!;
        dynamic shell=Activator.CreateInstance(type)!;
        try{
            dynamic shortcut=shell.CreateShortcut(entry+".lnk");
            try{shortcut.TargetPath=destination;shortcut.Save();}
            finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);}
        }finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}

        Assert.Equal(destination,ManagementPlacement.DirectoryPath(entry,allowShortcut:true));
        Assert.Throws<IOException>(()=>ManagementPlacement.DirectoryPath(entry));
        Directory.CreateDirectory(entry);
        Assert.Throws<IOException>(()=>ManagementPlacement.DirectoryPath(entry,allowShortcut:true));
    }

    [Fact]
    public void OfflineBackupCopiesTheContentsOfLinkedDirectories(){
        var work=Path.GetFullPath(Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"placement-"+Guid.NewGuid().ToString("N")));
        var external=Path.GetFullPath(Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TABLE_STORAGE_TEST_ROOT")??work,"placement-"+Guid.NewGuid().ToString("N")));
        var root=Path.Combine(work,"management");
        var generation=Path.Combine(external,"db1","v000004");
        var data=Path.Combine(external,"data");
        var wal=Path.Combine(external,"wal");
        var backup=Path.Combine(work,"backup");
        Directory.CreateDirectory(Path.Combine(root,"generations"));
        Directory.CreateDirectory(generation);
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(wal);
        File.WriteAllText(Path.Combine(root,"management.format"),"7");
        File.WriteAllText(Path.Combine(wal,"segment-0.wal"),"example");
        ManagementPlacement.CreateDirectoryLink(Path.Combine(root,"generations","db1"),Path.GetDirectoryName(generation)!);
        ManagementPlacement.CreateDirectoryLink(Path.Combine(generation,"data"),data);
        ManagementPlacement.CreateDirectoryLink(Path.Combine(data,"wal-v2"),wal);
        ManagementPlacement.SealData(generation);
        ManagementPlacement.CheckData(generation);

        var actual=ManagementPlacement.GenerationPath(root,Path.Combine("generations","db1","v000004"));
        Assert.Equal(generation,actual);
        Seamless.Management.Server.Program.CopyLocked(root,backup);
        var backedUpWal=Path.Combine(backup,"generations","db1","v000004","data","wal-v2");
        Assert.Null(new DirectoryInfo(backedUpWal).LinkTarget);
        Assert.Equal("example",File.ReadAllText(Path.Combine(backedUpWal,"segment-0.wal")));
        ManagementPlacement.CheckData(Path.Combine(backup,"generations","db1","v000004"));

        var next=Path.Combine(Path.GetDirectoryName(generation)!,"v000005");
        Directory.CreateDirectory(next);
        ManagementPlacement.InheritData(generation,next);
        ManagementPlacement.SealData(next);
        var nextData=ManagementPlacement.DirectoryPath(Path.Combine(next,"data"));
        var nextWal=ManagementPlacement.DirectoryPath(Path.Combine(nextData,"wal-v2"));
        Assert.NotEqual(data,nextData);
        Assert.Equal(Path.GetDirectoryName(wal),Path.GetDirectoryName(nextWal));
        Assert.NotEqual(wal,nextWal);
        Assert.False(File.Exists(Path.Combine(nextWal,"segment-0.wal")));

        Directory.Move(wal,wal+"-offline");
        Assert.Throws<DirectoryNotFoundException>(()=>ManagementPlacement.CheckData(actual));
        Assert.False(Directory.Exists(wal));
    }
}
