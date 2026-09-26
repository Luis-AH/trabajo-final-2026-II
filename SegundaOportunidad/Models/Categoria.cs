using System.ComponentModel.DataAnnotations;

namespace SegundaOportunidad.Models
{
    public class Categoria
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Name { get; set; } = string.Empty;
        public string? Icon { get; set; }
    }
}
