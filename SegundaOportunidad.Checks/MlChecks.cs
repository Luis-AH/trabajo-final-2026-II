using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;
using SegundaOportunidad.Services;
using SegundaOportunidad.Controllers;

internal static class MlChecks
{
    public static async Task Run()
    {
        var root=Path.GetFullPath("SegundaOportunidad");
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions {ContentRootPath=root});
        builder.Logging.ClearProviders();builder.WebHost.ConfigureKestrel(o=>o.Listen(System.Net.IPAddress.Loopback,0));
        using var artifact=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"App_Data","recommender.json")));
        var model=new ScikitContentModel(builder.Configuration,builder.Environment);
        var validationRoot=Path.Combine(Path.GetTempPath(),"segunda-ml-validation-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(validationRoot);
        foreach(var invalid in new[]{"missing","malformed","null-vocabulary","null-idf","null-weights","empty-vocabulary","duplicate-index","wrong-weights"})
        {
            var path=Path.Combine(validationRoot,invalid+".json");
            var content=JsonNode.Parse(artifact.RootElement.GetRawText())!;
            switch(invalid)
            {
                case "null-vocabulary":content["vocabulary"]=null;break;
                case "null-idf":content["idf"]=null;break;
                case "null-weights":content["fieldWeights"]=null;break;
                case "empty-vocabulary":content["vocabulary"]=new JsonObject();content["idf"]=new JsonArray();break;
                case "duplicate-index":var keys=content["vocabulary"]!.AsObject().Select(p=>p.Key).Take(2).ToArray();content["vocabulary"]![keys[1]]=content["vocabulary"]![keys[0]]!.GetValue<int>();break;
                case "wrong-weights":content["fieldWeights"]!["mode"]=2;break;
            }
            if(invalid!="missing")await File.WriteAllTextAsync(path,invalid=="malformed"?"{invalid":content.ToJsonString());
            var invalidConfig=new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Ml:ModelPath",path}}).Build();
            Ensure(!new ScikitContentModel(invalidConfig,builder.Environment).Ready,"artefacto rechazado sin excepción HTTP: "+invalid);
        }
        var corpus=artifact.RootElement.GetProperty("corpus").EnumerateArray().ToDictionary(r=>r.GetProperty("id").GetString()!,r=>r.GetProperty("text").GetString()!);
        var vectors=corpus.ToDictionary(r=>r.Key,r=>model.Transform(r.Value));
        double maxError=0;
        foreach(var test in artifact.RootElement.GetProperty("parity").EnumerateArray())
        {
            var vector=model.Transform(test.GetProperty("query").GetString()!);
            foreach(var expected in test.GetProperty("scores").EnumerateObject())
                maxError=Math.Max(maxError,Math.Abs(expected.Value.GetDouble()-ScikitContentModel.Similarity(vector,vectors[expected.Name])));
        }
        Ensure(maxError<1e-10,"paridad Scikit-learn/.NET: error máximo="+maxError.ToString("G5",System.Globalization.CultureInfo.InvariantCulture));
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        var options=new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using(var db=new ApplicationDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Users.Add(new ApplicationUser {Id="ml-user",IsVerified=true,VerifiedAtUtc=DateTime.UtcNow,VerifiedByAdminId="ml-user"});
            using var source=JsonDocument.Parse(File.ReadAllText("ml/demo-corpus.json"));
            var rows=source.RootElement.EnumerateArray().Where(r=>r.GetProperty("available").GetBoolean()).ToList();
            var categories=rows.Select(r=>r.GetProperty("category").GetString()!).Distinct().Select((name,index)=>new {name,id=index+1}).ToDictionary(c=>c.name,c=>c.id);
            foreach(var category in categories)db.Categorias.Add(new Categoria {Id=category.Value,Name=category.Key});
            foreach(var row in rows)db.Articulos.Add(new Articulo {Id=row.GetProperty("id").GetString()!,Title=row.GetProperty("title").GetString()!,Description=row.GetProperty("description").GetString()!,UserId="ml-user",CategoriaId=categories[row.GetProperty("category").GetString()!],Modalidad=Enum.Parse<ModalidadArticulo>(row.GetProperty("mode").GetString()!),EstadoFisico=Enum.Parse<EstadoFisico>(row.GetProperty("condition").GetString()!)});
            db.Articulos.Add(new Articulo {Id="hidden-ml",Title="laptop",UserId="ml-user",CategoriaId=1,Moderacion=EstadoModeracion.Oculta});
            db.Users.AddRange(new ApplicationUser {Id="ml-unapproved"},new ApplicationUser {Id="ml-suspended",IsVerified=true,VerifiedAtUtc=DateTime.UtcNow,VerifiedByAdminId="ml-user",AccountSuspendedUntilUtc=DateTime.UtcNow.AddDays(1)});
            db.Articulos.AddRange(
                new Articulo {Id="inactive-ml",Title="laptop para estudiar",UserId="ml-user",CategoriaId=1,IsActive=false},
                new Articulo {Id="suspended-ml",Title="laptop para estudiar",UserId="ml-user",CategoriaId=1,Moderacion=EstadoModeracion.Suspendida},
                new Articulo {Id="removed-ml",Title="laptop para estudiar",UserId="ml-user",CategoriaId=1,Moderacion=EstadoModeracion.Eliminada},
                new Articulo {Id="unapproved-ml",Title="laptop para estudiar",UserId="ml-unapproved",CategoriaId=1},
                new Articulo {Id="seller-suspended-ml",Title="laptop para estudiar",UserId="ml-suspended",CategoriaId=1},
                new Articulo {Id="completed-ml",Title="laptop para estudiar",UserId="ml-user",CategoriaId=1});
            db.Intercambios.Add(new Intercambio {Id="ml-completed",ProposerId="ml-user",ReceiverId="ml-unapproved",OfferedArticleId="completed-ml",RequestedArticleId="unapproved-ml",Estado=EstadoIntercambio.Completado});
            await db.SaveChangesAsync();
        }
        builder.Services.AddDbContext<ApplicationDbContext>(o=>o.UseSqlite(connection));builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(model);builder.Services.AddScoped<MlRecommendationService>();
        builder.Services.AddControllers().AddApplicationPart(typeof(MlController).Assembly);
        await using var app=builder.Build();app.MapControllers();await app.StartAsync();
        try
        {
            using var http=new HttpClient {BaseAddress=new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())};
            using var response=await http.GetAsync("/api/ml/recomendaciones?consulta=laptop%20para%20estudiar&cantidad=5");
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Ensure(response.IsSuccessStatusCode&&json.RootElement.GetProperty("publicaciones").GetArrayLength()>0,"GET /api/ml/recomendaciones responde con inferencia real");
            Ensure(response.Headers.CacheControl?.NoStore==true,"resultados personales no se almacenan en cachés compartidas");
            Ensure(json.RootElement.GetProperty("publicaciones").EnumerateArray().All(a=>corpus.ContainsKey(a.GetProperty("id").GetString()!)),"IDs reales y ninguna publicación oculta");
            Ensure((await http.GetAsync("/api/ml/recomendaciones?cantidad=100")).StatusCode==System.Net.HttpStatusCode.BadRequest,"límite de recomendaciones validado");
            var expectedScores=artifact.RootElement.GetProperty("parity")[0].GetProperty("scores");
            Ensure(json.RootElement.GetProperty("publicaciones").EnumerateArray().All(p=>Math.Abs(p.GetProperty("similitud").GetDouble()-expectedScores.GetProperty(p.GetProperty("id").GetString()!).GetDouble())<1e-6),"extracción y scoring del endpoint coinciden con Python");
            using var firstRotation=JsonDocument.Parse(await http.GetStringAsync("/api/ml/recomendaciones?cantidad=2&rotacion=0"));
            using var nextRotation=JsonDocument.Parse(await http.GetStringAsync("/api/ml/recomendaciones?cantidad=2&rotacion=1"));
            var firstIds=firstRotation.RootElement.GetProperty("publicaciones").EnumerateArray().Select(p=>p.GetProperty("id").GetString()!).ToHashSet();
            var nextIds=nextRotation.RootElement.GetProperty("publicaciones").EnumerateArray().Select(p=>p.GetProperty("id").GetString()!).ToHashSet();
            Ensure(firstIds.Count==2&&nextIds.Count==2&&!firstIds.SetEquals(nextIds),"recargar cambia publicaciones entre candidatos afines, sin duplicados");
            Ensure(nextIds.All(corpus.ContainsKey),"rotación mantiene restricciones de visibilidad");
            using var rotatedQuery=JsonDocument.Parse(await http.GetStringAsync("/api/ml/recomendaciones?consulta=laptop%20para%20estudiar&cantidad=5&rotacion=2"));
            Ensure(rotatedQuery.RootElement.GetProperty("publicaciones").EnumerateArray().All(p=>Math.Abs(p.GetProperty("similitud").GetDouble()-expectedScores.GetProperty(p.GetProperty("id").GetString()!).GetDouble())<1e-6),"rotar no modifica las similitudes calculadas por Scikit-learn");
            using var noMatches=JsonDocument.Parse(await http.GetStringAsync("/api/ml/recomendaciones?consulta=zzzzzzzzzzzzzzzzzzzz&rotacion=3"));
            Ensure(noMatches.RootElement.GetProperty("publicaciones").GetArrayLength()==0,"rotación sin coincidencias no inventa resultados");
            Ensure((await http.GetAsync("/api/ml/recomendaciones?rotacion=-1")).StatusCode==System.Net.HttpStatusCode.BadRequest,"rotación negativa rechazada");
            Ensure((await http.GetAsync("/api/ml/recomendaciones?rotacion=texto")).StatusCode==System.Net.HttpStatusCode.BadRequest,"rotación no numérica rechazada");
            using var scope=app.Services.CreateScope();
            var context=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Users.Add(new ApplicationUser {Id="interest-user",IsVerified=true,VerifiedAtUtc=DateTime.UtcNow,VerifiedByAdminId="ml-user"});
            context.Articulos.Add(new Articulo {Id="owned-interest",Title="Laptop para estudiar",UserId="interest-user",CategoriaId=1});
            var wanted=corpus.First(p=>p.Value.Contains("laptop para clases")).Key;
            context.Intercambios.Add(new Intercambio {Id="interest",ProposerId="interest-user",ReceiverId="ml-user",OfferedArticleId="owned-interest",RequestedArticleId=wanted});
            await context.SaveChangesAsync();
            var recommendationService=new MlRecommendationService(context,model,TimeProvider.System);
            var personal=await recommendationService.RecommendAsync("interest-user",null,null,5,default);
            Ensure(personal.Criterio.Contains("tus publicaciones")&&personal.Publicaciones.All(p=>p.Id!="owned-interest"),"preferencias propias y exclusión de artículos propios");
            var cold=await recommendationService.RecommendAsync(null,null,null,5,default);
            Ensure(cold.Criterio.Contains("representativo")&&cold.Publicaciones.Count>0,"sin historial usa contenido sin inventar popularidad");
            foreach(var denied in new[]{"hidden-ml","inactive-ml","suspended-ml","removed-ml","unapproved-ml","seller-suspended-ml","completed-ml"})
            {
                using var unavailable=await http.GetAsync("/api/ml/recomendaciones?articuloId="+denied);
                using var deniedJson=JsonDocument.Parse(await unavailable.Content.ReadAsStringAsync());
                Ensure(deniedJson.RootElement.GetProperty("publicaciones").GetArrayLength()==0,"no usa como base artículo no autorizado: "+denied);
            }
            builder.Configuration["Ml:ModelPath"]=Path.Combine(validationRoot,"does-not-exist.json");
            using var missingResponse=await http.GetAsync("/api/ml/recomendaciones?consulta=libros");
            Ensure(missingResponse.StatusCode==System.Net.HttpStatusCode.ServiceUnavailable,"modelo ausente devuelve HTTP 503 controlado");
            builder.Configuration["Ml:ModelPath"]=null;
        }
        finally {await app.StopAsync();}
    }
    static void Ensure(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("OK ML: "+text);}
}
