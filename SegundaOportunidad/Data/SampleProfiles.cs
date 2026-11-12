namespace SegundaOportunidad.Data;

public static class SampleProfiles
{
    public static Dictionary<string, string>? ReadPasswords(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var configured = System.Text.Json.JsonSerializer.Deserialize<List<DemoAccount>>(
                json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

            if (configured.Count != 10
                || configured.Any(account => string.IsNullOrWhiteSpace(account.Password))
                || configured.Select(account => account.Email).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 10
                || Accounts.Any(profile => !configured.Any(account =>
                    string.Equals(account.Email, profile.Email, StringComparison.OrdinalIgnoreCase))))
            {
                throw new InvalidOperationException("SampleData:CredentialsJson debe contener las diez cuentas del catálogo y sus contraseñas privadas.");
            }

            return configured.ToDictionary(account => account.Email, account => account.Password, StringComparer.OrdinalIgnoreCase);
        }
        catch (System.Text.Json.JsonException)
        {
            throw new InvalidOperationException("SampleData:CredentialsJson no tiene formato JSON válido.");
        }
    }
    public static readonly (string Name,string Email)[] Accounts =
    [
        ("Renata Ramos","sofia.ramos@example.invalid"),
        ("Mateo Torres","mateo.torres@example.invalid"),
        ("Valeria Soto","valeria.soto@example.invalid"),
        ("Diego Flores","diego.flores@example.invalid"),
        ("Camila Rojas","camila.rojas@example.invalid"),
        ("Sebastián Vega","sebastian.vega@example.invalid"),
        ("Lucía Castro","lucia.castro@example.invalid"),
        ("Andrés Mendoza","andres.mendoza@example.invalid"),
        ("Paula Medina","paula.medina@example.invalid"),
        ("Nicolás Ruiz","nicolas.ruiz@example.invalid")
    ];
}
