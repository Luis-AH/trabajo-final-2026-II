using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace SegundaOportunidad.Models;
public sealed class NombreCompletoAttribute : ValidationAttribute
{
    public NombreCompletoAttribute() { ErrorMessage="Escribe tus nombres y apellidos completos (máximo 150 caracteres)."; }
    public static string Normalizar(string value) => Regex.Replace(value.Trim(),@"\s+"," ");
    public override bool IsValid(object? value) => value is string name && name.Length<=150
        && !name.Any(char.IsControl) && Normalizar(name).Split(' ',StringSplitOptions.RemoveEmptyEntries).Count(word => word.Any(char.IsLetter))>=2;
}
