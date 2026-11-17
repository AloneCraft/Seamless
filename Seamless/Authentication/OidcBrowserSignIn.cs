using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Web;

namespace Seamless.Authentication;

/// <summary>外部ブラウザーから同じ端末に戻る、一回の組織サインインを実行する。</summary>
public static class OidcBrowserSignIn{
    private const string CallbackPath="/callback";
    private const int MaximumHeaderBytes=16*1024;

    public static async Task<OidcSignInResult> SignInAsync(OidcProviderSettings settings,Func<Uri,CancellationToken,Task> openBrowser,int callbackPort=0,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(openBrowser);
        ArgumentOutOfRangeException.ThrowIfNegative(callbackPort);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(callbackPort,65535);
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        using var listener=new TcpListener(IPAddress.Loopback,callbackPort){ExclusiveAddressUse=true};
        listener.Start(8);
        var redirect=new Uri("http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port+CallbackPath);
        try{
            using var request=await OidcSignInRequest.PrepareAsync(settings,redirect,deadline.Token).ConfigureAwait(false);
            await openBrowser(request.StartUrl,deadline.Token).ConfigureAwait(false);
            var headers=new byte[MaximumHeaderBytes];
            while(true){
                using var client=await listener.AcceptTcpClientAsync(deadline.Token).ConfigureAwait(false);
                using var io=CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                io.CancelAfter(TimeSpan.FromSeconds(3));
                using var stream=client.GetStream();
                Uri? callback;
                try{callback=await ReadCallbackAsync(stream,redirect,request.CallbackState,headers,io.Token).ConfigureAwait(false);}
                catch(IOException){continue;}
                catch(OperationCanceledException)when(!deadline.IsCancellationRequested){continue;}
                if(callback is null){
                    await ReplyAsync(stream,400,"認証要求に対応する応答を待っています。",io.Token).ConfigureAwait(false);
                    continue;
                }
                listener.Stop();
                OidcSignInResult result;
                try{result=await request.CompleteAsync(callback,deadline.Token).ConfigureAwait(false);}
                catch(OidcAuthenticationException){
                    await ReplyAsync(stream,400,"組織サインインを完了できませんでした。アプリを確認してください。",deadline.Token).ConfigureAwait(false);
                    throw;
                }
                await ReplyAsync(stream,200,"組織サインインが完了しました。アプリに戻ってください。",deadline.Token).ConfigureAwait(false);
                return result;
            }
        }catch(OperationCanceledException)when(!cancellationToken.IsCancellationRequested&&deadline.IsCancellationRequested){
            throw new OidcAuthenticationException("SignInTimeout","The organization sign-in did not finish within its deadline.");
        }
    }

    private static async Task<Uri?> ReadCallbackAsync(NetworkStream stream,Uri redirect,string state,byte[] headers,CancellationToken cancellationToken){
        var count=0;
        while(count<headers.Length){
            var read=await stream.ReadAsync(headers.AsMemory(count),cancellationToken).ConfigureAwait(false);
            if(read==0){return null;}
            var start=Math.Max(0,count-3);
            count+=read;
            var end=headers.AsSpan(start,count-start).IndexOf("\r\n\r\n"u8);
            if(end>=0){return ParseCallback(headers.AsSpan(0,start+end),redirect,state);}
        }
        return null;
    }

    private static Uri? ParseCallback(ReadOnlySpan<byte> headers,Uri redirect,string state){
        var lines=Encoding.Latin1.GetString(headers).Split("\r\n",StringSplitOptions.None);
        var request=lines[0].Split(' ');
        if(request.Length!=3||request[0]!="GET"||request[2] is not("HTTP/1.1" or "HTTP/1.0")||
            !request[1].StartsWith(CallbackPath+"?",StringComparison.Ordinal)||request[1].Contains('#')){
            return null;
        }
        var hostFound=false;
        foreach(var header in lines.AsSpan(1)){
            var colon=header.IndexOf(':');
            if(colon<=0){return null;}
            var name=header.AsSpan(0,colon);
            if(!name.Equals("Host",StringComparison.OrdinalIgnoreCase)){continue;}
            if(hostFound||!header.AsSpan(colon+1).Trim().Equals(redirect.Authority,StringComparison.Ordinal)){return null;}
            hostFound=true;
        }
        if(!hostFound||!Uri.TryCreate(redirect.GetLeftPart(UriPartial.Authority)+request[1],UriKind.Absolute,out var callback)){return null;}
        var parameters=HttpUtility.ParseQueryString(callback.Query);
        var states=parameters.GetValues("state");
        if(states is not{Length:1}||!string.Equals(states[0],state,StringComparison.Ordinal)){return null;}
        var codes=parameters.GetValues("code");
        var errors=parameters.GetValues("error");
        if(codes is [{Length:>0}]&&errors is null||errors is [{Length:>0}]&&codes is null){return callback;}
        return null;
    }

    private static async Task ReplyAsync(NetworkStream stream,int status,string message,CancellationToken cancellationToken){
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(3));
        var body=Encoding.UTF8.GetBytes(message);
        var reason=status==200?"OK":"Bad Request";
        var header=Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {reason}\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n");
        try{
            await stream.WriteAsync(header,deadline.Token).ConfigureAwait(false);
            await stream.WriteAsync(body,deadline.Token).ConfigureAwait(false);
        }catch(IOException){}
        catch(OperationCanceledException){}
    }
}
