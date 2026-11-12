using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SegundaOportunidad.Models
{
    public enum ModalidadArticulo { Venta, Intercambio, Donacion }
    public enum EstadoFisico { Nuevo, ComoNuevo, BuenEstado, Aceptable }
    public enum EstadoModeracion { Visible, Oculta, Suspendida, Eliminada }

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
        public EstadoModeracion Moderacion { get; set; } = EstadoModeracion.Visible;
        [ConcurrencyCheck]
        public string ModeracionVersion { get; set; } = Guid.NewGuid().ToString();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Solo ubicación general validada por servidor; nunca coordenadas del domicilio.
        [MaxLength(120)] public string? Departamento { get; set; }
        [MaxLength(120)] public string? Provincia { get; set; }
        [MaxLength(120)] public string? Distrito { get; set; }
        [MaxLength(200)] public string? UbicacionPlaceId { get; set; }
        public double? LatitudPublica { get; set; }
        public double? LongitudPublica { get; set; }
        public DateTime? UbicacionValidadaUtc { get; set; }

        // Relaciones
        public int CategoriaId { get; set; }
        [ForeignKey("CategoriaId")]
        public Categoria? Categoria { get; set; }

        public string UserId { get; set; } = string.Empty;
        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }
    }
}
