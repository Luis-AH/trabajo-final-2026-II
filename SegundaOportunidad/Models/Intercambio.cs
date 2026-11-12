using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SegundaOportunidad.Models
{
    public enum EstadoIntercambio { Pendiente, Aceptado, Rechazado, Completado }

    public class Intercambio
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public EstadoIntercambio Estado { get; set; } = EstadoIntercambio.Pendiente;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Coordenadas precisas: se consultan únicamente por participantes autorizados.
        [MaxLength(120)] public string? EncuentroNombre { get; set; }
        public double? EncuentroLatitud { get; set; }
        public double? EncuentroLongitud { get; set; }
        [MaxLength(450)] public string? EncuentroPropuestoPor { get; set; }
        public DateTime? EncuentroPropuestoUtc { get; set; }
        [MaxLength(450)] public string? EncuentroConfirmadoPor { get; set; }
        public DateTime? EncuentroConfirmadoUtc { get; set; }
        [ConcurrencyCheck] public string EncuentroVersion { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string ProposerId { get; set; } = string.Empty;
        [ForeignKey("ProposerId")]
        public ApplicationUser? Proposer { get; set; }

        [Required]
        public string ReceiverId { get; set; } = string.Empty;
        [ForeignKey("ReceiverId")]
        public ApplicationUser? Receiver { get; set; }

        [Required]
        public string OfferedArticleId { get; set; } = string.Empty;
        [ForeignKey("OfferedArticleId")]
        public Articulo? OfferedArticle { get; set; }

        [Required]
        public string RequestedArticleId { get; set; } = string.Empty;
        [ForeignKey("RequestedArticleId")]
        public Articulo? RequestedArticle { get; set; }
    }
}
