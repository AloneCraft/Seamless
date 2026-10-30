# Last生成ローカルと利用者識別子の衝突回避

## 確認した問題と修正理由

`Query(int[] values,int __i)=>values.Last(p=>p>__i)` は、LastのCompute引数としてcaptureされた`__i`と逆順ループの固定名`__i`が衝突する。修正前の独立回帰ではAll/Noneとも正確に1個を傍受するが、CS0136でemitできない。`__lastSource0`等をcaptureする別queryの2セルはemitとSystem値比較に成功した。

既存の`GeneratedNames.Current.StableLocal`でspan/index/runtime span/runtime indexの名前を割り当てる。用途とFragmentIndexをキーにして同じ描画中の参照名を固定し、利用者識別子との衝突を避ける。走査方向、predicateの呼出回数、値のcapture、例外、counter scopeはこの変更では変えない。

## 受入と範囲

既存10800比較と別の2query×All/Noneを用いる。新回帰ではcompile/emit/実行成功、System値一致、傍受1個、EnumerableFallback=0、inline PhysicalDelegate=0を要求する。修正前後の入力SHA、生成C#、Generator/DLL SHA、診断を保存する。結果は`Work/Orchestration/last-name-safety-negative-07`と`last-name-safety-positive-07`へ記録した。同一4入力で修正後すべてが成功し、342入力ファイルと346出力ハッシュの一致を確認した。限定受入の対象版はpositive-07のsource-freezeであり、この文書は全Last意味契約の合格を示さない。
