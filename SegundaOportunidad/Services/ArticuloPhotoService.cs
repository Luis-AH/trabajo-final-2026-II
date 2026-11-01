using SegundaOportunidad.Models;

namespace SegundaOportunidad.Services;
public static class ArticuloPhotoService
{
    public const int MaxBytes=4*1024*1024;
    public static async Task<FotoArticulo> LeerAsync(IFormFile? file,CancellationToken ct)
    {
        if(file==null || file.Length==0) throw new ArgumentException("Añade una fotografía del artículo.");
        if(file.Length>MaxBytes) throw new ArgumentException("La fotografía no puede superar 4 MB.");
        await using var stream=file.OpenReadStream();
        byte[] data;
        try { data=await AdminGuardClient.LimitedReadAsync(stream,MaxBytes,ct); }
        catch(InvalidOperationException) { throw new ArgumentException("La fotografía no puede superar 4 MB."); }
        var png=data.Length>=45 && data.AsSpan(0,8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})
            && System.Text.Encoding.ASCII.GetString(data,12,4)=="IHDR" && System.Text.Encoding.ASCII.GetString(data,data.Length-8,4)=="IEND";
        var jpeg=data.Length>=32 && data[0]==255 && data[1]==216 && data[2]==255 && data[^2]==255 && data[^1]==217;
        var extension=Path.GetExtension(file.FileName).ToLowerInvariant();
        if((!png && !jpeg) || (png && extension!=".png") || (jpeg && extension is not ".jpg" and not ".jpeg"))
            throw new ArgumentException("Selecciona una fotografía PNG o JPEG válida; no se admiten SVG ni archivos renombrados.");
        return new FotoArticulo { Contenido=data,Mime=png ? "image/png" : "image/jpeg" };
    }
}
