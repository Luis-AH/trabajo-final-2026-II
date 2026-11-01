namespace SegundaOportunidad.Models;
// Evita que mensajes antiguos pendientes en RabbitMQ reaparezcan tras limpiar la base local.
public sealed class EstadoSistema
{
    public string Id { get; set; } = "";
    public DateTime FechaUtc { get; set; }
}
