using System.IO.Pipes;
using System.Security.Principal;
using Seamless.Execution;
using Seamless.Execution.Codecs;

namespace Seamless.Database.Client;

public sealed partial class NativeQueryClient{
    private string? pipeName;
    /// <summary>同一ホスト・同一OS所有者のIPCへ接続する。</summary>
    public static NativeQueryClient ForLocalOwner(string pipeName,ResourceBudget? recordBudget=null,RecordGraphValidationLimits? recordLimits=null){
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        if(pipeName.Length>128||pipeName.Any(static value=>!char.IsAsciiLetterOrDigit(value)&&value is not '-' and not '_' and not '.')){throw new ArgumentException("Invalid local pipe name.",nameof(pipeName));}
        recordLimits??=RecordGraphValidationLimits.Default;recordLimits.CheckForNative();
        return new(){pipeName=pipeName,RecordBudget=recordBudget??NativeRecordResources.DefaultBudget,RecordLimits=recordLimits};
    }
    private async Task ConnectPipeAsync(CancellationToken cancellationToken){
        var pipe=new NamedPipeClientStream(".",this.pipeName!,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly,TokenImpersonationLevel.Identification);
        try{
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);deadline.CancelAfter(TimeSpan.FromSeconds(15));
            await pipe.ConnectAsync(deadline.Token).ConfigureAwait(false);this.stream=pipe;
        }catch{pipe.Dispose();throw;}
    }
}
