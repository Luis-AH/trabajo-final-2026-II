using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Models;

namespace SegundaOportunidad.Data;

public sealed record DemoAccount(string Email,string Password);
public sealed record DemoSeedResult(int NuevasCuentas,int NuevasPublicaciones,int NuevosIntercambios,IReadOnlyList<DemoAccount> CuentasNuevas);
public sealed class DemoSeedSafetyException(string message):InvalidOperationException(message);
public sealed class DemoCatalogSeeder(ApplicationDbContext db,UserManager<ApplicationUser> users,RoleManager<IdentityRole> roles,IWebHostEnvironment environment,IConfiguration config)
{
    public async Task<DemoSeedResult> SeedAsync(CancellationToken ct=default)
    {
        if(!environment.IsDevelopment() && !environment.IsEnvironment("Demo"))throw new InvalidOperationException("El seeding DEMO solo se permite en Development o Demo, nunca Production.");
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        // Solo una cuenta externa de administración puede coexistir con la base DEMO vacía.
        var administrators=from membership in db.UserRoles join role in db.Roles on membership.RoleId equals role.Id
                           where role.NormalizedName=="ADMIN" select membership.UserId;
        if(await db.Users.AnyAsync(u=>!u.Id.StartsWith("demo-ml-user-")&&!administrators.Contains(u.Id),ct)
            ||await db.Articulos.IgnoreQueryFilters().AnyAsync(a=>!a.Id.StartsWith("demo-ml-art-"),ct)
            ||await db.Intercambios.AnyAsync(i=>!i.Id.StartsWith("demo-ml-exchange-"),ct)
            ||await db.Mensajes.AnyAsync(ct)||await db.Reportes.AnyAsync(ct))
            throw new DemoSeedSafetyException("Esta base contiene cuentas o actividad ajena al conjunto DEMO. Prepara una base de demostración separada; no se modificó ningún registro.");
        var path=Path.Combine(environment.ContentRootPath,"App_Data","demo-corpus.json");
        if(!File.Exists(path))path=Path.Combine(AppContext.BaseDirectory,"App_Data","demo-corpus.json");
        if(!File.Exists(path))path=Path.GetFullPath(Path.Combine(environment.ContentRootPath,"..","ml","demo-corpus.json"));
        var rows=JsonSerializer.Deserialize<List<DemoRow>>(await File.ReadAllTextAsync(path,ct),new JsonSerializerOptions {PropertyNameCaseInsensitive=true})??[];
        if(rows.Count!=48 || rows.Any(r=>!r.Id.StartsWith("demo-ml-art-")))throw new InvalidOperationException("Corpus DEMO inválido.");
        await CategoryCatalog.EnsureAsync(db,ct);
        if(!await roles.RoleExistsAsync("Admin") && !(await roles.CreateAsync(new IdentityRole("Admin"))).Succeeded)throw new InvalidOperationException("No se pudo crear rol para la demostración.");
        var accounts=new List<DemoAccount>();
        var configuredJson=config["SampleData:CredentialsJson"];
        Dictionary<string,string>? configuredPasswords=null;
        if(!string.IsNullOrWhiteSpace(configuredJson))
        {
            try
            {
                var configured=JsonSerializer.Deserialize<List<DemoAccount>>(configuredJson,new JsonSerializerOptions {PropertyNameCaseInsensitive=true})??[];
                if(configured.Count!=10||configured.Any(a=>string.IsNullOrWhiteSpace(a.Password))||configured.Select(a=>a.Email).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=10
                    ||SampleProfiles.Accounts.Any(a=>!configured.Any(c=>string.Equals(c.Email,a.Email,StringComparison.OrdinalIgnoreCase))))
                    throw new InvalidOperationException("SampleData:CredentialsJson debe contener las diez cuentas del catálogo y sus contraseñas privadas.");
                configuredPasswords=configured.ToDictionary(a=>a.Email,a=>a.Password,StringComparer.OrdinalIgnoreCase);
            }
            catch(JsonException){throw new InvalidOperationException("SampleData:CredentialsJson no tiene formato JSON válido; no se registra su contenido.");}
        }
        for(var number=1;number<=10;number++)
        {
            var id=$"demo-ml-user-{number:00}";
            var profile=SampleProfiles.Accounts[number-1];
            var email=profile.Email;
            var existing=await users.FindByIdAsync(id);
            if(existing!=null)
            {
                if((existing.Email!=email && existing.Email!=$"demo{number:00}@example.invalid") || (!existing.FullName.StartsWith("[DEMO]")&&existing.FullName!=profile.Name))throw new DemoSeedSafetyException("Colisión de identificadores: no se alteró la cuenta existente.");
                continue;
            }
            var password=configuredPasswords?.GetValueOrDefault(email)??"D!"+RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789",6)+"7a";
            var user=new ApplicationUser {Id=id,UserName=email,Email=email,EmailConfirmed=true,FullName=profile.Name,RegisteredAtUtc=DateTime.UtcNow,
                IsVerified=true,VerifiedAtUtc=DateTime.UtcNow,VerifiedByAdminId="demo-ml-user-01",Dni=null};
            // El primer usuario se guarda sin referencia circular, luego se registra aprobación técnica DEMO.
            if(number==1)user.VerifiedByAdminId=null;
            if(!(await users.CreateAsync(user,password)).Succeeded)throw new InvalidOperationException("No se pudo crear cuenta DEMO.");
            if(number==1)
            {
                if(!(await users.AddToRoleAsync(user,"Admin")).Succeeded)throw new InvalidOperationException("No se pudo crear administrador DEMO.");
                user.VerifiedByAdminId=user.Id;await users.UpdateAsync(user);
            }
            accounts.Add(new(email,password));
        }
        var categories=await db.Categorias.ToDictionaryAsync(c=>c.Name,c=>c.Id,ct);
        var added=0;
        foreach(var row in rows)
        {
            var existing=await db.Articulos.IgnoreQueryFilters().SingleOrDefaultAsync(a=>a.Id==row.Id,ct);
            if(existing!=null)
            {
                if(!existing.UserId.StartsWith("demo-ml-user-"))throw new DemoSeedSafetyException("Colisión de artículo: no se modificó el existente.");
                continue;
            }
            var number=int.Parse(row.Id[^3..],System.Globalization.CultureInfo.InvariantCulture);
            var state=number switch {6=>EstadoModeracion.Oculta,12=>EstadoModeracion.Suspendida,18=>EstadoModeracion.Eliminada,_=>EstadoModeracion.Visible};
            db.Articulos.Add(new Articulo {Id=row.Id,Title=row.Title,Description=row.Description,UserId=$"demo-ml-user-{2+(number-1)%9:00}",CategoriaId=categories[row.Category],
                Modalidad=Enum.Parse<ModalidadArticulo>(row.Mode),EstadoFisico=Enum.Parse<EstadoFisico>(row.Condition),Price=row.Price,IsActive=number is not (24 or 30 or 36 or 42 or 48),Moderacion=state,
                ExchangeDetails=row.Mode=="Intercambio"?"Busco artículos relacionados con estudio, hogar o deporte.":null,ImageUrl=row.Image});
            added++;
        }
        await db.SaveChangesAsync(ct);
        var interactions=0;
        for(var index=0;index<6;index++)
        {
            var id="demo-ml-exchange-"+index;
            if(await db.Intercambios.AnyAsync(i=>i.Id==id,ct))continue;
            var requested=rows[1+index*6].Id;
            var offered=rows[2+index*6].Id;
            var request=await db.Articulos.IgnoreQueryFilters().SingleAsync(a=>a.Id==requested,ct);
            var offer=await db.Articulos.IgnoreQueryFilters().SingleAsync(a=>a.Id==offered,ct);
            db.Intercambios.Add(new Intercambio {Id=id,ProposerId=offer.UserId,ReceiverId=request.UserId,OfferedArticleId=offered,RequestedArticleId=requested,Estado=EstadoIntercambio.Pendiente});
            interactions++;
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(accounts.Count,added,interactions,accounts);
    }
    private sealed class DemoRow
    {
        public string Id {get;set;}=""; public string Title {get;set;}=""; public string Description {get;set;}="";public string Category {get;set;}="";
        public string Mode {get;set;}="";public string Condition {get;set;}="";public decimal? Price {get;set;}
        public string Image {get;set;}="/images/catalog-placeholder.svg";
    }
}
