using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Seamless.Collections;
//using IntList = global::Seamless.Collections.UnmanagedList<int>;
//using StringList = global::Seamless.Collections.ManagedList<string>;

namespace Test.Seamless.Unit.Collections;

public sealed class ListTests {
    private const int UnicodeLineBytes = 4*1024;
    private static readonly Encoding Utf16NoBom = new UnicodeEncoding(bigEndian: false,byteOrderMark: false);
    private static string CreatePath([CallerMemberName] string methodName = "") {
        var directoryPath = Path.Combine(
            Environment.CurrentDirectory,
            nameof(ListTests)
        );
        Directory.CreateDirectory(directoryPath);
        return Path.Combine(directoryPath,$"{methodName}.dat");
    }

    private static string CreateFixedWidthUnicodeLine(int lineIndex) {
        var offset = lineIndex*UnicodeLineBytes;
        var prefix = offset.ToString(CultureInfo.InvariantCulture)+":";
        var suffix = "\r\n";
        var fillerLength = (UnicodeLineBytes/2)-prefix.Length-suffix.Length;
        var line = prefix+new string((char)('A'+(lineIndex%26)),fillerLength)+suffix;

        Assert.Equal(UnicodeLineBytes,Utf16NoBom.GetByteCount(line));
        Assert.StartsWith(prefix,line,StringComparison.Ordinal);
        Assert.EndsWith(suffix,line,StringComparison.Ordinal);
        return line;
    }

    [Fact, Trait("Category","Unit")]
    public void SequenctialAdd() {
        //0000 0000 0000 0000
        //0000 0000 0000 1000
        //0000 0000 0000 2000
        //15文字のヘッダ。30バイト
        var path = CreatePath();
        const int 行のバイト数 = 4096;
        const int 行の文字数 = 行のバイト数/sizeof(char);
        using var list = TempList.Create<char>(path);
        var chars = new char[行の文字数];
        for(var 列 = 0; 列<行の文字数; 列++)
            chars[列]=' ';
        chars[^1]='\n';
        list.SetOptimizationMode(FileAccess.Write);
        list.SetAccessPattern(FileOptions.SequentialScan);
        for(var 行 = 0; 行<行の文字数; 行++) {
            Span<char> span = chars;
            (行*行のバイト数).TryFormat(span,out _,"X16");
            span.Slice(12,4).CopyTo(span.Slice(15,4));
            span.Slice(8,4).CopyTo(span.Slice(10,4));
            span.Slice(4,4).CopyTo(span.Slice(5,4));
            span[4]=' ';
            span[9]=' ';
            span[14]=' ';
            for(var 列 = 0; 列<行の文字数; 列++) {
                list.Add(chars[列]);
            }
        }
        list.SetOptimizationMode(FileAccess.Read);
        for(var 行 = 0; 行<行の文字数; 行++) {
            var Span = list.AsSpan(行*行の文字数,行の文字数);
            var value0 = long.Parse(Span.Slice(0,4),NumberStyles.HexNumber);
            var value1 = long.Parse(Span.Slice(5,4),NumberStyles.HexNumber);
            var value2 = long.Parse(Span.Slice(10,4),NumberStyles.HexNumber);
            var value3 = long.Parse(Span.Slice(15,4),NumberStyles.HexNumber);
            var expected = 行*行のバイト数;
            var actual = (value0<<48)|(value1<<32)|(value2<<16)|value3;
            Assert.Equal(expected,actual);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Add_Enumerate_AndIndexer() {
        var path = CreatePath();
        try {
            using var list = TempList.Create<long>(path);
            list.Add(10);
            list.Add(20);
            list.Add(30);

            ref var first = ref list[0];
            first=15;

            Assert.Equal(3L,list.Count);
            Assert.Equal(15,list[0]);
            Assert.Equal(20,list[1]);
            Assert.Equal(30,list[2]);

            var items = list.ToArray();
            Assert.Equal(new long[] { 15,20,30 },items);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void Remove_DeletesTailOnly() {
        var path = CreatePath();
        try {
            using var list = TempList.Create<int>(path); ;
            list.Add(1);
            list.Add(2);
            list.Add(3);

            var removed = list.Remove();

            Assert.True(removed);
            Assert.Equal(2L,list.Count);
            Assert.Equal(new[] { 1,2 },list.ToArray());
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void DuplicateRemoval_SortsAndDeduplicates() {
        var path = CreatePath();
        try {
            using var list = TempList.Create<int>(path,initialCapacity: 1);
            list.Add(5);
            list.Add(3);
            list.Add(5);
            list.Add(1);
            list.Add(3);

            list.DuplicateRemoval();

            Assert.Equal(new[] { 1,3,5 },list.ToArray());
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void Reopen_PreservesCountAndValues() {
        var path = CreatePath();
        try {
            using(var list = TempList.Create<int>(path,initialCapacity: 1)) {
                for(var i = 0; i<8; i++) {
                    list.Add(i*2);
                }
            }

            using var reopened = TempList.Create<int>(path); ;
            Assert.Equal(8L,reopened.Count);
            Assert.Equal(new[] { 0,2,4,6,8,10,12,14 },reopened.ToArray());
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void UnmanagedList_SetTuning_AfterConstruction_PreservesValues() {
        var path = CreatePath();
        try {
            global::Seamless.Collections.List<int> list = TempList.Create<int>(path,initialCapacity: 1);
            list.Add(1);
            list.Add(2);

            list.SetTuning(new(FileAccess.Write,FileOptions.RandomAccess));
            list.Add(3);

            Assert.Equal(FileAccess.Write,list.OptimizationMode);
            Assert.Equal(FileOptions.RandomAccess,list.AccessPattern);
            Assert.Equal(new[] { 1,2,3 },list.ToArray());

            list.SetOptimizationMode(FileAccess.Read);
            list.SetAccessPattern(FileOptions.SequentialScan);

            Assert.Equal(FileAccess.Read,list.OptimizationMode);
            Assert.Equal(FileOptions.SequentialScan,list.AccessPattern);
            Assert.Equal(new[] { 1,2,3 },list.ToArray());
            list.Dispose();
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void ManagedList_AddT_AddSpanByte_AndDuplicateRemoval() {
        var path = CreatePath();
        try {
            using var list = TempList.Create<string>(path,initialCapacity: 1,initialDataCapacity: 32);
            var bravo = CreateFixedWidthUnicodeLine(1);
            var alpha = CreateFixedWidthUnicodeLine(0);
            list.Add(bravo);
            var alphaBytes = global::Seamless.Collections.ManagedSerializer<string>.Serialize(ref alpha).ToArray();
#if TSAVORITE1_1_1
            list.Add(global::Tsavorite.core.SpanByte.FromPinnedSpan(alphaBytes));
#else
            list.Add(alphaBytes);
#endif
            list.Add(alpha);

            list.DuplicateRemoval();

            Assert.Equal(2L,list.Count);
            Assert.Equal(alpha,list[0]);
            Assert.Equal(bravo,list[1]);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void ManagedList_SetTuning_AfterConstruction_PreservesValues() {
        var path = CreatePath();
        try {
            global::Seamless.Collections.List<string> list = TempList.Create<string>(path,initialCapacity: 1,initialDataCapacity: 64);
            var alpha = CreateFixedWidthUnicodeLine(0);
            var bravo = CreateFixedWidthUnicodeLine(1);

            list.Add(alpha);
            list.SetTuning(new(FileAccess.ReadWrite,FileOptions.RandomAccess));
            list.Add(bravo);

            Assert.Equal(FileAccess.ReadWrite,list.OptimizationMode);
            Assert.Equal(FileOptions.RandomAccess,list.AccessPattern);
            Assert.Equal(new[] { alpha,bravo },list.ToArray());

            list.SetOptimizationMode(FileAccess.Write);
            list.SetAccessPattern(FileOptions.SequentialScan);

            Assert.Equal(FileAccess.Write,list.OptimizationMode);
            Assert.Equal(FileOptions.SequentialScan,list.AccessPattern);
            Assert.Equal(new[] { alpha,bravo },list.ToArray());
            list.Dispose();
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void ManagedList_Reopen_PreservesCountAndValues() {
        var path = CreatePath();
        var values = new[] {
            CreateFixedWidthUnicodeLine(0),
            CreateFixedWidthUnicodeLine(1),
            CreateFixedWidthUnicodeLine(2),
        };
        try {
            using(var list = TempList.Create<string>(path,initialCapacity: 1,initialDataCapacity: 32)) {
                foreach(var value in values)
                    list.Add(value);
            }

            using var reopened = TempList.Create<string>(path);
            Assert.Equal(3L,reopened.Count);
            Assert.True(reopened.ToArray().SequenceEqual(values));
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void ManagedList_Reopen_AfterCountShrink_PreservesOnlyLiveValues() {
        var path = CreatePath();
        var bravo = CreateFixedWidthUnicodeLine(1);
        var alpha = CreateFixedWidthUnicodeLine(0);
        var charlie = CreateFixedWidthUnicodeLine(2);
        try {
            using(var list = TempList.Create<string>(path,initialCapacity: 4,initialDataCapacity: 64)) {
                list.Add(bravo);
                list.Add(alpha);
                list.Add(bravo);
                list.Add(charlie);

                list.DuplicateRemoval();
            }

            using var reopened = TempList.Create<string>(path);
            Assert.Equal(3L,reopened.Count);
            Assert.True(reopened.ToHashSet().SetEquals(new[] { alpha,bravo,charlie }));
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    private static void TryDeleteDirectory(string path) {
        var directoryPath = Path.GetDirectoryName(path);
        if(string.IsNullOrEmpty(directoryPath))
            return;
        try {
            Directory.Delete(directoryPath,recursive: true);
        }
        catch {
        }
    }
}
