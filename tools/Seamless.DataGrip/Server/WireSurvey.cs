using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace DataGrip.Stage1.Server;

internal static class WireSurvey{
    internal static void Run(){
        object?[]values=[1,"日本語",123.4567m,true,new byte[]{0,1,255},null,new DateTime(2026,11,2,12,34,56).AddTicks(1234567)];
        byte[] Binary(){using var bytes=new MemoryStream();using var writer=new BinaryWriter(bytes,Encoding.UTF8,true);foreach(var value in values){ToolsWire.Value(writer,value);}return bytes.ToArray();}
        byte[] Json()=>JsonSerializer.SerializeToUtf8Bytes(values);
        foreach(var encoder in new[]{("binary",(Func<byte[]>)Binary),("json",(Func<byte[]>)Json)}){
            for(var index=0;index<1000;index++){_=encoder.Item2();}
            var before=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();long bytes=0;
            for(var index=0;index<10000;index++){bytes+=encoder.Item2().Length;}
            timer.Stop();Console.WriteLine(JsonSerializer.Serialize(new{format=encoder.Item1,iterations=10000,bytesPerRow=bytes/10000,elapsedMilliseconds=timer.Elapsed.TotalMilliseconds,allocatedBytes=GC.GetAllocatedBytesForCurrentThread()-before}));
        }
        Console.WriteLine("Local codec survey only: JSON is untagged; this does not measure network, backpressure, JDBC, GUI, or establish a final protocol choice.");
    }
}
