namespace Benchmark.Scalable.Core;

/// <summary>固定長チャンクを整列し、二分マージする。常駐する入力はチャンク1個と各ファイルのバッファだけ。</summary>
internal static class ExternalSort{
    public static long Verify(IEnumerable<int> values,ScalableRequest request,CancellationToken cancellationToken,out long diskBytes,Benchmark.Infrastructure.ContentDigest content,Seamless.Execution.ResourceBudget parentBudget){
        if(request.Count==0){diskBytes=0;return 0;}
        if(checked(request.Count*12+request.ChunkSize*8L)>request.DiskLimitBytes){throw new InvalidOperationException("External sort exceeds the disk budget.");}
        if(request.ChunkSize*8L+65536>request.MemoryLimitBytes){throw new InvalidOperationException("Sort buffer and IO streams exceed the remaining memory budget.");}
        var budget=parentBudget.CreateChild(request.MemoryLimitBytes,request.DiskLimitBytes);
        using var memoryReservation=budget.ReserveMemory(request.ChunkSize*8L+65536);
        using var diskReservation=budget.ReserveTemporaryStorage(checked(request.Count*12+request.ChunkSize*8L));
        var directory=Path.Combine(request.TemporaryDirectory??Path.Combine(Path.GetTempPath(),"Seamless"),"sort-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var levels=new string?[64];var buffer=new int[request.ChunkSize];var size=0;var sequence=0;long peak=0;
        void ObserveDisk()=>peak=Math.Max(peak,Directory.EnumerateFiles(directory).Sum(path=>new FileInfo(path).Length));
        string NewPath()=>Path.Combine(directory,(sequence++).ToString()+".bin");
        void StoreChunk(){
            Array.Sort(buffer,0,size);var path=NewPath();
            using(var output=new BinaryWriter(File.Create(path))){for(var index=0;index<size;index++){output.Write(buffer[index]);}}
            ObserveDisk();
            var level=0;
            while(levels[level] is string old){var next=NewPath();Merge(old,path,next,request.ChunkSize,cancellationToken,ObserveDisk);levels[level]=null;path=next;level++;}
            levels[level]=path;size=0;
        }
        try{
            foreach(var value in values){buffer[size++]=value;if(size==buffer.Length){cancellationToken.ThrowIfCancellationRequested();Benchmark.Infrastructure.SystemResources.Check();StoreChunk();}}
            if(size!=0){StoreChunk();}
            string? result=null;
            foreach(var path in levels){
                if(path is null){continue;}
                if(result is null){result=path;}
                else{var next=NewPath();Merge(result,path,next,request.ChunkSize,cancellationToken,ObserveDisk);result=next;}
            }
            long count=0,sum=0;var previous=int.MinValue;
            if(result is not null){
                using var input=new BinaryReader(File.OpenRead(result));
                while(input.BaseStream.Position<input.BaseStream.Length){
                    if(count%request.ChunkSize==0){cancellationToken.ThrowIfCancellationRequested();Benchmark.Infrastructure.SystemResources.Check();}
                    var value=input.ReadInt32();if(value<previous){throw new InvalidDataException("External merge order mismatch.");}previous=value;count++;sum+=value;content.Add(value);
                }
            }
            if(count!=request.Count){throw new InvalidDataException("External merge count mismatch.");}
            diskBytes=peak;return sum;
        }finally{Directory.Delete(directory,true);}
    }
    private static void Merge(string first,string second,string output,int chunk,CancellationToken cancellationToken,Action observeDisk){
        using(var left=new BinaryReader(File.OpenRead(first)))
        using(var right=new BinaryReader(File.OpenRead(second)))
        using(var target=new BinaryWriter(File.Create(output))){
            var leftPresent=Read(left,out var a);var rightPresent=Read(right,out var b);long count=0;
            while(leftPresent||rightPresent){
                if(count++%chunk==0){cancellationToken.ThrowIfCancellationRequested();Benchmark.Infrastructure.SystemResources.Check();}
                if(leftPresent&&(!rightPresent||a<=b)){target.Write(a);leftPresent=Read(left,out a);}
                else{target.Write(b);rightPresent=Read(right,out b);}
            }
        }
        observeDisk();File.Delete(first);File.Delete(second);
    }
    private static bool Read(BinaryReader reader,out int value){
        value=0;if(reader.BaseStream.Position==reader.BaseStream.Length){return false;}
        value=reader.ReadInt32();return true;
    }
}
