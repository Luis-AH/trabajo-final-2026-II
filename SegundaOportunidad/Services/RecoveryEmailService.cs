using System.Net;
using System.Net.Mail;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace SegundaOportunidad.Services;
public sealed class RecoveryEmailOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "";
    public string PublicBaseUrl { get; set; } = "";
}
public interface IRecoveryEmailService
{
    bool Configurado { get; }
    string BaseUrl { get; }
    Task EnviarAsync(string email,string url);
}
public sealed class RecoveryEmailService(IOptions<RecoveryEmailOptions> options, IWebHostEnvironment env) : IRecoveryEmailService, IEmailSender
{
    public string BaseUrl => options.Value.PublicBaseUrl.TrimEnd('/');
    public bool Configurado => !string.IsNullOrWhiteSpace(options.Value.Host) && !string.IsNullOrWhiteSpace(options.Value.From)
        && !string.IsNullOrWhiteSpace(options.Value.Username) && !string.IsNullOrWhiteSpace(options.Value.Password)
        && options.Value.Port is > 0 and <= 65535 && Uri.TryCreate(BaseUrl,UriKind.Absolute,out var uri)
        && string.IsNullOrEmpty(uri.UserInfo) && (uri.Scheme == "https" || (env.IsDevelopment() && uri.Scheme == "http" && uri.IsLoopback));
    public Task EnviarAsync(string email,string url) => SendEmailAsync(email,"Recuperar acceso a SegundaOportunidad",$"<p>Solicitaste restablecer tu contraseña. El enlace vence en una hora y solo puede usarse una vez.</p><p><a href=\"{WebUtility.HtmlEncode(url)}\">Restablecer contraseña</a></p><p>Si no lo solicitaste, ignora este correo.</p>");
    public async Task SendEmailAsync(string email,string subject,string htmlMessage)
    {
        // Identity usa este mismo remitente para confirmar correos de registros nuevos.
        if (!Configurado) return;
        var o = options.Value;
        using var message = new MailMessage(o.From,email,subject,htmlMessage) { IsBodyHtml=true };
        using var smtp = new SmtpClient(o.Host,o.Port) { EnableSsl=true,UseDefaultCredentials=false,Credentials=new NetworkCredential(o.Username,o.Password),Timeout=15000 };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await smtp.SendMailAsync(message,timeout.Token);
    }
}
public static class AccountSecurityRegistration
{
    public static IServiceCollection AddAccountSecurity(this IServiceCollection services,IConfiguration config)
    {
        services.Configure<RecoveryEmailOptions>(config.GetSection("RecoveryEmail"));
        services.AddScoped<RecoveryEmailService>();
        services.AddScoped<IRecoveryEmailService>(s => s.GetRequiredService<RecoveryEmailService>());
        services.AddScoped<IEmailSender>(s => s.GetRequiredService<RecoveryEmailService>());
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan=TimeSpan.FromHours(1));
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval=TimeSpan.Zero);
        services.AddRateLimiter(o => {
            o.RejectionStatusCode=StatusCodes.Status429TooManyRequests;
            o.AddPolicy("recuperacion",context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "desconocido",
                _ => new FixedWindowRateLimiterOptions { PermitLimit=5,Window=TimeSpan.FromHours(1),QueueLimit=0,AutoReplenishment=true }));
        });
        return services;
    }
}
