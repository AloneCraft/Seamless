using System.Globalization;
using System.Text;
using Seamless.Collections;

namespace Database.Verification;

public sealed record RowComparison(bool Equal,long ExpectedRows,long ActualRows,string?Reason=null);

/// <summary>比較専用の有界な外部ソート。変換・DB構築のライブラリからは参照しない。</summary>
public sealed class RowBagVerifier{
    public async Task<RowComparison>CompareOrderedAsync(IAsyncEnumerable<object?[]>expected,IAsyncEnumerable<object?[]>actual,long maximumRowBytes=1L<<20,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(expected);ArgumentNullException.ThrowIfNull(actual);
        if(maximumRowBytes<128){throw new ArgumentOutOfRangeException(nameof(maximumRowBytes));}
        await using var left=expected.GetAsyncEnumerator(cancellationToken);await using var right=actual.GetAsyncEnumerator(cancellationToken);
        long expectedRows=0,actualRows=0;var equal=true;
        while(true){
            var a=await left.MoveNextAsync().ConfigureAwait(false);var b=await right.MoveNextAsync().ConfigureAwait(false);
            if(!a&&!b){break;}
            if(a){expectedRows=checked(expectedRows+1);}if(b){actualRows=checked(actualRows+1);}
            if(a!=b){equal=false;}
            else if(Encode(left.Current,maximumRowBytes)!=Encode(right.Current,maximumRowBytes)){equal=false;}
        }
        if(equal){return new(true,expectedRows,actualRows);}
        return new(false,expectedRows,actualRows,"Ordered row values or row counts differ.");
    }

    public async Task<RowComparison>CompareAsync(IAsyncEnumerable<object?[]>expected,IAsyncEnumerable<object?[]>actual,string temporaryRoot,long memoryBytes=4L<<20,long temporaryBytes=1L<<30,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(expected);ArgumentNullException.ThrowIfNull(actual);
        var root=Path.GetFullPath(temporaryRoot);if(string.Equals(Path.GetPathRoot(root),@"C:\",StringComparison.OrdinalIgnoreCase)){throw new ArgumentException("Verification temporary files must not use C drive.");}
        if(memoryBytes<4096||temporaryBytes<memoryBytes){throw new ArgumentOutOfRangeException(nameof(memoryBytes));}
        var directory=Path.Combine(root,"row-bag-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var budget=new Budget(temporaryBytes,directory);
        try{
            var left=await Sort(expected,"left",budget,memoryBytes,cancellationToken).ConfigureAwait(false);
            var right=await Sort(actual,"right",budget,memoryBytes,cancellationToken).ConfigureAwait(false);
            if(left.Rows!=right.Rows){return new(false,left.Rows,right.Rows,"Row multiplicities differ.");}
            using var a=File.OpenText(left.Path);using var b=File.OpenText(right.Path);
            while(true){cancellationToken.ThrowIfCancellationRequested();var x=await a.ReadLineAsync(cancellationToken).ConfigureAwait(false);var y=await b.ReadLineAsync(cancellationToken).ConfigureAwait(false);if(x!=y){return new(false,left.Rows,right.Rows,"A row value differs.");}if(x is null){return new(true,left.Rows,right.Rows);}}
        }finally{
            foreach(var file in Directory.EnumerateFiles(directory)){budget.Delete(file);}
            Directory.Delete(directory);
        }
    }

    private static async Task<(string Path,long Rows)>Sort(IAsyncEnumerable<object?[]>rows,string prefix,Budget budget,long memory,CancellationToken cancellationToken){
        var buffer=new List<string>();var runs=new List<string?>();long used=0,count=0;
        async Task Flush(){
            buffer.Sort(StringComparer.Ordinal);var path=budget.NewPath(prefix);
            await using(var writer=new StreamWriter(path,false,new UTF8Encoding(false)){NewLine="\n"}){
                foreach(var line in buffer){budget.Reserve(line.Length+1);await writer.WriteLineAsync(line.AsMemory(),cancellationToken).ConfigureAwait(false);}
            }
            buffer.Clear();used=0;
            var level=0;
            // 完了したrunを逐次併合し、ファイル名の一覧も全件数に比例して増やさない。
            while(level<runs.Count&&runs[level] is{}previous){path=await Merge(previous,path,prefix,budget,cancellationToken).ConfigureAwait(false);runs[level]=null;level++;}
            if(level==runs.Count){runs.Add(path);}else{runs[level]=path;}
        }
        await foreach(var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false)){
            var line=Encode(row,memory/8);var bytes=checked((long)line.Length*2+128);
            if(bytes>memory){throw new InvalidDataException("A row exceeds the verification memory budget.");}
            if(used+bytes>memory&&buffer.Count>0){await Flush().ConfigureAwait(false);}
            buffer.Add(line);used+=bytes;count=checked(count+1);
        }
        if(buffer.Count>0||runs.Count==0){await Flush().ConfigureAwait(false);}
        string?result=null;
        foreach(var path in runs.OfType<string>()){
            if(result is null){result=path;}else{result=await Merge(result,path,prefix,budget,cancellationToken).ConfigureAwait(false);}
        }
        return(result!,count);
    }

    private static async Task<string>Merge(string first,string second,string prefix,Budget budget,CancellationToken cancellationToken){
        var path=budget.NewPath(prefix);using var left=File.OpenText(first);using var right=File.OpenText(second);
        await using(var writer=new StreamWriter(path,false,new UTF8Encoding(false)){NewLine="\n"}){
            var a=await left.ReadLineAsync(cancellationToken).ConfigureAwait(false);var b=await right.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            while(a is not null||b is not null){
                cancellationToken.ThrowIfCancellationRequested();string line;
                if(b is null||(a is not null&&StringComparer.Ordinal.Compare(a,b)<=0)){line=a!;a=await left.ReadLineAsync(cancellationToken).ConfigureAwait(false);}else{line=b;b=await right.ReadLineAsync(cancellationToken).ConfigureAwait(false);}
                budget.Reserve(line.Length+1);await writer.WriteLineAsync(line.AsMemory(),cancellationToken).ConfigureAwait(false);
            }
        }
        left.Dispose();right.Dispose();budget.Delete(first);budget.Delete(second);
        return path;
    }

    private static string Encode(object?[]row,long limit){
        long size=4;
        foreach(var value in row){
            size=checked(size+(value switch{string text=>5L+2L*text.Length,IFixedText text=>5L+2L*text.Length,byte[]bytes=>5L+bytes.Length,_=>128L}));
            if(size>limit){throw new InvalidDataException("A row exceeds the verification memory budget.");}
        }
        using var stream=new MemoryStream();using var writer=new BinaryWriter(stream,Encoding.UTF8,true);
        writer.Write(row.Length);
        foreach(var original in row){
            object?value=original;if(value is DBNull){value=null;}
            if(value is IFixedText text){var characters=new char[text.Length];text.CopyTo(characters);value=new string(characters);}
            switch(value){
                case null:writer.Write((byte)0);break;
                case bool boolean:writer.Write((byte)1);writer.Write(boolean);break;
                case byte or sbyte or short or ushort or int or uint or long or ulong or decimal:
                    writer.Write((byte)2);writer.Write(Convert.ToDecimal(value,CultureInfo.InvariantCulture).ToString("G29",CultureInfo.InvariantCulture));break;
                case string characters:writer.Write((byte)3);writer.Write(characters.Length);foreach(var character in characters){writer.Write((ushort)character);}break;
                case byte[]bytes:writer.Write((byte)4);writer.Write(bytes.Length);writer.Write(bytes);break;
                case Guid guid:writer.Write((byte)5);writer.Write(guid.ToByteArray());break;
                case DateTime date:writer.Write((byte)6);writer.Write(date.Ticks);writer.Write((byte)date.Kind);break;
                default:throw new NotSupportedException("Exact verification encoding is not implemented for "+value.GetType());
            }
        }
        writer.Flush();return Convert.ToBase64String(stream.GetBuffer(),0,checked((int)stream.Length));
    }

    private sealed class Budget(long limit,string directory){
        private long used;
        public string NewPath(string prefix)=>Path.Combine(directory,prefix+"-"+Guid.NewGuid().ToString("N")+".rows");
        public void Reserve(long bytes){if(bytes>limit-this.used){throw new IOException("Verification temporary storage budget exceeded.");}this.used+=bytes;}
        public void Delete(string path){
            var full=Path.GetFullPath(path);if(Path.GetDirectoryName(full)!=directory){throw new IOException("Refusing to delete an unowned verification file.");}
            if(File.Exists(full)){this.used-=new FileInfo(full).Length;File.Delete(full);}
        }
    }
}
