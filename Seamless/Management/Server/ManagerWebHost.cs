using System.Net;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Localization;
using Seamless.Management.Server.ManagerWeb;
using 表示文言=Seamless.Management.Components.表示文言;

namespace Seamless.Management.Server;

/// <summary>DBを所有するホストと同じプロセスで、共通ManagerのローカルWeb画面を配信する。</summary>
public sealed class ManagerWebHost:IAsyncDisposable{
    private readonly WebApplication application;
    private readonly IManagerBrowserSessions browserSessions;
    private readonly object disposalGate=new();
    private Task? disposal;
    public LocalBrowserSessions Sessions=>this.browserSessions as LocalBrowserSessions??throw new ManagementException("Unsupported","外部Managerには所有者起動セッションがありません。");
    public Uri Origin{get;}
    public int ProcessId=>Environment.ProcessId;
    private ManagerWebHost(WebApplication application,IManagerBrowserSessions sessions,Uri origin){this.application=application;this.browserSessions=sessions;this.Origin=origin;}

    public static async Task<ManagerWebHost> StartAsync(IManagementOperations operations,int port=0,int sessionCapacity=16,CancellationToken cancellationToken=default)=>
        await StartAsync(operations,null,port,sessionCapacity,cancellationToken).ConfigureAwait(false);
    internal static async Task<ManagerWebHost> StartAsync(IManagementOperations operations,Func<DraftSdkBuilder?>? sourceBuilds,int port=0,int sessionCapacity=16,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(operations);
        if(port is <0 or >65535){throw new ArgumentOutOfRangeException(nameof(port));}
        var info=await operations.InfoAsync(cancellationToken).ConfigureAwait(false);
        if(!info.Capabilities.Contains("OwnerSourceProjectsV1",StringComparer.Ordinal)){throw new ManagementException("Permission","OS所有者のホストからローカルManagerを起動してください。");}
        var sessions=new LocalBrowserSessions(info.InstanceId,capacity:sessionCapacity);
        return await StartCoreAsync(sessions,IPAddress.Loopback,port,null,null,provider=>new BrowserManagementOperations(sessions,operations,SessionId(provider)),sourceBuilds,cancellationToken).ConfigureAwait(false);
    }
    public static async Task<ManagerWebHost> StartExternalAsync(ManagementService service,ConcurrentManagementOperations execution,X509Certificate2 certificate,IPAddress address,int port,Uri? publicOrigin=null,int sessionCapacity=64,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(service);ArgumentNullException.ThrowIfNull(execution);ArgumentNullException.ThrowIfNull(certificate);ArgumentNullException.ThrowIfNull(address);
        if(!certificate.HasPrivateKey){throw new ArgumentException("HTTPSの秘密鍵を持つ証明書が必要です。",nameof(certificate));}
        if(port is <0 or >65535){throw new ArgumentOutOfRangeException(nameof(port));}
        if(!IPAddress.IsLoopback(address)&&publicOrigin is null){throw new ArgumentException("外部公開にはHTTPSの公開起点が必要です。",nameof(publicOrigin));}
        if(publicOrigin is not null&&(!publicOrigin.IsAbsoluteUri||publicOrigin.Scheme!="https"||publicOrigin.Port!=port||publicOrigin.AbsolutePath!="/"||publicOrigin.UserInfo.Length>0||publicOrigin.Query.Length>0||publicOrigin.Fragment.Length>0)){
            throw new ArgumentException("HTTPSの公開起点と待受けポートを指定してください。",nameof(publicOrigin));
        }
        if((await execution.InfoAsync(cancellationToken).ConfigureAwait(false)).InstanceId!=service.Info.InstanceId){throw new ArgumentException("同じ管理領域の実行ホストが必要です。",nameof(execution));}
        var sessions=new UserBrowserSessions(service,sessionCapacity);
        return await StartCoreAsync(sessions,address,port,certificate,publicOrigin,provider=>new AuthenticatedManagementOperations(execution,sessions,SessionId(provider)),()=>service.SdkBuilder,cancellationToken).ConfigureAwait(false);
    }
    private static Func<Task<string?>> SessionId(IServiceProvider provider)=>async()=>{
        var state=await provider.GetRequiredService<AuthenticationStateProvider>().GetAuthenticationStateAsync();
        return state.User.FindFirst("browserSession")?.Value;
    };
    private static async Task<ManagerWebHost> StartCoreAsync(IManagerBrowserSessions sessions,IPAddress address,int port,X509Certificate2? certificate,Uri? publicOrigin,Func<IServiceProvider,IManagementOperations> resolveOperations,Func<DraftSdkBuilder?>? sourceBuilds,CancellationToken cancellationToken){
        WebApplication? application=null;
        try{
            var assembly=typeof(ManagerWebHost).Assembly;
            var assemblyDirectory=Path.GetDirectoryName(global::Seamless.Execution.CompilationReferences.PathFor(assembly))!;
            var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=assembly.GetName().Name,ContentRootPath=assemblyDirectory});
            builder.WebHost.ConfigureKestrel(options=>options.Listen(address,port,listen=>{if(certificate is not null){listen.UseHttps(certificate);}}));
            builder.WebHost.UseStaticWebAssets();
            builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics",LogLevel.Warning);
            builder.Services.AddSingleton(sessions);
            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddScoped<IManagementOperations>(resolveOperations);
            builder.Services.AddSingleton<global::Seamless.Management.Components.I生成ソース取得>(new 生成ソース取得サービス(sourceBuilds));
            builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options=>{
                options.Cookie.Name="Seamless.Manager."+Guid.NewGuid().ToString("N");
                options.Cookie.HttpOnly=true;
                options.Cookie.Path=sessions.PathBase;
                options.Cookie.SameSite=SameSiteMode.Strict;
                options.Cookie.SecurePolicy=CookieSecurePolicy.SameAsRequest;
                if(certificate is not null){options.Cookie.SecurePolicy=CookieSecurePolicy.Always;}
                options.ExpireTimeSpan=sessions.SessionLifetime;
                options.SlidingExpiration=false;
                options.Events.OnValidatePrincipal=context=>{
                    try{_=sessions.Authenticate(context.Principal?.FindFirst("browserSession")?.Value);}
                    catch(ManagementException){context.RejectPrincipal();}
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToLogin=context=>{
                    if(sessions is UserBrowserSessions){context.Response.Redirect(sessions.PathBase+"login");}
                    else{
                        context.Response.StatusCode=401;
                        context.Response.ContentType="text/html; charset=utf-8";
                        return context.Response.WriteAsync(OwnerLaunchRequiredPage());
                    }
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied=context=>{context.Response.StatusCode=403;return Task.CompletedTask;};
            });
            builder.Services.AddAuthorization(options=>options.AddPolicy("manager-user",policy=>policy.RequireAuthenticatedUser().RequireClaim("instance",sessions.InstanceId.ToString())));
            builder.Services.AddRazorComponents().AddInteractiveServerComponents().AddHubOptions(options=>options.MaximumReceiveMessageSize=ManagementWire.MaximumBytes);
            application=builder.Build();
            var browserLanguage=new CustomRequestCultureProvider(context=>{
                var header=context.Request.GetTypedHeaders().AcceptLanguage;
                if(header is null){return Task.FromResult<ProviderCultureResult?>(null);}
                var accepted=header.Where(value=>value.Quality.GetValueOrDefault(1)>0)
                    .OrderByDescending(value=>value.Quality.GetValueOrDefault(1)).Select(value=>value.Value).ToArray();
                if(accepted.Length==0){return Task.FromResult<ProviderCultureResult?>(null);}
                return Task.FromResult<ProviderCultureResult?>(new(accepted));
            });
            var languages=new RequestLocalizationOptions{RequestCultureProviders=[browserLanguage],ApplyCurrentCultureToResponseHeaders=true};
            languages.SetDefaultCulture("en").AddSupportedCultures("ja","en").AddSupportedUICultures("ja","en");
            application.UseRequestLocalization(languages);
            Uri? origin=null;
            application.Use(async(context,next)=>{
                if(origin is null||context.Request.Host.Value!=origin.Authority){context.Response.StatusCode=400;return;}
                if(!context.Request.Path.StartsWithSegments(sessions.PathBase.TrimEnd('/'))){context.Response.StatusCode=404;return;}
                var requestOrigin=context.Request.Headers.Origin.ToString();
                if(requestOrigin.Length>0&&requestOrigin!=origin.GetLeftPart(UriPartial.Authority)){context.Response.StatusCode=403;return;}
                context.Response.Headers.CacheControl="no-store";
                context.Response.Headers["Referrer-Policy"]="no-referrer";
                context.Response.Headers["X-Content-Type-Options"]="nosniff";
                await next();
            });
            application.UsePathBase(sessions.PathBase.TrimEnd('/'));
            application.UseRouting();
            application.UseAuthentication();
            application.UseAuthorization();
            application.UseAntiforgery();
            application.MapStaticAssets(ResolveStaticAssetsManifest(assemblyDirectory,assembly.GetName().Name!,System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name));
            if(sessions is LocalBrowserSessions local){
                application.MapGet("/launch",()=>Results.Content(LaunchPage(sessions.PathBase),"text/html")).AllowAnonymous();
                application.MapPost("/session",async(HttpContext context)=>{
                    if(origin is null||context.Request.Headers.Origin!=origin.GetLeftPart(UriPartial.Authority)||context.Request.ContentLength!=64){return Results.Unauthorized();}
                    using var reader=new StreamReader(context.Request.Body);
                    var token=await reader.ReadToEndAsync(context.RequestAborted);
                    LocalBrowserSession session;
                    try{session=local.Redeem(token,context.Request.Headers.Origin.ToString());}
                    catch(ManagementException){return Results.Unauthorized();}
                    await SignInAsync(context,sessions,session.Id,session.User,session.ExpiresAt);
                    return Results.NoContent();
                }).AllowAnonymous();
            }
            if(sessions is UserBrowserSessions users){
                application.MapGet("/login",()=>Results.Content(LoginPage(sessions.PathBase),"text/html")).AllowAnonymous();
                application.MapPost("/login",async(HttpContext context)=>{
                    if(origin is null||context.Request.Headers.Origin!=origin.GetLeftPart(UriPartial.Authority)){return Results.StatusCode(403);}
                    if(!context.Request.HasFormContentType){return Results.StatusCode(415);}
                    var bodyLimit=context.Features.Get<IHttpMaxRequestBodySizeFeature>();
                    if(bodyLimit is{IsReadOnly:false}){bodyLimit.MaxRequestBodySize=4096;}
                    var form=await context.Request.ReadFormAsync(context.RequestAborted);
                    try{
                        var session=await Task.Run(()=>users.SignIn(new(form["name"].ToString(),form["password"].ToString())),context.RequestAborted);
                        await SignInAsync(context,sessions,session.Id,session.User,session.ExpiresAt);
                        return Results.Redirect(sessions.PathBase);
                    }catch(ManagementException error)when(error.Code=="Authentication"){
                        return Results.Content(LoginPage(sessions.PathBase,true),"text/html",statusCode:401);
                    }catch(ManagementException error)when(error.Code=="Busy"){return Results.StatusCode(503);}
                }).AllowAnonymous();
                application.MapPost("/logout",async(HttpContext context)=>{
                    if(origin is null||context.Request.Headers.Origin!=origin.GetLeftPart(UriPartial.Authority)){return Results.StatusCode(403);}
                    users.SignOut(context.User.FindFirst("browserSession")?.Value);
                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return Results.Redirect(sessions.PathBase+"login");
                }).AllowAnonymous();
            }
            application.MapRazorComponents<管理ページ>().AddInteractiveServerRenderMode().RequireAuthorization("manager-user");
            await application.StartAsync(cancellationToken).ConfigureAwait(false);
            origin=publicOrigin??new(application.Urls.Single());
            if(sessions is LocalBrowserSessions owner){owner.BindOrigin(origin);}
            return new(application,sessions,origin);
        }catch{
            sessions.Dispose();
            if(application is not null){await application.DisposeAsync().ConfigureAwait(false);}
            throw;
        }
    }
    internal static string? ResolveStaticAssetsManifest(string directory,string serverName,string? entryName){
        var serverManifest=Path.Combine(directory,serverName+".staticwebassets.endpoints.json");
        if(File.Exists(serverManifest)){return serverManifest;}
        // 組み込みアプリのpublishでは、参照先サーバーの資産も起動EXEのmanifestへ集約される。
        if(entryName is not null){
            var entryManifest=Path.Combine(directory,entryName+".staticwebassets.endpoints.json");
            if(File.Exists(entryManifest)){return entryManifest;}
        }
        return null;
    }
    private static Task SignInAsync(HttpContext context,IManagerBrowserSessions sessions,string id,UserInfo user,DateTimeOffset expires){
        var principal=new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.Name,user.Name),new("instance",sessions.InstanceId.ToString()),new("browserSession",id)],CookieAuthenticationDefaults.AuthenticationScheme));
        return context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,principal,new AuthenticationProperties{IsPersistent=false,ExpiresUtc=expires});
    }
    private static string LoginPage(string path,bool failed=false){
        var message="";if(failed){message="<p role=\"alert\">"+WebUtility.HtmlEncode(表示文言.取得("利用者名またはパスワードを確認してください。"))+"</p>";}
        return """
            <!DOCTYPE html><html lang="__LANG__"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="referrer" content="same-origin"><title>Seamless Manager · __LOGIN__</title>
            <link rel="stylesheet" href="__BASE___content/Seamless.Management.Components/manager.css"></head>
            <body><main class="ログイン画面"><small>SEAMLESS</small><h1>Manager</h1>__MESSAGE__
            <form method="post" action="__BASE__login"><label>__USER__<input name="name" autocomplete="username" required></label><label>__PASSWORD__<input name="password" type="password" autocomplete="current-password" required></label><button class="主要ボタン" type="submit"><svg class="操作アイコン" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><path d="M15 3h5v18h-5M3 12h13m-5-5 5 5-5 5"/></svg>__LOGIN__</button></form>
            </main></body></html>
            """.Replace("__BASE__",path,StringComparison.Ordinal).Replace("__MESSAGE__",message,StringComparison.Ordinal)
                .Replace("__LANG__",表示文言.言語,StringComparison.Ordinal).Replace("__LOGIN__",WebUtility.HtmlEncode(表示文言.取得("ログイン")),StringComparison.Ordinal)
                .Replace("__USER__",WebUtility.HtmlEncode(表示文言.取得("利用者名")),StringComparison.Ordinal).Replace("__PASSWORD__",WebUtility.HtmlEncode(表示文言.取得("パスワード")),StringComparison.Ordinal);
    }
    private static string LaunchPage(string path)=>"""
        <!DOCTYPE html><html lang="__LANG__"><meta charset="utf-8"><title>Seamless Manager</title>
        <p id="state">__CHECKING__</p>
        <script>
        const capability=location.hash.slice(1);history.replaceState(null,'','__BASE__launch');
        fetch('__BASE__session',{method:'POST',credentials:'same-origin',headers:{'Content-Type':'text/plain'},body:capability}).then(response=>{
            if(response.ok){location.replace('__BASE__');}else{document.getElementById('state').textContent=__INVALID__;}
        }).catch(()=>{document.getElementById('state').textContent=__UNAVAILABLE__;});
        </script></html>
        """.Replace("__BASE__",path,StringComparison.Ordinal).Replace("__LANG__",表示文言.言語,StringComparison.Ordinal)
            .Replace("__CHECKING__",WebUtility.HtmlEncode(表示文言.取得("所有者の起動セッションを確認しています。")),StringComparison.Ordinal)
            .Replace("__INVALID__",System.Text.Json.JsonSerializer.Serialize(表示文言.取得("起動セッションが無効です。ホストから開き直してください。")),StringComparison.Ordinal)
            .Replace("__UNAVAILABLE__",System.Text.Json.JsonSerializer.Serialize(表示文言.取得("サーバーへ接続できません。ホストから開き直してください。")),StringComparison.Ordinal);
    private static string OwnerLaunchRequiredPage()=>"""
        <!DOCTYPE html><html lang="__LANG__"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Seamless Manager</title></head>
        <body><main><h1>Seamless Manager</h1><p role="status">__MESSAGE__</p></main></body></html>
        """.Replace("__LANG__",表示文言.言語,StringComparison.Ordinal)
            .Replace("__MESSAGE__",WebUtility.HtmlEncode(表示文言.取得("起動セッションが無効です。ホストから開き直してください。")),StringComparison.Ordinal);
    public ValueTask DisposeAsync(){lock(this.disposalGate){return new(this.disposal??=this.DisposeCoreAsync());}}
    private async Task DisposeCoreAsync(){
        this.browserSessions.Dispose();
        try{await this.application.StopAsync().ConfigureAwait(false);}
        finally{await this.application.DisposeAsync().ConfigureAwait(false);}
    }
}
