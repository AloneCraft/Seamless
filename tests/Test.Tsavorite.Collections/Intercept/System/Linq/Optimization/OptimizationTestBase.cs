namespace Test.Tsavorite.Intercept.System.Linq.Optimization;

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

    public static void AssertSequenceEqual<T>(IEnumerable<T> expected,IEnumerable<T> actual){
        Assert.Equal(expected,actual);
    }
}
