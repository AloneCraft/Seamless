using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TestSeamless;

public sealed record BagComparison(bool Equal,long Rows,string? Reason=null);
public sealed record FloatingTolerance(double SingleAbsolute=1e-6,double SingleRelative=1e-5,double DoubleAbsolute=1e-12,double DoubleRelative=1e-9){
    public static FloatingTolerance FromEnvironment(){
        var json=Environment.GetEnvironmentVariable("SEAMLESS_COMPARISON_FLOAT_TOLERANCE");if(string.IsNullOrWhiteSpace(json)){return new();}return Parse(json);
    }
    public static FloatingTolerance Parse(string json){
        var result=JsonSerializer.Deserialize<FloatingTolerance>(json)??throw new InvalidDataException("Floating tolerance must be a JSON object.");
        foreach(var value in new[]{result.SingleAbsolute,result.SingleRelative,result.DoubleAbsolute,result.DoubleRelative}){if(!double.IsFinite(value)||value<0){throw new InvalidDataException("Floating tolerances must be finite nonnegative numbers.");}}return result;
    }
}

/// <summary>重複数を保持し、32MiB以下のrunと2入力のmergeで照合する。</summary>
public static class ResultBag{
    public const long MemoryBytes=32L<<20,TemporaryBytes=16L<<30;
    public static BagComparison Compare(IEnumerable<object?[]> expected,IEnumerable<object?[]> actual,string temporaryRoot,bool computedFloatingPoint,FloatingTolerance? tolerance=null,IReadOnlySet<int>? computedFloatingPointOrdinals=null){
        tolerance??=FloatingTolerance.FromEnvironment();var directory=Path.Combine(temporaryRoot,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try{
            var budget=new SpillBudget();var left=Sort(expected,Path.Combine(directory,"expected"),budget);var right=Sort(actual,Path.Combine(directory,"actual"),budget);if(computedFloatingPoint){
                var identical=true;long exactCount=0;using(var exactLeft=File.OpenText(left))using(var exactRight=File.OpenText(right)){while(true){var x=exactLeft.ReadLine();var y=exactRight.ReadLine();if(x!=y){identical=false;break;}if(x is null){break;}exactCount++;}}
                if(identical){return new(true,exactCount);}return MatchApproximate(left,right,directory,budget,tolerance,computedFloatingPointOrdinals);
            }using var a=File.OpenText(left);using var b=File.OpenText(right);long count=0;
            while(true){var x=a.ReadLine();var y=b.ReadLine();if(x is null&&y is null){return new(true,count);}if(x is null||y is null){return new(false,count,"Row multiplicities differ.");}if(!Equal(x,y,computedFloatingPoint,tolerance,computedFloatingPointOrdinals)){return new(false,count,"A row value differs.");}count++;}
        }finally{Directory.Delete(directory,true);}
    }
    public static bool CompareOrder(IEnumerable<object?[]> expected,IEnumerable<object?[]> actual,IReadOnlyList<int> keyOrdinals,bool computedFloatingPoint,IReadOnlySet<int>? computedFloatingPointOrdinals=null){
        HashSet<int>? orderComputed=null;if(computedFloatingPointOrdinals is not null){orderComputed=[];for(var index=0;index<keyOrdinals.Count;index++){if(computedFloatingPointOrdinals.Contains(keyOrdinals[index])){orderComputed.Add(index);}}}var tolerance=FloatingTolerance.FromEnvironment();using var left=expected.GetEnumerator();using var right=actual.GetEnumerator();while(true){var hasLeft=left.MoveNext();var hasRight=right.MoveNext();if(hasLeft!=hasRight){return false;}if(!hasLeft){return true;}
            if(keyOrdinals.Any(i=>i<0||i>=left.Current.Length||i>=right.Current.Length)){throw new InvalidDataException("Order key ordinal is outside the result columns.");}
            var a=JsonSerializer.Serialize(keyOrdinals.Select(i=>Encode(left.Current[i])).ToArray());var b=JsonSerializer.Serialize(keyOrdinals.Select(i=>Encode(right.Current[i])).ToArray());if(!Equal(a,b,computedFloatingPoint,tolerance,orderComputed)){return false;}
        }
    }
    private static bool Equal(string a,string b,bool computed,FloatingTolerance tolerance,IReadOnlySet<int>? computedFloatingPointOrdinals){
        if(a==b){return true;}if(!computed){return false;}var x=JsonSerializer.Deserialize<string[]>(a)!;var y=JsonSerializer.Deserialize<string[]>(b)!;if(x.Length!=y.Length){return false;}
        for(var i=0;i<x.Length;i++){
            if(x[i]==y[i]){continue;}if(computedFloatingPointOrdinals is not null&&!computedFloatingPointOrdinals.Contains(i)){return false;}if(x[i].Length==0||y[i].Length==0||x[i][0]!=y[i][0]||x[i][0] is not ('F' or 'D')){return false;}
            var left=DecodeFloat(x[i]);var right=DecodeFloat(y[i]);if(!double.IsFinite(left)||!double.IsFinite(right)){return false;}
            var absolute=tolerance.DoubleAbsolute;var relative=tolerance.DoubleRelative;if(x[i][0]=='F'){absolute=tolerance.SingleAbsolute;relative=tolerance.SingleRelative;}
            if(Math.Abs(left-right)>absolute+relative*Math.Max(Math.Abs(left),Math.Abs(right))){return false;}
        }return true;
    }
    private static double DecodeFloat(string encoded){var bits=ulong.Parse(encoded.AsSpan(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture);if((bits&0x8000000000000000UL)!=0){bits^=0x8000000000000000UL;}else{bits=~bits;}return BitConverter.Int64BitsToDouble(unchecked((long)bits));}
    private static string Float(char tag,double value){var bits=unchecked((ulong)BitConverter.DoubleToInt64Bits(value));if((bits&0x8000000000000000UL)!=0){bits=~bits;}else{bits^=0x8000000000000000UL;}return tag+bits.ToString("X16",CultureInfo.InvariantCulture);}
    private static string Encode(object? value)=>value switch{
        null or DBNull=>"N",byte[] bytes=>"B"+Convert.ToBase64String(bytes),bool bit=>"L"+Convert.ToInt32(bit).ToString(CultureInfo.InvariantCulture),
        byte or sbyte or short or ushort or int or uint or long or ulong=>"I"+Convert.ToString(value,CultureInfo.InvariantCulture),
        decimal number=>"M"+number.ToString("G29",CultureInfo.InvariantCulture),float number=>Float('F',number),double number=>Float('D',number),
        string text=>"S"+text,Guid guid=>"G"+guid.ToString("D"),DateOnly date=>"T"+date.ToDateTime(TimeOnly.MinValue).Ticks.ToString(CultureInfo.InvariantCulture),DateTime date=>"T"+date.Ticks.ToString(CultureInfo.InvariantCulture),
        TimeOnly time=>"U"+time.Ticks.ToString(CultureInfo.InvariantCulture),TimeSpan time=>"U"+time.Ticks.ToString(CultureInfo.InvariantCulture),DateTimeOffset time=>"O"+time.Ticks.ToString(CultureInfo.InvariantCulture)+"/"+time.Offset.Ticks.ToString(CultureInfo.InvariantCulture),
        _=>throw new NotSupportedException($"Result value type '{value.GetType()}' has no lossless comparison codec.")};
    private static string Sort(IEnumerable<object?[]> rows,string directory,SpillBudget budget){
        Directory.CreateDirectory(directory);var runs=new List<string>();var chunk=new List<string>();long bytes=0;
        foreach(var row in rows){long estimate=64;foreach(var value in row){estimate=checked(estimate+(value switch{string text=>text.Length*12L,byte[] binary=>binary.Length*8L,_=>256L}));}if(estimate>MemoryBytes/16){throw new InvalidDataException("Comparison row exceeds the bounded encoding budget.");}var encoded=JsonSerializer.Serialize(row.Select(Encode).ToArray());var cost=checked(encoded.Length*2L+64);if(cost>MemoryBytes/8){throw new InvalidDataException("A comparison row exceeds the memory budget.");}if(bytes+cost>MemoryBytes*3/4){Flush();}chunk.Add(encoded);bytes+=cost;}
        if(chunk.Count>0||runs.Count==0){Flush();}
        var pass=0;while(runs.Count>1){var merged=new List<string>();for(var first=0;first<runs.Count;first+=2){var group=runs.Skip(first).Take(2).ToArray();var output=Path.Combine(directory,$"merge-{pass}-{first}.jsonl");Merge(group,output,budget);foreach(var input in group){budget.Release(new FileInfo(input).Length);File.Delete(input);}merged.Add(output);}runs=merged;pass++;}return runs[0];
        void Flush(){chunk.Sort(StringComparer.Ordinal);var path=Path.Combine(directory,$"run-{runs.Count}.jsonl");using(var writer=new StreamWriter(path,false,new UTF8Encoding(false))){foreach(var line in chunk){budget.Add(Encoding.UTF8.GetByteCount(line)+Encoding.UTF8.GetByteCount(Environment.NewLine));writer.WriteLine(line);}}runs.Add(path);chunk.Clear();bytes=0;}
    }
    // 許容誤差は推移的ではないため、整列後の同順位比較ではなく完全二部マッチングを行う。
    // インデックス、対応関係、探索キューを全て一時ファイルに置き、行数に比例する常駐配列を作らない。
    private static BagComparison MatchApproximate(string left,string right,string directory,SpillBudget budget,FloatingTolerance tolerance,IReadOnlySet<int>? computedFloatingPointOrdinals){
        using var leftData=File.OpenRead(left);using var rightData=File.OpenRead(right);
        using var leftIndex=Create("left-index");using var rightIndex=Create("right-index");var leftCount=Index(leftData,leftIndex);var rightCount=Index(rightData,rightIndex);if(leftCount!=rightCount){return new(false,Math.Min(leftCount,rightCount),"Row multiplicities differ.");}
        using var matchLeft=CreateArray("match-left",leftCount,true);using var matchRight=CreateArray("match-right",rightCount,true);using var visited=CreateArray("visited",rightCount,false);using var parent=CreateArray("parent",rightCount,false);using var queue=CreateArray("queue",leftCount,false);
        for(long start=0;start<leftCount;start++){
            var epoch=start+1;long head=0,tail=0;Set(queue,tail++,start);long free=-1;
            while(head<tail&&free<0){var current=Get(queue,head++);var expected=Read(leftData,Get(leftIndex,current));
                for(long candidate=0;candidate<rightCount;candidate++){
                    if(Get(visited,candidate)==epoch){continue;}var actual=Read(rightData,Get(rightIndex,candidate));if(!Equal(expected,actual,true,tolerance,computedFloatingPointOrdinals)){continue;}
                    Set(visited,candidate,epoch);Set(parent,candidate,current);var matched=Get(matchRight,candidate);if(matched<0){free=candidate;break;}Set(queue,tail++,matched);
                }
            }
            if(free<0){return new(false,start,"No complete matching exists within the floating-point tolerance.");}
            while(free>=0){var row=Get(parent,free);var previous=Get(matchLeft,row);Set(matchLeft,row,free);Set(matchRight,free,row);free=previous;}
        }
        return new(true,leftCount);
        FileStream Create(string name)=>new(Path.Combine(directory,name),FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,4096,FileOptions.RandomAccess);
        FileStream CreateArray(string name,long count,bool negative){var stream=Create(name);try{var size=checked(count*8);budget.Add(size);stream.SetLength(size);if(negative){for(long i=0;i<count;i++){Set(stream,i,-1);}}return stream;}catch{stream.Dispose();throw;}}
        long Index(FileStream data,FileStream index){data.Position=0;var buffer=new byte[65536];long position=0,count=0;var atStart=true;int read;while((read=data.Read(buffer))>0){for(var i=0;i<read;i++){if(atStart){budget.Add(8);Set(index,count++,position);atStart=false;}position++;if(buffer[i]==10){atStart=true;}}}return count;}
        static long Get(FileStream file,long index){Span<byte> bytes=stackalloc byte[8];file.Position=checked(index*8);file.ReadExactly(bytes);return System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes);}
        static void Set(FileStream file,long index,long value){Span<byte> bytes=stackalloc byte[8];System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(bytes,value);file.Position=checked(index*8);file.Write(bytes);}
        static string Read(FileStream file,long offset){file.Position=offset;using var reader=new StreamReader(file,Encoding.UTF8,false,4096,true);return reader.ReadLine()??throw new InvalidDataException("Truncated comparison spill file.");}
    }
    private static void Merge(string[] inputs,string output,SpillBudget budget){
        var readers=inputs.Select(File.OpenText).ToArray();try{var queue=new PriorityQueue<(string Line,int Reader),string>(StringComparer.Ordinal);for(var i=0;i<readers.Length;i++){var line=readers[i].ReadLine();if(line is not null){queue.Enqueue((line,i),line);}}using var writer=new StreamWriter(output,false,new UTF8Encoding(false));while(queue.TryDequeue(out var item,out _)){budget.Add(Encoding.UTF8.GetByteCount(item.Line)+Encoding.UTF8.GetByteCount(Environment.NewLine));writer.WriteLine(item.Line);var next=readers[item.Reader].ReadLine();if(next is not null){queue.Enqueue((next,item.Reader),next);}}}finally{foreach(var reader in readers){reader.Dispose();}}
    }
    private sealed class SpillBudget{private long used;public void Add(long bytes){this.used=checked(this.used+bytes);if(this.used>TemporaryBytes){throw new IOException("Comparison temporary-storage budget exceeded.");}}public void Release(long bytes){this.used-=bytes;}}
}
