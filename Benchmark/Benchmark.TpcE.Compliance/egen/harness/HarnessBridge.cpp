// EGenTxnHarness(C++)を C# から呼ぶための橋渡し DLL。
// 取引の手順(フレームの呼び出し順・出力の検証・状態コード)は EGen のハーネスにそのまま任せ、
// ハーネスが DB インターフェース経由で呼ぶフレームを、C# のコールバックへ中継する。
//
// 使い方(C# 側): Harness_Create でコールバックを登録 → 取引ごとに Harness_Run(取引 ID, 入力の構造体, 出力の構造体)。
// 入力・出力は EGen の TxnHarnessStructs.h の構造体そのもの(C# 側は生成したラッパーで読み書きする)。
// 一度に 1 本のスレッドから呼ぶ(SUT は取引を直列に実行する)。
#include "HarnessBridge.g.h"
#include <exception>

using namespace HarnessBridge;

extern "C" {

__declspec(dllexport) void* __cdecl Harness_Create(FrameCallback frame,SendCallback send){
    return new Bridge(frame,send);
}

__declspec(dllexport) void __cdecl Harness_Destroy(void* handle){
    delete static_cast<Bridge*>(handle);
}

// 戻り値: ハーネスの出力の status(0 成功、正は警告、負は誤り)。
// *failure: 0 なら正常。フレームのコールバックが失敗を返した(その値)、または想定外の例外(-1)。
__declspec(dllexport) int __cdecl Harness_Run(void* handle,int txnId,const void* input,void* output,int* failure){
    *failure=0;
    try{
        return RunTransaction(*static_cast<Bridge*>(handle),txnId,input,output);
    }catch(const BridgeFailure& error){
        *failure=error.code;
        return -1;
    }catch(const std::exception&){
        *failure=-1;
        return -1;
    }
}

// 試験用: 実測した構造体の大きさ(C# の生成コードと照合する)。
__declspec(dllexport) int __cdecl Harness_Version(){
    return 1;
}

}   // extern "C"
