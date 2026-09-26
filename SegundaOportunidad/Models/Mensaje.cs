using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SegundaOportunidad.Models
{
    public class Mensaje
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        // El MessageId de RabbitMQ para garantizar la idempotencia
        public string? RabbitMqMessageId { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;

        [Required]
        public string SenderId { get; set; } = string.Empty;
        [ForeignKey("SenderId")]
        public ApplicationUser? Sender { get; set; }

        [Required]
        public string ReceiverId { get; set; } = string.Empty;
        [ForeignKey("ReceiverId")]
        public ApplicationUser? Receiver { get; set; }
    }
}
