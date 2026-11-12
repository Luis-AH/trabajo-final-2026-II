using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SegundaOportunidad.Models;

namespace SegundaOportunidad.Services;

public static class ChatQueryParser
{
    public static string Normalize(string text) => string.Concat(text.ToLowerInvariant().Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).Normalize(NormalizationForm.FormC);
    private static readonly string[][] Families = [
        ["bicicleta", "bicicletas", "bici", "bicis"], ["celular", "celulares", "telefono", "telefonos", "smartphone", "smartphones", "movil", "moviles"],
        ["laptop", "laptops", "portatil", "portatiles", "computadora", "computadoras"], ["libro", "libros"],
        ["mochila", "mochilas"], ["silla", "sillas"], ["electrica", "electricas", "electrico", "electricos"]
    ];
    private static readonly HashSet<string> Stop = ("que cuales cual hay tiene tienen disponible disponibles muestra muestrame mostrar busca buscar busco " +
        "quiero necesito tienes dame recomienda recomiendame recomendar recomendacion recomendaciones para estudiar universidad " +
        "mejor mejores economico economica economicos economicas barato barata baratos baratas " +
        "producto productos articulo articulos publicacion publicaciones precio precios menor menos hasta maximo presupuesto mayor mas " +
        "compara comparar comparame compara entre con frente versus vendedor vendedores tiene tienen confiable confiables valoracion valoraciones " +
        "verificado verificados conviene comprar comprarme por soles sol venta intercambio donacion categoria categorias estado " +
        "los las una uno unos unas del como este esta esto estos estas me de el la un en y o a es al se mi su son " +
        "porfavor favor puedes podrias cuanto cuesta cuestan disponibles plataforma segundaoportunidad nuevo nuevos nueva nuevas buen aceptable usado usados").Split(' ').ToHashSet();

    public static ChatCatalogQuery Parse(string message, IReadOnlyList<Categoria> categories)
    {
        var text = Normalize(message);
        var q = new ChatCatalogQuery {
            Restricted = Regex.IsMatch(text, @"\b(dni|contrasena|password|correo|correos|email|emails|telefono personal|reportes confidenciales|sanciones|datos administrativos)\b"),
            Cheap = Regex.IsMatch(text, @"\b(economic\w*|barat\w*|menor precio)\b"),
            Recommend = Regex.IsMatch(text, @"\b(recom\w*|mejor\w*|conviene)\b"),
            Compare = Regex.IsMatch(text, @"\b(compara\w*|versus|frente)\b"),
            Sellers = Regex.IsMatch(text, @"\b(vendedor\w*|confiab\w*|valoraci\w*)\b")
        };
        foreach(Match m in Regex.Matches(message, @"/Articulos/Details/([A-Za-z0-9_-]{1,100})", RegexOptions.IgnoreCase)) q.ArticleIds.Add(m.Groups[1].Value);
        var between = Regex.Match(text, @"entre\s+(?:s/\s*)?(\d+(?:[.,]\d{1,2})?)\s+y\s+(?:s/\s*)?(\d+(?:[.,]\d{1,2})?)");
        if(between.Success) { q.MinPrice = Amount(between.Groups[1].Value); q.MaxPrice = Amount(between.Groups[2].Value); }
        else
        {
            var max = Regex.Match(text, @"(?:menor\s+(?:a|de)|menos\s+de|hasta|maximo(?:\s+de)?|presupuesto(?:\s+de)?|por)\s*(?:s/\s*|s\.\s*)?(\d+(?:[.,]\d{1,2})?)");
            if(!max.Success) max=Regex.Match(text,@"s/\s*(\d+(?:[.,]\d{1,2})?)");
            if(max.Success) {q.MaxPrice=Amount(max.Groups[1].Value);q.ExclusiveMax=text.Contains("menor") || text.Contains("menos de");}
            var min=Regex.Match(text,@"(?:mayor\s+(?:a|de)|desde|minimo(?:\s+de)?)\s*(?:s/\s*)?(\d+(?:[.,]\d{1,2})?)");
            if(min.Success) q.MinPrice=Amount(min.Groups[1].Value);
        }
        if(text.Contains("donacion") || text.Contains("gratis")) q.Mode=ModalidadArticulo.Donacion;
        else if(text.Contains("intercambio")) q.Mode=ModalidadArticulo.Intercambio;
        else if(text.Contains("venta")) q.Mode=ModalidadArticulo.Venta;
        if(Regex.IsMatch(text,@"\b(nuevo|nueva|nuevos|nuevas)\b")) q.Condition=EstadoFisico.Nuevo;
        if(text.Contains("como nuevo")) q.Condition=EstadoFisico.ComoNuevo;
        if(text.Contains("buen estado")) q.Condition=EstadoFisico.BuenEstado;
        if(text.Contains("aceptable")) q.Condition=EstadoFisico.Aceptable;
        var termsText=text;
        // Las categorías explícitas usan solo IDs existentes; no son nombres inventados por IA.
        foreach(var category in categories.OrderByDescending(c=>c.Name.Length))
        {
            var name=Normalize(category.Name).Trim('.');
            if(name.Length>2 && text.Contains("categoria") && termsText.Contains(name))
            { q.CategoryIds.Add(category.Id); termsText=termsText.Replace(name, " "); }
        }
        if(q.ArticleIds.Count==0)
        {
            if(Regex.IsMatch(text,@"\b(estudiar|estudio|universidad|clases)\b"))
                q.TermGroups.Add(["libro","laptop","portatil","computadora","mochila","escritorio","silla","lampara","cuaderno"]);
            var words=Regex.Matches(termsText,@"\p{L}{3,}").Select(m=>m.Value).Where(w=>!Stop.Contains(w)).Distinct().Take(8);
            foreach(var word in words)
            {
                if(word is "estudio" or "clases" or "registradas" or "reales") continue;
                var family=Families.FirstOrDefault(f=>f.Contains(word)) ?? [word];
                if(!q.TermGroups.Any(g=>g.SequenceEqual(family))) q.TermGroups.Add(family);
            }
        }
        q.IsCatalog=q.Restricted || q.ArticleIds.Count>0 || q.MaxPrice!=null || q.MinPrice!=null || q.CategoryIds.Count>0 || q.Recommend || q.Compare || q.Sellers || q.Mode!=null ||
            Regex.IsMatch(text,@"\b(articulo\w*|producto\w*|publicaci\w*|disponible\w*|busc\w*|muestra\w*|tienes|necesito|precio\w*|categoria\w*)\b") ||
            Families.Any(f=>f.Any(w=>Regex.IsMatch(text,@"\b"+Regex.Escape(w)+@"\b")));
        return q;
    }
    private static decimal? Amount(string text) => decimal.TryParse(text.Replace(',','.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,out var n) && n>=0 && n<=10000000 ? n : null;
}
