using System.ComponentModel.DataAnnotations;

namespace SegundaOportunidad.Models;
public sealed class CambiarClaveViewModel
{
    [Required, DataType(DataType.Password)] public string Actual { get; set; } = "";
    [Required, StringLength(100,MinimumLength=6), DataType(DataType.Password)] public string Nueva { get; set; } = "";
    [Required, Compare(nameof(Nueva)), DataType(DataType.Password)] public string Confirmacion { get; set; } = "";
}
public sealed class RecuperarClaveViewModel
{
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
}
public sealed class RestablecerClaveViewModel
{
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [Required] public string Codigo { get; set; } = "";
    [Required, StringLength(100,MinimumLength=6), DataType(DataType.Password)] public string Nueva { get; set; } = "";
    [Required, Compare(nameof(Nueva)), DataType(DataType.Password)] public string Confirmacion { get; set; } = "";
}
public sealed class SeguridadViewModel
{
    public bool CorreoConfigurado { get; set; }
    public List<string> RolesPropios { get; set; } = [];
    public List<RolSeguridadFila> Roles { get; set; } = [];
}
public sealed record RolSeguridadFila(string Nombre,int Usuarios);
