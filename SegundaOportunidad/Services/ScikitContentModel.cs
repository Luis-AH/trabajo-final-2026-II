using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SegundaOportunidad.Services;

public sealed class ScikitContentModel(IConfiguration config,IWebHostEnvironment environment)
{
    private readonly object gate=new();
    private DateTime loadedAt;
    private Artifact? artifact;
    private string version="";
    public string ModelPath=>Path.GetFullPath(config["Ml:ModelPath"] ?? Path.Combine(environment.ContentRootPath,"App_Data","recommender.json"));
    public bool Ready {get {try {Load();return true;}catch(Exception ex) when(ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException or ArgumentException){return false;}}}
    public string Version {get {Load();return version;}}
    public string Algorithm {get {Load();return artifact!.Algorithm;}}
    public static string Normalize(string text)
    {
        var normalized=text.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormKD);
        var ascii=string.Concat(normalized.Where(c=>c<128 && (char.IsAsciiLetterOrDigit(c)||char.IsWhiteSpace(c))));
        return Regex.Replace(ascii,@"\s+"," ").Trim();
    }
    private void Load()
    {
        lock(gate)
        {
            var stamp=File.GetLastWriteTimeUtc(ModelPath);
            if(artifact!=null && loadedAt==stamp)return;
            var bytes=File.ReadAllBytes(ModelPath);
            var parsed=JsonSerializer.Deserialize<Artifact>(bytes,new JsonSerializerOptions {PropertyNameCaseInsensitive=true}) ?? throw new InvalidDataException("Modelo vacío.");
            if(parsed.Schema!=1 || parsed.Vocabulary==null || parsed.Idf==null || parsed.FieldWeights==null
                || parsed.Vocabulary.Count is <1 or >12000 || parsed.Vocabulary.Count!=parsed.Idf.Length
                || parsed.Normalization!="NFKD-ascii-alphanumeric-whitespace-v1"
                || parsed.Idf.Any(x=>!double.IsFinite(x)||x<=0)
                || parsed.Vocabulary.Keys.Any(k=>k.Length is <3 or >5)
                || parsed.Vocabulary.Values.Any(i=>i<0||i>=parsed.Idf.Length)
                || parsed.Vocabulary.Values.Distinct().Count()!=parsed.Vocabulary.Count)
                throw new InvalidDataException("Modelo TF-IDF no compatible.");
            if(parsed.FieldWeights.GetValueOrDefault("title")!=3||parsed.FieldWeights.GetValueOrDefault("category")!=2||parsed.FieldWeights.GetValueOrDefault("description")!=1
                ||parsed.FieldWeights.GetValueOrDefault("mode")!=1||parsed.FieldWeights.GetValueOrDefault("condition")!=1)
                throw new InvalidDataException("Pesos de campos no compatibles con el pipeline publicado.");
            artifact=parsed;version=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));loadedAt=stamp;
        }
    }
    public Dictionary<int,double> Transform(string text)
    {
        Load();
        var a=artifact!;
        var normalized=Normalize(text);
        var vector=new Dictionary<int,double>();
        for(var n=3;n<=5;n++)for(var start=0;start+n<=normalized.Length;start++)
            if(a.Vocabulary.TryGetValue(normalized.Substring(start,n),out var index))vector[index]=vector.GetValueOrDefault(index)+1;
        foreach(var key in vector.Keys.ToArray())vector[key]*=a.Idf[key];
        var norm=Math.Sqrt(vector.Values.Sum(x=>x*x));
        if(norm>0)foreach(var key in vector.Keys.ToArray())vector[key]/=norm;
        return vector;
    }
    public static double Similarity(IReadOnlyDictionary<int,double> a,IReadOnlyDictionary<int,double> b)=>a.Sum(p=>p.Value*b.GetValueOrDefault(p.Key));
    public static Dictionary<int,double> Centroid(IEnumerable<IReadOnlyDictionary<int,double>> vectors)
    {
        var average=new Dictionary<int,double>();
        foreach(var vector in vectors)foreach(var pair in vector)average[pair.Key]=average.GetValueOrDefault(pair.Key)+pair.Value;
        var norm=Math.Sqrt(average.Values.Sum(x=>x*x));
        if(norm>0)foreach(var key in average.Keys.ToArray())average[key]/=norm;
        return average;
    }
    private sealed class Artifact
    {
        public int Schema {get;set;}
        public string Algorithm {get;set;}="";
        public string CorpusSha256 {get;set;}="";
        public string Normalization {get;set;}="";
        public Dictionary<string,int> Vocabulary {get;set;}=[];
        public Dictionary<string,int> FieldWeights {get;set;}=[];
        public double[] Idf {get;set;}=[];
    }
}
