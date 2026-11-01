using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;
using SegundaOportunidad.Services;

internal static class UserChecks
{
    static void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("OK: " + label); }
    public static async Task Run(HttpClient client, DbContextOptions<ApplicationDbContext> options, IServiceProvider services)
    {
        Check((await client.GetAsync("/AdminUsuarios")).StatusCode == HttpStatusCode.OK, "Tabla usuarios renderizada");
        Check((await client.GetAsync("/AdminUsuarios/Detalle?id=u")).StatusCode == HttpStatusCode.OK, "Perfil con historial renderizado");
        Check((await client.PostAsync("/AdminUsuarios/Sancionar", new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode == HttpStatusCode.BadRequest, "Sanciones requieren antiforgery");
        client.DefaultRequestHeaders.Remove("X-Check-Role");
        Check((await client.GetAsync("/AdminUsuarios")).StatusCode == HttpStatusCode.Unauthorized, "Usuarios requiere login");
        client.DefaultRequestHeaders.Add("X-Check-Role", "Usuario");
        Check((await client.GetAsync("/AdminUsuarios")).StatusCode == HttpStatusCode.Forbidden, "Usuarios rechaza rol común");
        await using var db = new ApplicationDbContext(options);
        var clock = new CheckClock();
        var service = new SancionUsuarioService(db, clock);
        var user = await db.Users.SingleAsync(u => u.Id == "u");
        async Task Reject(TipoSancion tipo, int? horas, string actor, string version)
        {
            try { await service.AplicarAsync("u", actor, tipo, "Motivo de prueba", horas, version); throw new Exception("Aceptó transición inválida"); }
            catch (Exception ex) when (ex is ArgumentException or DbUpdateConcurrencyException) { Console.WriteLine("OK: Rechazo " + tipo); }
        }
        await Reject(TipoSancion.SuspenderCuenta, 2, "u", user.SanctionVersion);
        await Reject(TipoSancion.SuspenderPublicaciones, 2, "u", user.SanctionVersion);
        await Reject(TipoSancion.ReactivarCuenta, null, "checks", user.SanctionVersion);
        await Reject(TipoSancion.SuspenderCuenta, 0, "checks", user.SanctionVersion);
        await service.AplicarAsync("u", "checks", TipoSancion.Advertencia, "Advertencia justificada", null, user.SanctionVersion);
        var old = user.SanctionVersion;
        await service.AplicarAsync("u", "checks", TipoSancion.SuspenderCuenta, "Suspensión justificada", 2, user.SanctionVersion);
        Check(user.AccountSuspendedUntilUtc == clock.GetUtcNow().UtcDateTime.AddHours(2), "Duración exacta de suspensión");
        await Reject(TipoSancion.ReactivarCuenta, null, "checks", old);
        await Reject(TipoSancion.SuspenderCuenta, 1, "checks", user.SanctionVersion);
        await service.AplicarAsync("u", "checks", TipoSancion.SuspenderPublicaciones, "Publicación restringida", 2, user.SanctionVersion);
        await service.AplicarAsync("u", "checks", TipoSancion.ReactivarCuenta, "Reactivación justificada", null, user.SanctionVersion);
        Check(user.AccountSuspendedUntilUtc == null && user.PublishingSuspendedUntilUtc != null, "Reactivación conserva restricciones independientes");
        clock.Now = clock.Now.AddHours(3);
        await service.AplicarAsync("u", "checks", TipoSancion.SuspenderPublicaciones, "Nueva suspensión tras vencer", 1, user.SanctionVersion);
        Check(await db.SancionesUsuarios.CountAsync() == 5, "Historial completo de cinco decisiones");
        var actor = await db.Users.SingleAsync(u => u.Id == "checks");
        actor.PublishingSuspendedUntilUtc = DateTime.UtcNow.AddHours(1);
        await db.SaveChangesAsync();
        Check((await client.GetAsync("/Articulos/Create")).StatusCode == HttpStatusCode.Forbidden, "Sesión abierta no puede publicar tras sanción");
        actor.AccountSuspendedUntilUtc = DateTime.UtcNow.AddHours(1);
        await db.SaveChangesAsync();
        Check((await client.GetAsync("/probe")).StatusCode == HttpStatusCode.Forbidden, "Cuenta suspendida bloquea solicitudes de sesión abierta");
        using (var scope = services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
            Check(!await manager.CanSignInAsync(actor), "Identity rechaza inicio de sesión de cuenta suspendida");
        }
        actor.PublishingSuspendedUntilUtc = DateTime.UtcNow.AddHours(-1);
        actor.AccountSuspendedUntilUtc = DateTime.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();
        Check((await client.GetAsync("/Articulos/Create")).StatusCode == HttpStatusCode.OK, "Vencimiento automático restablece acceso");
        using (var scope = services.CreateScope())
            Check(await scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>().CanSignInAsync(actor), "Identity permite acceso tras vencer suspensión");
    }
    sealed class CheckClock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
