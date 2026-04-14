# Copilot Instructions

## Coding Rule
-
う
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
- 制御文は省略して
    if(condition)
        continue;
- 記号の前後のスペースはなくして
    a + b
    a+b
- 括弧の間のスペースはなくして
    ) {
    ){
- 改行の前にスペースがあったら削除して
    break;   
    break;
- 1.1.1 vs 2.0
- Managed vs Unmanaged のような対称的なファイルは
- ファイルの差分を調べれるようにすべてのメンバーの順序を親クラスのメンバー順に合わせる
    class Managed{
        int a;
        int b;
        int c;
    }
    class Managed{
        int b;
        int c;
        int d;
    }
- 行番号を揃える。diffアルゴリズムを使わない比較ツールでもわかるようにする
    class Managed{
        int a;
        int b;
        int c;

    }
    class Managed{

        int b;
        int c;
        int d;
    }

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
