using Benchmark.Tpc.Core;
namespace Benchmark.Tpc.Tests;
public sealed class RedoLogFormat{
    private static RedoTransaction Sample(long sequence)=>new(sequence,[
        new RedoEntry(3,WriteKind.Insert,[1,2,3,4],[9,9]),
        new RedoEntry(5,WriteKind.Delete,[7],null),
    ]);
    [Fact,Trait("Category","Unit")]public void RoundTrip(){
        {
            //符号化して復号すると元に戻る(削除の行なしも保たれる)
            var frame=Server.RedoLogFormat.Encode(Sample(42));
            Assert.True(Server.RedoLogFormat.TryDecode(frame,out var decoded,out var consumed));
            Assert.Equal(frame.Length,consumed);
            Assert.Equal(42,decoded!.Sequence);
            Assert.Equal(2,decoded.Entries.Count);
            Assert.Equal(WriteKind.Insert,decoded.Entries[0].Kind);
            Assert.Equal(new byte[]{1,2,3,4},decoded.Entries[0].Key);
            Assert.Equal(new byte[]{9,9},decoded.Entries[0].Row);
            Assert.Null(decoded.Entries[1].Row);
        }
    }
    [Fact,Trait("Category","Unit")]public void RejectsDamagedTail(){
        {
            //途中で切れた記録は読まない
            var frame=Server.RedoLogFormat.Encode(Sample(1));
            for(var cut=0;cut<frame.Length;cut++){
                Assert.False(Server.RedoLogFormat.TryDecode(frame.AsSpan(0,cut),out _,out _),$"cut={cut}");
            }
        }
        {
            //中身の1バイトが壊れた記録は読まない(CRC)
            var frame=Server.RedoLogFormat.Encode(Sample(1));
            frame[^1]^=0xFF;
            Assert.False(Server.RedoLogFormat.TryDecode(frame,out _,out _));
        }
    }
}
