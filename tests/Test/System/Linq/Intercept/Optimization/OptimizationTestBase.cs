namespace Test.System.Linq.Intercept.Optimization;

// 最適化変換（不変式ホイスト/キャプチャ除去/SelectMany 融合・述語プッシュダウン/ハッシュ結合）の
// 意味保存を検証するテスト群の共通基底。Chain1 と同じ「expected=完全修飾 SystemEnumerable（非 intercept）/
// actual=拡張メソッド形（intercept）」の対比で比較する。actual は `.Consume(Consumer<T>)` で駆動して
// チェーンへ余計な LINQ メソッドを足さない（Consumer/ConsumeSupport は親名前空間にある）。
public abstract class OptimizationTestBase{
    public static int[] Outer() => [1,2,3,4,5];
    public static int[] Inner() => [10,20,30];
    public static int[] EmptyOuter() => [];

    public sealed record Item(int Id,int Key,string Name);

    // 結合系のソース。Key に重複を含めてハッシュ結合のマルチバケットを通す。
    public static Item[] Outers() => [new(1,0,"a"),new(2,1,"b"),new(3,2,"c"),new(4,0,"d")];
    public static Item[] Inners() => [new(10,1,"x"),new(11,1,"y"),new(12,2,"z"),new(13,7,"none")];

    // ラムダ入力に依存しない不変式。ホイスト対象になっても結果が変わらないよう決定的にする。
    public static int StaticCheck() => 100;
    public static int StackCheck() => 2;
    public static int GetOffset() => 1;
    public static int Limit() => 20;

    // §1 代数的書換テスト用のソース。
    public static int[] Numbers() => [5,3,8,1,9,2,7];
    public static int[] EmptyNumbers() => [];

    // 値型（struct）。X に重複（1,3）を含め、MinBy の先頭一致や struct メンバアクセスの順入替を通す。
    public readonly record struct Point(int X,int Y);
    public static Point[] Points() => [new(3,1),new(1,9),new(3,5),new(2,4),new(1,2)];

    // 参照型ソース（OrderBy(k).FirstOrDefault()→MinBy の null 既定パス用）。
    public static string[] Words() => ["pear","apple","kiwi","fig","banana"];
    public static string[] EmptyWords() => [];

    // null 混在（Where 順入替の安全ゲート抑止ケース用）。
    public static string?[] WithNulls() => ["abc",null,"de",null,"fghi"];

    public static void AssertSequenceEqual<T>(IEnumerable<T> expected,IEnumerable<T> actual){
        Assert.Equal(expected,actual);
    }
}
