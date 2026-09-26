using Seamless.Execution;
namespace Seamless.Collections.Persisted.Work;

/// <summary>保存済み世代を保持する。生データの形式には依存しない。</summary>
internal sealed class WorkSnapshotFiles:IDisposable{
    private const int BufferSize=4096;
    private readonly ResourceBudget budget;
    private readonly string root;
    private readonly string componentDirectory;
    private readonly ResourceReservation disk;
    private bool disposed;

    internal WorkSnapshotFiles(ResourceBudget budget,string componentDirectory){
        this.budget=budget;
        this.componentDirectory=Path.GetFullPath(componentDirectory)+Path.DirectorySeparatorChar;
        this.root=Path.Combine(Path.GetFullPath(componentDirectory),"snapshots");
        long bytes=0;
        if(Directory.Exists(this.root)){
            foreach(var file in Directory.EnumerateFiles(this.root,"*",SearchOption.AllDirectories)){
                bytes=checked(bytes+new FileInfo(file).Length);
            }
        }
        this.disk=budget.ReserveTemporaryStorage(bytes);
    }

    internal string Capture(IReadOnlyList<string> sourcePaths){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        ArgumentNullException.ThrowIfNull(sourcePaths);
        if(sourcePaths.Count==0){
            throw new ArgumentException("A snapshot must contain at least one file.",nameof(sourcePaths));
        }
        long length=0;
        foreach(var path in sourcePaths){
            if(!Path.GetFullPath(path).StartsWith(this.componentDirectory,StringComparison.OrdinalIgnoreCase)){
                throw new ArgumentException("Snapshot sources must belong to the component directory.",nameof(sourcePaths));
            }
            length=checked(length+new FileInfo(path).Length);
        }
        using var memory=this.budget.ReserveMemory(BufferSize);
        var buffer=new byte[BufferSize];
        var original=this.disk.Bytes;
        this.disk.Resize(checked(original+length));
        var id=Guid.NewGuid().ToString("N");
        var directory=this.GetDirectory(id);
        try{
            Directory.CreateDirectory(directory);
            for(var index=0;index<sourcePaths.Count;index++){
                Copy(sourcePaths[index],Path.Combine(directory,index.ToString(System.Globalization.CultureInfo.InvariantCulture)+".dat"),buffer);
            }
            return id;
        }catch{
            try{
                if(Directory.Exists(directory)){
                    Directory.Delete(directory,true);
                }
            }finally{
                this.disk.Resize(original);
            }
            throw;
        }
    }

    internal string[] GetPaths(string snapshotId,int expectedCount){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedCount);
        var directory=this.GetDirectory(snapshotId);
        if(!Directory.Exists(directory)){
            throw new InvalidDataException("The work collection snapshot is missing.");
        }
        var paths=new string[expectedCount];
        for(var index=0;index<paths.Length;index++){
            var path=Path.Combine(directory,index.ToString(System.Globalization.CultureInfo.InvariantCulture)+".dat");
            if(!File.Exists(path)){
                throw new InvalidDataException("The work collection snapshot is incomplete.");
            }
            paths[index]=path;
        }
        if(Directory.EnumerateFiles(directory).Count()!=expectedCount){
            throw new InvalidDataException("The work collection snapshot has an unexpected file count.");
        }
        return paths;
    }

    private string GetDirectory(string snapshotId){
        if(!Guid.TryParseExact(snapshotId,"N",out var id)||!string.Equals(id.ToString("N"),snapshotId,StringComparison.Ordinal)){
            throw new InvalidDataException("The work collection snapshot identifier is invalid.");
        }
        return Path.Combine(this.root,snapshotId);
    }

    internal static void Copy(string source,string destination,byte[] buffer){
        using var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.ReadWrite,1,FileOptions.SequentialScan);
        using var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None,1,FileOptions.SequentialScan);
        var remaining=input.Length;
        while(remaining>0){
            var read=input.Read(buffer,0,(int)Math.Min(buffer.Length,remaining));
            if(read==0){
                throw new EndOfStreamException("The source file ended before the snapshot was complete.");
            }
            output.Write(buffer,0,read);
            remaining-=read;
        }
        output.Flush(flushToDisk:true);
    }

    public void Dispose(){
        if(this.disposed){
            return;
        }
        this.disposed=true;
        this.disk.Dispose();
    }
}
