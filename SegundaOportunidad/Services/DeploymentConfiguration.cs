using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.Sqlite;

namespace SegundaOportunidad.Services;

public static class DeploymentConfiguration
{
    public static void AddDeploymentConfiguration(this WebApplicationBuilder builder,string connectionString)
    {
        var sqlite=new SqliteConnectionStringBuilder(connectionString);
        if(!builder.Environment.IsDevelopment() && !Path.IsPathFullyQualified(sqlite.DataSource))
            throw new InvalidOperationException("Production requiere ConnectionStrings:DefaultConnection con una ruta SQLite absoluta en almacenamiento persistente.");
        if(sqlite.DataSource!=":memory:")
        {
            var parent=Path.GetDirectoryName(Path.GetFullPath(sqlite.DataSource));
            if(parent!=null) Directory.CreateDirectory(parent);
        }
        var protection=builder.Services.AddDataProtection().SetApplicationName("SegundaOportunidad");
        var keyPath=builder.Configuration["DataProtection:KeyPath"];
        if(!string.IsNullOrWhiteSpace(keyPath))
        {
            if(!Path.IsPathFullyQualified(keyPath)) throw new InvalidOperationException("DataProtection:KeyPath debe ser una ruta absoluta.");
            Directory.CreateDirectory(keyPath);
            protection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
        }
        else if(!builder.Environment.IsDevelopment())
            throw new InvalidOperationException("Production requiere DataProtection:KeyPath en el disco persistente para conservar cookies y tokens entre despliegues.");
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
    }
}
