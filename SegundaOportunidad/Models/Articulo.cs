using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SegundaOportunidad.Models
{
    public enum ModalidadArticulo { Venta, Intercambio, Donacion }
    public enum EstadoFisico { Nuevo, ComoNuevo, BuenEstado, Aceptable }

    public class Articulo
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [Required]
        public string Title { get; set; } = string.Empty;
        
        public string Description { get; set; } = string.Empty;
        
        public string? ImageUrl { get; set; }
        
        public ModalidadArticulo Modalidad { get; set; }
        public EstadoFisico EstadoFisico { get; set; }
        
        // Precio si es venta
        public decimal? Price { get; set; }
        
        // Lo que busca a cambio si es intercambio
        public string? ExchangeDetails { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Relaciones
        public int CategoriaId { get; set; }
        [ForeignKey("CategoriaId")]
        public Categoria? Categoria { get; set; }

        public string UserId { get; set; } = string.Empty;
        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }
    }
}
