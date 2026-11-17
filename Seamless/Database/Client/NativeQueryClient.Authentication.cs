using System.Buffers;
using System.Security.Cryptography;

namespace Seamless.Database.Client;

public sealed record NativeAuthenticatedPrincipal(Guid AuthorityId,string Subject);
public sealed class NativeAuthenticationException(NativeAuthenticationError error):IOException("The native authentication exchange was rejected: "+error+"."){
    public NativeAuthenticationError Error{get;}=error;
}

public sealed partial class NativeQueryClient{
    /// <summary>現在の認証済み接続で、同じ主体の資格情報を更新する。</summary>
    public async Task ReauthenticateAsync(CancellationToken cancellationToken=default){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        if(this.accessTokenProvider is null){throw new InvalidOperationException("The client does not use authenticated native TLS.");}
        if(this.transaction is not null){throw new InvalidOperationException("A transaction already owns this connection.");}
        await this.EnterConnectionAsync(cancellationToken).ConfigureAwait(false);
        try{
            ObjectDisposedException.ThrowIf(this.disposed,this);
            if(this.stream is not{} transport||this.AuthenticatedPrincipal is not{} previous){throw new InvalidOperationException("Connect with native authentication before refreshing credentials.");}
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);deadline.CancelAfter(TimeSpan.FromSeconds(15));
            var token=await this.accessTokenProvider(deadline.Token).ConfigureAwait(false);deadline.Token.ThrowIfCancellationRequested();
            var request=NativeAuthenticationWire.Request(token);
            try{
                var principal=await ExchangeAuthenticationAsync(transport,NativeAuthenticationWire.Reauthenticate,request,deadline.Token).ConfigureAwait(false);
                if(principal!=previous){throw new InvalidDataException("The refreshed native connection returned a different subject.");}
            }catch(NativeAuthenticationException){throw;}
            catch{this.CloseConnection();throw;}
        }finally{this.ReleaseOperation(preservePreconnected:true);}
    }
    private async Task<NativeAuthenticatedPrincipal> AuthenticateAsync(Stream transport,CancellationToken cancellationToken){
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var token=await this.accessTokenProvider!(deadline.Token).ConfigureAwait(false);deadline.Token.ThrowIfCancellationRequested();
        var request=NativeAuthenticationWire.Request(token);
        return await ExchangeAuthenticationAsync(transport,NativeAuthenticationWire.Authenticate,request,deadline.Token).ConfigureAwait(false);
    }
    private static async Task<NativeAuthenticatedPrincipal> ExchangeAuthenticationAsync(Stream transport,byte kind,byte[] request,CancellationToken cancellationToken){
        try{await NativeFrame.WriteAsync(transport,kind,request,cancellationToken).ConfigureAwait(false);}
        finally{CryptographicOperations.ZeroMemory(request);}
        var header=await NativeFrame.ReadHeaderAsync(transport,new byte[5],cancellationToken).ConfigureAwait(false)??throw new EndOfStreamException("The server disconnected during native authentication.");
        if(header.Kind==NativeAuthenticationWire.Error){
            if(header.PayloadLength!=2){throw new InvalidDataException("Invalid native authentication failure size.");}
            var bytes=new byte[2];await transport.ReadExactlyAsync(bytes,cancellationToken).ConfigureAwait(false);throw new NativeAuthenticationException(NativeAuthenticationWire.ReadFailure(bytes));
        }
        if(header.Kind!=NativeAuthenticationWire.Authenticated||header.PayloadLength>NativeAuthenticationWire.MaximumAcknowledgementBytes){throw new InvalidDataException("Expected a bounded native authentication acknowledgement.");}
        var response=new byte[header.PayloadLength];await transport.ReadExactlyAsync(response,cancellationToken).ConfigureAwait(false);
        var subject=NativeAuthenticationWire.ReadAcknowledgement(new ReadOnlySequence<byte>(response));return new(subject.AuthorityId,subject.Subject);
    }
}
