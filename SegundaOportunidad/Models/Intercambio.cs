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
