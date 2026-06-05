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

