using Seamless.Collections;

namespace Database.Compilation.Tests;

public sealed class CodecRegistry{
    private readonly record struct Row(int Id,FixedText<TextBuffer32>Name,FixedText<TextBuffer32>?Optional);

    [Fact]public void Get(){
        var registry=new global::Seamless.Execution.CodecRegistry();
        var codec=registry.Get<Row>();
        foreach(var row in new[]{new Row(1,"abcdefghijklmnopqrstuvwxyz日本😀",null),new Row(2,"",new FixedText<TextBuffer32>("a\0b"))}){
            var bytes=new byte[codec.GetEncodedLength(row)];
            codec.Encode(row,bytes);
            Assert.Equal(row,codec.Decode(bytes));
        }
    }

    [Fact]public void CreateList(){
        var path=Path.Combine(Path.GetTempPath(),"SeamlessRoundtripTests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        var file=Path.Combine(path,"strings");
        using(var list=global::Seamless.Collections.Persisted.Store.CreateList<FixedText<TextBuffer32>>(file)){
            list.Add("abcdefghijklmnopqrstuvwxyz日本😀");
            list.Add("");
        }
        using(var list=global::Seamless.Collections.Persisted.Store.CreateList<FixedText<TextBuffer32>>(file)){
            Assert.Equal(2,list.Count);
            Assert.Equal("abcdefghijklmnopqrstuvwxyz日本😀",list[0].ToString());
            Assert.Equal("",list[1].ToString());
        }
    }
}
