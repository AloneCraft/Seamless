using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
namespace Test.Seamless.Stress;

/// <summary>
/// Stress テストの決定性検証ユーティリティ。
/// 正規化済みのキー/値列を SHA256 でハッシュし、ゴールデンファイルに保存・比較する。
/// 初回はゴールデンを生成して PASS。以降の実行ではハッシュ一致を Assert する。
/// ゴールデンはテストソースと同じフォルダ (Stress/Goldens/) に置き、Git にコミットする運用とする。
/// </summary>
internal static class StressGolden {
    private const string GoldensSubDir = "Goldens";

    private static string ResolveGoldensDir([CallerFilePath] string? callerFilePath = null) {
        // Test.Seamless/Stress/ 直下の Goldens/ に配置する。
        var dir = Path.GetDirectoryName(callerFilePath)
            ??throw new InvalidOperationException("Caller file path not available.");
        return Path.Combine(dir,GoldensSubDir);
    }

    /// <summary>Int32 キーのハッシュ化。列挙は 1 パスで収集する。</summary>
    public static string HashInt32Keys(IEnumerable<int> keys) {
        var list = new List<int>();
        foreach(var k in keys) list.Add(k);
        list.Sort();
        using var sha = SHA256.Create();
        var buf = new byte[4];
        foreach(var k in list) {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(buf,k);
            sha.TransformBlock(buf,0,4,null,0);
        }
        sha.TransformFinalBlock([],0,0);
        return Convert.ToHexString(sha.Hash!);
    }

    /// <summary>String キーのハッシュ化。列挙は 1 パスで収集する。</summary>
    public static string HashStringKeys(IEnumerable<string> keys) {
        var list = new List<string>();
        foreach(var k in keys) list.Add(k);
        var sorted = list.ToArray();
        Array.Sort(sorted,StringComparer.Ordinal);
        using var sha = SHA256.Create();
        foreach(var k in sorted) {
            var bytes = Encoding.UTF8.GetBytes(k);
            var len = BitConverter.GetBytes(bytes.Length);
            sha.TransformBlock(len,0,4,null,0);
            sha.TransformBlock(bytes,0,bytes.Length,null,0);
        }
        sha.TransformFinalBlock([],0,0);
        return Convert.ToHexString(sha.Hash!);
    }

    /// <summary>(Int32 キー, Int32 値) ペアのハッシュ化。列挙は 1 パスで収集する。</summary>
    public static string HashInt32Pairs(IEnumerable<KeyValuePair<int,int>> pairs) {
        var list = new List<KeyValuePair<int,int>>();
        foreach(var kv in pairs) list.Add(kv);
        var sorted = list.ToArray();
        Array.Sort(sorted,(a,b) => a.Key.CompareTo(b.Key));
        using var sha = SHA256.Create();
        var buf = new byte[8];
        foreach(var kv in sorted) {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(0,4),kv.Key);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4,4),kv.Value);
            sha.TransformBlock(buf,0,8,null,0);
        }
        sha.TransformFinalBlock([],0,0);
        return Convert.ToHexString(sha.Hash!);
    }

    /// <summary>
    /// 既知キー集合を point-read して (Int32 key, Int32 value) ペアをハッシュ化する。
    /// scan/列挙経路を使わないため、ストレージ実装差異の影響を受けにくい。
    /// </summary>
    public static string HashInt32PairsByProbe(IEnumerable<int> keys,Func<int,(bool Found,int Value)> tryGet) {
        var list = new List<KeyValuePair<int,int>>();
        foreach(var key in keys) {
            var (found,value)=tryGet(key);
            if(found)
                list.Add(new KeyValuePair<int,int>(key,value));
        }
        return HashInt32Pairs(list);
    }

    /// <summary>(String キー, String 値) ペアのハッシュ化。列挙は 1 パスで収集する。</summary>
    public static string HashStringPairs(IEnumerable<KeyValuePair<string,string>> pairs) {
        var list = new List<KeyValuePair<string,string>>();
        foreach(var kv in pairs) list.Add(kv);
        var sorted = list.ToArray();
        Array.Sort(sorted,(a,b) => StringComparer.Ordinal.Compare(a.Key,b.Key));
        using var sha = SHA256.Create();
        foreach(var kv in sorted) {
            var kb = Encoding.UTF8.GetBytes(kv.Key);
            var vb = Encoding.UTF8.GetBytes(kv.Value);
            var len = BitConverter.GetBytes(kb.Length);
            sha.TransformBlock(len,0,4,null,0);
            sha.TransformBlock(kb,0,kb.Length,null,0);
            len=BitConverter.GetBytes(vb.Length);
            sha.TransformBlock(len,0,4,null,0);
            sha.TransformBlock(vb,0,vb.Length,null,0);
        }
        sha.TransformFinalBlock([],0,0);
        return Convert.ToHexString(sha.Hash!);
    }

    /// <summary>
    /// 既知キー集合を point-read して (String key, String value) ペアをハッシュ化する。
    /// </summary>
    public static string HashStringPairsByProbe(IEnumerable<string> keys,Func<string,(bool Found,string Value)> tryGet) {
        var list = new List<KeyValuePair<string,string>>();
        foreach(var key in keys) {
            var (found,value)=tryGet(key);
            if(found)
                list.Add(new KeyValuePair<string,string>(key,value));
        }
        return HashStringPairs(list);
    }

    /// <summary>
    /// 既知キー集合を Contains で probe して Int32 キー集合ハッシュを作る。
    /// </summary>
    public static string HashInt32KeysByProbe(IEnumerable<int> keys,Func<int,bool> contains) {
        var list = new List<int>();
        foreach(var key in keys) {
            if(contains(key))
                list.Add(key);
        }
        return HashInt32Keys(list);
    }

    /// <summary>
    /// 既知キー集合を Contains で probe して String キー集合ハッシュを作る。
    /// </summary>
    public static string HashStringKeysByProbe(IEnumerable<string> keys,Func<string,bool> contains) {
        var list = new List<string>();
        foreach(var key in keys) {
            if(contains(key))
                list.Add(key);
        }
        return HashStringKeys(list);
    }

    /// <summary>
    /// ゴールデンファイルとの比較。存在しなければ作成して PASS（初回生成モード）。
    /// <paramref name="testId"/> はファイル名。拡張子不要。
    /// </summary>
    public static void AssertGolden(string testId,string actualHash,[CallerFilePath] string? callerFilePath = null) {
        var goldensDir = ResolveGoldensDir(callerFilePath);
        Directory.CreateDirectory(goldensDir);
        var path = Path.Combine(goldensDir,testId+".sha256");
        if(!File.Exists(path)) {
            File.WriteAllText(path,actualHash+Environment.NewLine);
            // 初回生成: 以降 commit すれば再実行で比較。
            return;
        }
        var expected = File.ReadAllText(path).Trim();
        Assert.Equal(expected,actualHash);
    }
}
