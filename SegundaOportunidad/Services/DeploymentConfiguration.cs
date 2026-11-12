using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.Sqlite;

namespace SegundaOportunidad.Services;

public static class DeploymentConfiguration
{
    public static string AddDeploymentConfiguration(this WebApplicationBuilder builder,string connectionString)
    {
        var sqlite=new SqliteConnectionStringBuilder(connectionString);
        var fileDatabase = sqlite.DataSource != ":memory:" && sqlite.Mode != SqliteOpenMode.Memory;
        if(fileDatabase)
        {
            sqlite.DataSource = Path.GetFullPath(sqlite.DataSource, builder.Environment.ContentRootPath);
            Directory.CreateDirectory(Path.GetDirectoryName(sqlite.DataSource)!);
        }
        builder.Configuration["ConnectionStrings:DefaultConnection"] = sqlite.ConnectionString;
        var protection=builder.Services.AddDataProtection().SetApplicationName("SegundaOportunidad");
        var keyPath=builder.Configuration["DataProtection:KeyPath"];
        if(string.IsNullOrWhiteSpace(keyPath) && !builder.Environment.IsDevelopment())
        {
            var dataDirectory = fileDatabase
                ? Path.GetDirectoryName(sqlite.DataSource)!
                : Path.Combine(Path.GetTempPath(), "segunda-oportunidad");
            keyPath = Path.Combine(dataDirectory, "keys");
            builder.Configuration["DataProtection:KeyPath"] = keyPath;
        }
        if(!string.IsNullOrWhiteSpace(keyPath))
        {
            keyPath = Path.GetFullPath(keyPath, builder.Environment.ContentRootPath);
            builder.Configuration["DataProtection:KeyPath"] = keyPath;
            Directory.CreateDirectory(keyPath);
            protection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
        }
        builder.Services.Configure<ForwardedHeadersOptions>(o=> {
            o.ForwardedHeaders=ForwardedHeaders.XForwardedFor|ForwardedHeaders.XForwardedProto;
            o.ForwardLimit=1;
            foreach(var child in builder.Configuration.GetSection("ReverseProxy:KnownProxies").GetChildren())
            {
                if(!IPAddress.TryParse(child.Value,out var address)) throw new InvalidOperationException("Proxy de confianza no válido.");
                o.KnownProxies.Add(address);
                if(address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork) o.KnownProxies.Add(address.MapToIPv6());
            }
            foreach(var child in builder.Configuration.GetSection("ReverseProxy:KnownNetworks").GetChildren())
            {
                if(!System.Net.IPNetwork.TryParse(child.Value,out var network)) throw new InvalidOperationException("Red de proxy de confianza no válida.");
                o.KnownIPNetworks.Add(network);
            }
        });
        if(!builder.Environment.IsDevelopment()) builder.Services.ConfigureApplicationCookie(o=>o.Cookie.SecurePolicy=CookieSecurePolicy.Always);
        return sqlite.ConnectionString;
    }
}
