using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;

namespace SegundaOportunidad.Services;
public interface IAdminGuardImageReader { Task<(byte[] Bytes, string Mime)> LeerAsync(string url, CancellationToken ct); }
public sealed class AdminGuardImageReader(HttpClient http, IOptions<AdminGuardOptions> options, ApplicationDbContext? db = null) : IAdminGuardImageReader
{
    public async Task<(byte[] Bytes, string Mime)> LeerAsync(string url, CancellationToken ct)
    {
        const string prefix="/Articulos/Imagen/";
        if(url.StartsWith(prefix,StringComparison.Ordinal))
        {
            if(db==null) throw new InvalidOperationException("No está disponible el almacén local de fotografías.");
            var id=url[prefix.Length..];
            var local=await db.FotosArticulos.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(f => f.ArticuloId==id,ct);
            if(local==null) throw new InvalidOperationException("La fotografía local no existe.");
            return (local.Contenido,local.Mime);
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo)
            || !options.Value.ImageHosts.Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("La imagen debe usar HTTPS y un dominio autorizado en AdminGuard:ImageHosts.");
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("No se pudo descargar la fotografía para analizarla.");
        var bytes = await AdminGuardClient.LimitedReadAsync(await response.Content.ReadAsStreamAsync(ct), 4 * 1024 * 1024, ct);
        var mime = bytes.Length >= 8 && bytes.AsSpan(0,8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) ? "image/png"
            : bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 ? "image/jpeg"
            : bytes.Length >= 12 && System.Text.Encoding.ASCII.GetString(bytes,0,4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes,8,4) == "WEBP" ? "image/webp" : null;
        if (mime == null) throw new InvalidOperationException("Formato no compatible: usa una fotografía JPEG, PNG o WebP.");
        return (bytes, mime);
    }
    public static bool PublicAddress(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)) return false;
        var b = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork)
            return !(b[0] is 0 or 10 or 127 || b[0] >= 224 || (b[0] == 169 && b[1] == 254) || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 100 && b[1] is >= 64 and <= 127));
        return (b[0] & 0xe0) == 0x20; // Solo IPv6 global unicast; excluye local, multicast y link-local.
    }
    public static SocketsHttpHandler Handler() => new()
    {
        AllowAutoRedirect = false, UseProxy = false,
        ConnectCallback = async (context, ct) => {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
            foreach (var ip in addresses.Where(PublicAddress))
            {
                var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try { await socket.ConnectAsync(new IPEndPoint(ip, context.DnsEndPoint.Port), ct); return new NetworkStream(socket, ownsSocket: true); }
                catch { socket.Dispose(); }
            }
            throw new HttpRequestException("No hay una dirección pública accesible para la imagen.");
        }
    };
}
