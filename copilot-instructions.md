# Copilot Instructions
- 作業を開始する前に、あなたの思考プロセス（Reasoning）と各修正の意図を REASONING.md というファイルに詳しく書き出し、それをリポジトリに残してください。作業完了後もこのファイルを削除しないでください。見やすいように整形したREASONING.htmlも同じ場所に残してください。REASONING.mdとREASONING.htmlは、あなたの思考プロセスを理解するための重要なドキュメントです。これらのファイルには、なぜ特定の変更を行ったのか、どのような選択肢があったのか、そして最終的にどのような結論に至ったのかを詳しく説明してください。これにより、他の開発者があなたの作業を理解しやすくなり、将来的なメンテナンスや改善にも役立ちます。
- エージェントが作成するスクリプト、一時ファイルはルートのTempに入れて。ソース管理はそれをignoreに設定して
## Coding Rule
インデントは全てスペースにする
全ての識別子と記号間、記号と記号間、") {","> {"といった括弧の間のスペースは無し、カンマ前後のスペースは無し
- K&Rをsベースに
- 波括弧位置を同一行
- if elseで片方にしか処理がなくても{}は省略しない
    if(condition){
        処理
    }else{
    }
    if(condition){
    }else{
        処理
    }
- 三項演算子
    三項演算子は片方しか通過しなくてもカバレッジできないのでif{}else{}を必ず使うこと
- 制御文は省略して
    if(condition)
        continue;
- 記号の前後のスペースはなくして
    a + b
    a+b
- コロンの前後の間のスペースはなくして
    class A : IDispose
    class A:IDispose
- カンマの前後の間のスペースはなくして
    int a ,int b, int c
    int a,int b,int c
- 記号と識別子の間のスペースはなくして
    List<int> a
    List<int>a
- 括弧の間のスペースはなくして
    ) {
    ){
    > {
    >{
- 改行の前にスペースがあったら削除して
    break;   
    break;
- 1.1.1 vs 2.0beta3、Managed vs Unmanaged のような対称ファイルは、意味だけでなく構文配置も揃える
    対応ファイルでは、フィールド、プロパティ、コンストラクタ、メソッド、ネスト型など全メンバーを同じ順序に並べる
    メンバー順は親クラスまたは基準となる対応ファイルの順序に合わせる
    片側にしか存在しないメンバーがあっても後続メンバーの位置を崩さず、空行で対応位置を維持する
    diffアルゴリズムなしの比較ツールでも見比べられるように、行番号と縦位置を可能な限り揃える
    自動整形や単独ファイルの見た目より、対称性と比較容易性を優先する
    版差分、型差分、API差分による最小限の違いだけを許容し、それ以外の順序差分、空白差分、配置差分は作らない
    この規則は Seamless.1.1.1 と Seamless.2.0beta3 の対応ファイル全体、および Managed... と Unmanaged... の対応ファイル全体に適用する
    対応ファイルを編集するときは単独ファイルとして最適化せず、必ず対応ファイル群を1セットとして扱う
    片側だけメンバー順や空行配置を変えて、比較位置対応を崩してはいけない

## User Preferences
- Listに追加してはいけない。Tsavoriteを使っている理由はメモリに収まらないサイズのコレクションを扱うためであり、Iterator/Enumeratorで全件をList等のメモリ上コレクションに蓄積するのはNG。チャンク単位（固定長配列）でバッファリングし、少しずつ返す方式にすること。
- C# のジェネリック関連コンパイルエラーでは型変換だけでなく where 制約の不一致を優先して確認すること。
- ベースとなる機能は最上位クラスに実装し特定のクラスのインスタンス化はfluent factoryパターンで実装する
- 制約でオーバーロードしたいので拡張メソッドで行う
- 静的ストラテジーでunmanaged,非unmanaged動作を切り替える
- 切り替えるのは
	unmanaged(Fixed) TKey,unmanaged(Fixed) TValue
	unmanaged(Fixed) TKey,managed(Variable) TValue
	managed(Variable) TKey,unmanaged(Fixed) TValue
	managed(Variable) TKey,managed(Variable)TValue
- CreateUnmanaged 系の新規経路では識別子に `Unmanaged` を付ける命名方針を優先する。
## Test Class Rules
- テストクラスはテスト対象クラスと同じ名前にすること。テスト対象が `Seamless.1.1.1/Collections/Collection.cs` だとするとテストクラスは `Test.Seamless.1.1.1/Collections/Collection.cs`
- テストメソッドの順序はテスト対象と全く同じにする
- テスト対象の public virtual bool TryGetValue(TKey Key,[MaybeNullWhen(false)] out TValue Value);のテストメソッドは
- [Fact,Trait("Category","Unit")]public void TryGetValue(){....}
- TryGetValueに関するあらゆるテストはこの内部で完結させる。つまり複数テストを行う。コメントで何のテストか明示する
[Fact,Trait("Category","Unit")]public void TryGetValue(){
    {
        //キーが一致するときのテスト
    }
    {
        //キーが一致しないときのテスト
    }
}
このように{}でスコープを区切る
#if #elseで1.1.1,2.0beta3を分岐させない。やりたいときはTest.Seamless.1.1.1,Test.Seamless.2.0beta3の両方に同じテストコードを置いて、片方だけでなく両方でテストすること。

