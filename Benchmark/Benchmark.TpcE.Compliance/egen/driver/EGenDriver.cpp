// TPC-E 準拠リハーサルの運転側(Driver)。EGen 1.14.0 の CE/MEE/DM をそのまま使い、取引の入力(EGen の TxnInput の構造体のバイト列)を SUT(C#)へ送る。
// SUT は EGenTxnHarness(C++)で取引を実行する。入力は EGen が作った構造体を変換せずに送るので、取引の中身はドライバーと SUT の間で書き換わらない。
// 出す数字は TPC-E 由来の非監査値で、公式結果とは比較できない。
//
// 構成(仕様 6.2 の「複数スレッドの CE」): CE のスレッドが U 本(各自 CCE を 1 つ・SUT への接続を 1 本持つ)、MEE のスレッド 1 本、DM のスレッド 1 本。
// SUT は取引を直列に実行するので、隔離は成り立つ。応答時間は運転側で計る(sT=送る直前、eT=応答を受けた直後。仕様 6.5.1)。
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <winsock2.h>
#include <ws2tcpip.h>
#include <algorithm>
#include <atomic>
#include <chrono>
#include <cmath>
#include <condition_variable>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <map>
#include <memory>
#include <mutex>
#include <stdexcept>
#include <string>
#include <thread>
#include <vector>

#include "CE.h"
#include "MEE.h"
#include "DM.h"
#include "EGenLogger.h"
#include "EGenLogFormatterTab.h"
#include "../InputFiles/inc/DataFileManager.h"

using namespace TPCE;
using Clock=std::chrono::steady_clock;

namespace {

enum Txn { BV, CP, MF, MW, SD, TL, TO, TR, TS, TU, DMT, TC, TxnCount };
const char* const TxnNames[TxnCount]={"BrokerVolume","CustomerPosition","MarketFeed","MarketWatch","SecurityDetail","TradeLookup","TradeOrder","TradeResult","TradeStatus","TradeUpdate","DataMaintenance","TradeCleanup"};
// 応答時間の上限(秒、90 パーセンタイル。仕様 6.5.1.2)。DM は 55 秒以内、TC は制約なし。
const double ResponseLimit[TxnCount]={3,3,2,3,3,3,2,2,1,3,55,0};

struct Settings {
    std::string host="127.0.0.1";
    int port=0;
    std::string input="flat_in/";
    long long customers=1000;
    int scaleFactor=500;
    int days=1;
    int users=8;
    int rampSeconds=10;
    int measureSeconds=60;
    int downSeconds=5;
    double targetTpse=0;      // 0 は間を空けない(最大速度)
    int dmIntervalSeconds=60;
    bool cleanup=true;
    std::string out="driver-result.json";
};

// ---- 計測 -------------------------------------------------------------------------------------------------------
struct Sample { unsigned startMs; unsigned endMs; bool ok; int status; };
struct Stats {
    std::mutex lock;
    std::vector<Sample> samples;
};
Stats g_stats[TxnCount];
std::mutex g_errorLock;
std::map<std::string,long long> g_errors;       // "取引: エラー文" ごとの件数(原因調査と結果の記録用)
Clock::time_point g_t0;
std::atomic<bool> g_stop(false);
std::atomic<bool> g_fatal(false);
// 応答を受け取った中で最大の「確定済みの書き込みの番号」。SUT を強制停止したあと、これより前の書き込みが全部残っているかを確かめる基準(耐久性の試験)。
std::atomic<long long> g_lastAckedWriteSequence(0);
std::string g_fatalMessage;
std::mutex g_fatalLock;

long long NowMs(){ return std::chrono::duration_cast<std::chrono::milliseconds>(Clock::now()-g_t0).count(); }

void Fatal(const std::string& message){
    std::lock_guard<std::mutex> guard(g_fatalLock);
    if(!g_fatal.exchange(true)){
        g_fatalMessage=message;
        fprintf(stderr,"fatal: %s\n",message.c_str());
    }
    g_stop=true;
}

// ---- 通信 -------------------------------------------------------------------------------------------------------
class Connection {
    SOCKET m_socket;
    void SendAll(const char* data,int length){
        int sent=0;
        while(sent<length){
            int n=send(m_socket,data+sent,length-sent,0);
            if(n<=0){ throw std::runtime_error("send failed"); }
            sent+=n;
        }
    }
    void ReceiveAll(char* data,int length){
        int got=0;
        while(got<length){
            int n=recv(m_socket,data+got,length-got,0);
            if(n<=0){ throw std::runtime_error("recv failed"); }
            got+=n;
        }
    }
public:
    Connection(const std::string& host,int port){
        m_socket=socket(AF_INET,SOCK_STREAM,IPPROTO_TCP);
        sockaddr_in address={};
        address.sin_family=AF_INET;
        address.sin_port=htons((u_short)port);
        inet_pton(AF_INET,host.c_str(),&address.sin_addr);
        if(connect(m_socket,(sockaddr*)&address,sizeof(address))!=0){
            closesocket(m_socket);
            throw std::runtime_error("connect failed");
        }
        BOOL noDelay=TRUE;
        setsockopt(m_socket,IPPROTO_TCP,TCP_NODELAY,(const char*)&noDelay,sizeof(noDelay));
    }
    ~Connection(){ closesocket(m_socket); }
    std::string Call(const std::string& body){
        unsigned char header[4];
        int length=(int)body.size();
        header[0]=(unsigned char)(length&0xFF); header[1]=(unsigned char)((length>>8)&0xFF);
        header[2]=(unsigned char)((length>>16)&0xFF); header[3]=(unsigned char)((length>>24)&0xFF);
        std::string frame((const char*)header,4);
        frame+=body;
        SendAll(frame.data(),(int)frame.size());
        unsigned char reply[4];
        ReceiveAll((char*)reply,4);
        int replyLength=reply[0]|(reply[1]<<8)|(reply[2]<<16)|(reply[3]<<24);
        std::string text((size_t)replyLength,'\0');
        ReceiveAll(&text[0],replyLength);
        return text;
    }
};

// ---- JSON(SUT の応答の形に限った簡易版。取引の入力は JSON にしない) -------------------------------------------------
std::string Quote(const char* text){
    std::string out="\"";
    for(const char* p=text;*p;++p){
        unsigned char c=(unsigned char)*p;
        if(c=='"'||c=='\\'){ out+='\\'; out+=(char)c; }
        else if(c<0x20){ char buffer[8]; snprintf(buffer,sizeof(buffer),"\\u%04x",c); out+=buffer; }
        else { out+=(char)c; }
    }
    out+='"';
    return out;
}

bool FindRaw(const std::string& json,const char* key,std::string& value){
    std::string pattern=std::string("\"")+key+"\":";
    size_t at=json.find(pattern);
    if(at==std::string::npos){ return false; }
    at+=pattern.size();
    size_t end=at;
    if(json[at]=='"'){
        end=json.find('"',at+1);
        value=json.substr(at+1,end-at-1);
    }else{
        while(end<json.size()&&json[end]!=','&&json[end]!='}'){ ++end; }
        value=json.substr(at,end-at);
    }
    return true;
}

TTradeRequest ParseRequest(const std::string& json){
    TTradeRequest request;
    request.Clear();
    std::string v;
    if(FindRaw(json,"trade_id",v)){ request.trade_id=_atoi64(v.c_str()); }
    if(FindRaw(json,"symbol",v)){ strncpy(request.symbol,v.c_str(),sizeof(request.symbol)-1); }
    if(FindRaw(json,"trade_qty",v)){ request.trade_qty=atoi(v.c_str()); }
    if(FindRaw(json,"trade_type_id",v)){ strncpy(request.trade_type_id,v.c_str(),sizeof(request.trade_type_id)-1); }
    if(FindRaw(json,"price_quote",v)){ request.price_quote=atof(v.c_str()); }
    if(FindRaw(json,"action",v)){ request.eAction=(atoi(v.c_str())==0)?eMEEProcessOrder:eMEESetLimitOrderTrigger; }
    return request;
}
struct Reply {
    bool ok=false;
    int status=0;
    // SUT が市場(MEE)へ送るよう返した注文。Trade-Order の成行(処理)と指値(発動条件の登録)、Market-Feed が発動させた指値(処理)。
    std::vector<TTradeRequest> requests;
    std::string error;
};
Reply ParseReply(const std::string& json){
    Reply reply;
    std::string v;
    if(FindRaw(json,"ok",v)){ reply.ok=(v=="true"); }
    if(FindRaw(json,"status",v)){ reply.status=atoi(v.c_str()); }
    if(FindRaw(json,"wseq",v)){
        long long acked=_atoi64(v.c_str());
        long long seen=g_lastAckedWriteSequence.load();
        while(acked>seen&&!g_lastAckedWriteSequence.compare_exchange_weak(seen,acked)){}
    }
    if(FindRaw(json,"error",v)){ reply.error=v; }
    size_t array=json.find("\"trade_requests\":[");
    if(array!=std::string::npos){
        size_t end=json.find(']',array);
        size_t at=array;
        while(true){
            size_t open=json.find('{',at);
            if(open==std::string::npos||(end!=std::string::npos&&open>end)){ break; }
            size_t close=json.find('}',open);
            if(close==std::string::npos){ break; }
            reply.requests.push_back(ParseRequest(json.substr(open,close-open+1)));
            at=close+1;
        }
    }
    return reply;
}

// ---- MEE のタイマー ---------------------------------------------------------------------------------------------
class MeeTimer {
    std::mutex m_lock;
    std::condition_variable m_wake;
    Clock::time_point m_fire=Clock::time_point::max();
public:
    void Schedule(INT32 delayMs){
        if(delayMs<0){ return; }
        std::lock_guard<std::mutex> guard(m_lock);
        Clock::time_point at=Clock::now()+std::chrono::milliseconds(delayMs);
        if(at<m_fire){ m_fire=at; }
        m_wake.notify_all();
    }
    // 次に GenerateTradeResult を呼ぶ時刻まで待つ。止められたら false。
    bool WaitForFire(){
        std::unique_lock<std::mutex> guard(m_lock);
        while(!g_stop){
            if(m_fire!=Clock::time_point::max()&&Clock::now()>=m_fire){
                m_fire=Clock::time_point::max();
                return true;
            }
            if(m_fire==Clock::time_point::max()){ m_wake.wait_for(guard,std::chrono::milliseconds(50)); }
            else { m_wake.wait_until(guard,std::min(m_fire,Clock::now()+std::chrono::milliseconds(50))); }
        }
        return false;
    }
};
MeeTimer g_meeTimer;
std::atomic<CMEE*> g_mee(nullptr);
// Market-Feed が発動させた指値注文を、MEE のスレッドが GenerateTradeResult から戻った後に渡すための待ち行列。
// MEE が GenerateTradeResult を実行している最中(= SUT の MarketFeed を呼んでいる最中)に同じスレッドから SubmitTradeRequest を呼ぶと、
// MEE の内部の待ち行列をその場で書き換えて壊す(仕様の図 6.e でも、市場への送信は非同期)。
std::mutex g_triggeredLock;
std::vector<TTradeRequest> g_triggered;
void QueueTriggered(const std::vector<TTradeRequest>& requests){
    if(requests.empty()){ return; }
    {
        std::lock_guard<std::mutex> guard(g_triggeredLock);
        g_triggered.insert(g_triggered.end(),requests.begin(),requests.end());
    }
    g_meeTimer.Schedule(0);   // MEE のスレッドを起こして、すぐ渡させる
}
void SubmitQueuedTriggered(CMEE* mee){
    std::vector<TTradeRequest> batch;
    {
        std::lock_guard<std::mutex> guard(g_triggeredLock);
        batch.swap(g_triggered);
    }
    for(TTradeRequest& request:batch){
        INT32 delay=mee->SubmitTradeRequest(&request);
        g_meeTimer.Schedule(delay);
    }
}

// ---- SUT への口(CE・MEE・DM が共通に使う) -----------------------------------------------------------------------
// EGenHarness の取引 ID(SUT の HarnessTransaction と同じ値)。
enum HarnessId { HTradeOrder=0,HTradeStatus=1,HCustomerPosition=2,HBrokerVolume=3,HSecurityDetail=4,HMarketWatch=5,HTradeLookup=6,HTradeUpdate=7,HTradeResult=8,HMarketFeed=9,HDataMaintenance=10,HTradeCleanup=11 };

class SutLink {
protected:
    Connection m_connection;
    SutLink(const Settings& s):m_connection(s.host,s.port){}
    // 1 件送って応答を待つ。sT/eT を計り、成功したかを返す。要求は「0x01 + 取引 ID(4 バイト) + TxnInput の構造体」。
    // 取引の status が負(誤り)のときは失敗として数える(仕様 6.1.1.2: 取引の誤りは認められない。予期された巻き戻しの status は正)。
    template<typename Input>
    bool Send(Txn txn,HarnessId id,const Input* input,Reply* reply=nullptr){
        std::string message(1+4+sizeof(Input),'\0');
        message[0]=1;
        int32_t txnId=(int32_t)id;
        memcpy(&message[1],&txnId,4);
        memcpy(&message[5],input,sizeof(Input));
        long long start=NowMs();
        std::string text;
        try{
            text=m_connection.Call(message);
        }catch(const std::exception& e){
            if(!g_stop){ Fatal(std::string(TxnNames[txn])+": "+e.what()); }
            return false;
        }
        long long end=NowMs();
        Reply parsed=ParseReply(text);
        bool ok=parsed.ok;
        if(!parsed.ok){
            std::lock_guard<std::mutex> guard(g_errorLock);
            ++g_errors[std::string(TxnNames[txn])+": "+parsed.error];
        }else if(parsed.status<0){
            ok=false;
            std::lock_guard<std::mutex> guard(g_errorLock);
            ++g_errors[std::string(TxnNames[txn])+": status "+std::to_string(parsed.status)];
        }
        {
            std::lock_guard<std::mutex> guard(g_stats[txn].lock);
            g_stats[txn].samples.push_back(Sample{(unsigned)start,(unsigned)end,ok,parsed.status});
        }
        if(reply){ *reply=parsed; }
        return ok;
    }
};

class CeSut : public CCESUTInterface, private SutLink {
public:
    CeSut(const Settings& s):SutLink(s){}
    bool BrokerVolume(PBrokerVolumeTxnInput p) override { return Send(BV,HBrokerVolume,p); }
    bool CustomerPosition(PCustomerPositionTxnInput p) override { return Send(CP,HCustomerPosition,p); }
    bool MarketWatch(PMarketWatchTxnInput p) override { return Send(MW,HMarketWatch,p); }
    bool SecurityDetail(PSecurityDetailTxnInput p) override { return Send(SD,HSecurityDetail,p); }
    bool TradeLookup(PTradeLookupTxnInput p) override { return Send(TL,HTradeLookup,p); }
    bool TradeOrder(PTradeOrderTxnInput p,INT32 /*tradeType*/,bool /*executorIsAccountOwner*/) override {
        Reply reply;
        bool ok=Send(TO,HTradeOrder,p,&reply);
        CMEE* mee=g_mee.load();
        if(ok&&mee){
            // 市場(MEE)へ注文を送る。ここから先は応答時間に含めない(仕様どおり、応答を受けた後)。
            for(TTradeRequest& request:reply.requests){
                INT32 delay=mee->SubmitTradeRequest(&request);
                g_meeTimer.Schedule(delay);
            }
        }
        return ok;
    }
    bool TradeStatus(PTradeStatusTxnInput p) override { return Send(TS,HTradeStatus,p); }
    bool TradeUpdate(PTradeUpdateTxnInput p) override { return Send(TU,HTradeUpdate,p); }
};

class MeeSut : public CMEESUTInterface, private SutLink {
public:
    MeeSut(const Settings& s):SutLink(s){}
    bool TradeResult(PTradeResultTxnInput p) override { return Send(TR,HTradeResult,p); }
    bool MarketFeed(PMarketFeedTxnInput p) override {
        Reply reply;
        bool ok=Send(MF,HMarketFeed,p,&reply);
        // 発動した指値注文は、ここ(MEE が GenerateTradeResult を実行している最中)では渡さず、待ち行列に積む。
        if(ok){
            QueueTriggered(reply.requests);
        }
        return ok;
    }
};

class DmSut : public CDMSUTInterface, private SutLink {
public:
    DmSut(const Settings& s):SutLink(s){}
    bool DataMaintenance(PDataMaintenanceTxnInput p) override { return Send(DMT,HDataMaintenance,p); }
    bool TradeCleanup(PTradeCleanupTxnInput p) override { return Send(TC,HTradeCleanup,p); }
};

// 負荷単位(顧客 1,000 人ごと)ごとの Trade-Result の累計を SUT に問い合わせる(計測区間の始めと終わりの差が、区間中の件数。仕様 6.7.4)。
std::vector<long long> QueryLoadUnits(const Settings& s){
    std::vector<long long> counts;
    try{
        Connection connection(s.host,s.port);
        std::string text=connection.Call("{\"admin\":\"load_units\"}");
        size_t at=text.find("\"load_units\":[");
        if(at==std::string::npos){ return counts; }
        at=text.find('[',at)+1;
        size_t end=text.find(']',at);
        while(at<end){
            size_t comma=text.find(',',at);
            if(comma==std::string::npos||comma>end){ comma=end; }
            counts.push_back(_atoi64(text.substr(at,comma-at).c_str()));
            at=comma+1;
        }
    }catch(const std::exception& e){
        fprintf(stderr,"load unit query failed: %s\n",e.what());
    }
    return counts;
}

// ---- スレッド ----------------------------------------------------------------------------------------------------
// EGen の CCE/CMEE/CDM のコンストラクターは、共有の静的な状態を最初に初期化する(スレッド安全ではない)。
// そのため、オブジェクトはすべて main スレッドで順番に作り、スレッドは作られたものを回すだけにする。
void CeThread(const Settings& s,CCE* cePointer){
    CCE& ce=*cePointer;
    try{
        // 間隔の目標: CE が出す取引は全体の約 89%(Trade-Result 10%・Market-Feed 1% は MEE が出す)。tpsE = Trade-Result の毎秒件数。
        double period=0;
        if(s.targetTpse>0){ period=(double)s.users/(8.9*s.targetTpse); }
        Clock::time_point next=Clock::now();
        while(!g_stop){
            if(period>0){
                std::this_thread::sleep_until(next);
                next+=std::chrono::duration_cast<Clock::duration>(std::chrono::duration<double>(period));
                if(next<Clock::now()){ next=Clock::now(); }
                if(g_stop){ break; }
            }
            ce.DoTxn();
        }
    }catch(const std::exception& e){
        Fatal(std::string("CE thread: ")+e.what());
    }
}
void MeeThread(CMEE* mee){
    try{
        while(g_meeTimer.WaitForFire()){
            SubmitQueuedTriggered(mee);
            INT32 delay=mee->GenerateTradeResult();
            g_meeTimer.Schedule(delay);
            SubmitQueuedTriggered(mee);
        }
        mee->DisableTickerTape();
    }catch(const std::exception& e){
        Fatal(std::string("MEE thread: ")+e.what());
    }
}
void DmThread(const Settings& s,CDM* dmPointer){
    CDM& dm=*dmPointer;
    try{
        Clock::time_point next=Clock::now()+std::chrono::seconds(s.dmIntervalSeconds);
        while(!g_stop){
            if(Clock::now()>=next){
                dm.DoTxn();
                next+=std::chrono::seconds(s.dmIntervalSeconds);
            }else{
                std::this_thread::sleep_for(std::chrono::milliseconds(100));
            }
        }
    }catch(const std::exception& e){
        Fatal(std::string("DM thread: ")+e.what());
    }
}

// ---- 結果 --------------------------------------------------------------------------------------------------------
struct Summary { long long count=0,failed=0; double avg=0,p90=0,max=0,min=0; };
Summary Summarize(Txn txn,long long fromMs,long long toMs){
    Summary out;
    std::vector<double> rts;
    std::lock_guard<std::mutex> guard(g_stats[txn].lock);
    for(const Sample& sample:g_stats[txn].samples){
        if((long long)sample.startMs<fromMs||(long long)sample.endMs>toMs){ continue; }
        // 仕様 6.7: 失敗は数える。予期された巻き戻し(Trade-Order の status は SUT が決める)は ok のまま。
        if(!sample.ok){ ++out.failed; }
        rts.push_back((double)(sample.endMs-sample.startMs)/1000.0);
    }
    out.count=(long long)rts.size();
    if(rts.empty()){ return out; }
    double sum=0; for(double r:rts){ sum+=r; }
    out.avg=sum/rts.size();
    out.max=*std::max_element(rts.begin(),rts.end());
    out.min=*std::min_element(rts.begin(),rts.end());
    size_t k=(size_t)std::ceil(0.9*rts.size())-1;
    std::nth_element(rts.begin(),rts.begin()+k,rts.end());
    out.p90=rts[k];
    return out;
}
double RoundUp2(double v){ if(v<=0){ return 0; } return std::ceil(v*100.0-1e-9)/100.0; }

void WriteResult(const Settings& s,long long m0,long long m1,const std::vector<long long>& loadUnitsAtStart,const std::vector<long long>& loadUnitsAtEnd){
    FILE* f=fopen(s.out.c_str(),"w");
    if(!f){ fprintf(stderr,"cannot write %s\n",s.out.c_str()); return; }
    double seconds=(double)(m1-m0)/1000.0;
    Summary summaries[TxnCount];
    long long total=0;
    for(int t=0;t<TxnCount;++t){ summaries[t]=Summarize((Txn)t,m0,m1); if(t!=DMT&&t!=TC){ total+=summaries[t].count; } }
    double tpse=0;
    if(seconds>0){ tpse=summaries[TR].count/seconds; }
    fprintf(f,"{\n  \"notice\": \"TPC-E derived, unaudited, not comparable to published TPC-E results\",\n");
    fprintf(f,"  \"customers\": %lld, \"scale_factor\": %d, \"days\": %d, \"users\": %d, \"target_tpse\": %.3f,\n",s.customers,s.scaleFactor,s.days,s.users,s.targetTpse);
    fprintf(f,"  \"measure_seconds\": %.1f, \"tpsE\": %.3f, \"nominal_tpsE\": %.3f, \"fatal\": %s, \"last_acked_wseq\": %lld,\n",seconds,tpse,(double)s.customers/s.scaleFactor,g_fatal?"true":"false",(long long)g_lastAckedWriteSequence.load());
    fprintf(f,"  \"transactions\": [\n");
    for(int t=0;t<TxnCount;++t){
        const Summary& m=summaries[t];
        double mix=0; if(total>0&&t!=DMT&&t!=TC){ mix=100.0*m.count/total; }
        bool pass=true;
        if(ResponseLimit[t]>0){ pass=(RoundUp2(m.p90)<=ResponseLimit[t])&&(m.avg<=m.p90+1e-9); if(t==DMT){ pass=(m.max<=55.0); } }
        fprintf(f,"    {\"name\":\"%s\",\"count\":%lld,\"failed\":%lld,\"mix_percent\":%.3f,\"avg_s\":%.2f,\"p90_s\":%.2f,\"min_s\":%.2f,\"max_s\":%.2f,\"limit_s\":%.0f,\"within_limit\":%s}%s\n",
            TxnNames[t],m.count,m.failed,mix,RoundUp2(m.avg),RoundUp2(m.p90),RoundUp2(m.min),RoundUp2(m.max),ResponseLimit[t],pass?"true":"false",(t<TxnCount-1)?",":"");
    }
    fprintf(f,"  ],\n  \"trade_result_per_second\": [");
    // 定常状態の推移(仕様 6.7)。Trade-Result の毎秒件数を、全期間について出す。
    long long span=NowMs()/1000+1;
    std::vector<long long> perSecond((size_t)span+1,0);
    {
        std::lock_guard<std::mutex> guard(g_stats[TR].lock);
        for(const Sample& sample:g_stats[TR].samples){ size_t at=sample.endMs/1000; if(at<perSecond.size()){ ++perSecond[at]; } }
    }
    for(size_t i=0;i<perSecond.size();++i){ fprintf(f,"%s%lld",(i>0)?",":"",perSecond[i]); }
    fprintf(f,"],\n  \"errors\": [");
    {
        std::lock_guard<std::mutex> guard(g_errorLock);
        bool first=true;
        for(const auto& entry:g_errors){
            fprintf(f,"%s{\"message\":%s,\"count\":%lld}",first?"":",",Quote(entry.first.c_str()).c_str(),entry.second);
            first=false;
        }
    }
    fprintf(f,"],\n  \"load_unit_trade_results\": [");
    for(size_t i=0;i<loadUnitsAtEnd.size();++i){
        long long before=(i<loadUnitsAtStart.size())?loadUnitsAtStart[i]:0;
        fprintf(f,"%s%lld",(i>0)?",":"",loadUnitsAtEnd[i]-before);
    }
    fprintf(f,"]\n}\n");
    fclose(f);
    printf("tpsE(unaudited) %.3f over %.1f s (nominal %.3f)\n",tpse,seconds,(double)s.customers/s.scaleFactor);
    {
        std::lock_guard<std::mutex> guard(g_errorLock);
        for(const auto& entry:g_errors){ printf("  error x%lld: %s\n",entry.second,entry.first.c_str()); }
    }
    for(int t=0;t<TxnCount;++t){ printf("  %-18s count=%-8lld failed=%-4lld p90=%.2f avg=%.2f max=%.2f\n",TxnNames[t],summaries[t].count,summaries[t].failed,RoundUp2(summaries[t].p90),RoundUp2(summaries[t].avg),RoundUp2(summaries[t].max)); }
}

bool Parse(int argc,char** argv,Settings& s){
    for(int i=1;i<argc;++i){
        std::string a=argv[i];
        auto next=[&](const char* name)->const char*{ if(i+1>=argc){ fprintf(stderr,"%s needs a value\n",name); exit(3); } return argv[++i]; };
        if(a=="--host"){ s.host=next("--host"); }
        else if(a=="--port"){ s.port=atoi(next("--port")); }
        else if(a=="--input"){ s.input=next("--input"); }
        else if(a=="--customers"){ s.customers=_atoi64(next("--customers")); }
        else if(a=="--scale-factor"){ s.scaleFactor=atoi(next("--scale-factor")); }
        else if(a=="--days"){ s.days=atoi(next("--days")); }
        else if(a=="--users"){ s.users=atoi(next("--users")); }
        else if(a=="--ramp"){ s.rampSeconds=atoi(next("--ramp")); }
        else if(a=="--measure"){ s.measureSeconds=atoi(next("--measure")); }
        else if(a=="--down"){ s.downSeconds=atoi(next("--down")); }
        else if(a=="--tpse"){ s.targetTpse=atof(next("--tpse")); }
        else if(a=="--dm-interval"){ s.dmIntervalSeconds=atoi(next("--dm-interval")); }
        else if(a=="--no-cleanup"){ s.cleanup=false; }
        else if(a=="--out"){ s.out=next("--out"); }
        else { fprintf(stderr,"unknown option %s\n",a.c_str()); return false; }
    }
    return s.port>0&&s.users>0;
}

}   // namespace

int main(int argc,char** argv){
    Settings s;
    if(!Parse(argc,argv,s)){
        fprintf(stderr,"usage: EGenDriver --port N [--host H] [--input flat_in/] [--customers N] [--days N] [--users N] [--ramp S] [--measure S] [--down S] [--tpse X] [--out file]\n");
        return 3;
    }
    WSADATA wsa;
    WSAStartup(MAKEWORD(2,2),&wsa);
    int exitCode=0;
    const bool trace=getenv("EGEN_DRIVER_TRACE")!=nullptr;
    auto Stage=[&](const char* text){ if(trace){ fprintf(stderr,"[driver] %s\n",text); fflush(stderr); } };
    try{
        Stage("start");
        CLogFormatTab fmt;
        CEGenLogger logger(eDriverAll,0,"EGenDriver.log",&fmt);
        Stage("logger ready");
        // DataFileManager はスレッド安全ではない(遅延読み込みで書き換わる)。最初に全部読み込めば、以後は読むだけで複数スレッドから使える。
        const DataFileManager dfm(s.input,s.customers,s.customers,DataFileManager::IMMEDIATE_LOAD);
        Stage("input files ready");
        g_t0=Clock::now();
        if(s.cleanup){
            // 仕様 6.5.1.6: Trade-Cleanup は Test Run の前に完了していなければならない。
            DmSut sut(s);
            CDM dm(&sut,&logger,dfm,s.customers,s.customers,s.scaleFactor,s.days,(UINT32)(s.users+3));
            Stage("cleanup start");
            dm.DoCleanupTxn();
            Stage("cleanup done");
        }
        // EGen のオブジェクトは、スレッドを起こす前に main で順番に全部作る(コンストラクターがスレッド安全ではないため)。
        MeeSut meeSut(s);
        CMEE mee(0,&meeSut,&logger,dfm,(UINT32)(s.users+1));
        DmSut dmSut(s);
        CDM dm(&dmSut,&logger,dfm,s.customers,s.customers,s.scaleFactor,s.days,(UINT32)(s.users+2));
        std::vector<std::unique_ptr<CeSut>> ceSuts;
        std::vector<std::unique_ptr<CCE>> ces;
        for(int u=0;u<s.users;++u){
            ceSuts.emplace_back(new CeSut(s));
            ces.emplace_back(new CCE(ceSuts.back().get(),&logger,dfm,s.customers,s.customers,s.scaleFactor,s.days,(UINT32)(u+1)));
        }
        Stage("objects ready");
        // 計測の時計は Trade-Cleanup のあと(ここから)を 0 とする。
        for(int t=0;t<TxnCount;++t){ if(t!=TC){ g_stats[t].samples.clear(); } }
        mee.SetBaseTime();
        g_mee=&mee;
        g_t0=Clock::now();
        Stage("threads start");
        std::vector<std::thread> threads;
        threads.emplace_back(MeeThread,&mee);
        threads.emplace_back(DmThread,std::cref(s),&dm);
        for(int u=0;u<s.users;++u){ threads.emplace_back(CeThread,std::cref(s),ces[(size_t)u].get()); }
        const long long m0=(long long)s.rampSeconds*1000;
        const long long m1=m0+(long long)s.measureSeconds*1000;
        const long long end=m1+(long long)s.downSeconds*1000;
        std::vector<long long> loadUnitsAtStart,loadUnitsAtEnd;
        bool tookStart=false,tookEnd=false;
        while(!g_stop&&NowMs()<end){
            long long now=NowMs();
            if(!tookStart&&now>=m0){ loadUnitsAtStart=QueryLoadUnits(s); tookStart=true; }
            if(!tookEnd&&now>=m1){ loadUnitsAtEnd=QueryLoadUnits(s); tookEnd=true; }
            std::this_thread::sleep_for(std::chrono::milliseconds(200));
        }
        g_stop=true;
        for(std::thread& t:threads){ t.join(); }
        g_mee=nullptr;
        WriteResult(s,m0,m1,loadUnitsAtStart,loadUnitsAtEnd);
        if(g_fatal){ exitCode=2; }
    }catch(const std::exception& e){
        fprintf(stderr,"driver failed: %s\n",e.what());
        exitCode=2;
    }
    WSACleanup();
    return exitCode;
}
