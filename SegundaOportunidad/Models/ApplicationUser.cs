using Microsoft.AspNetCore.Identity;

namespace SegundaOportunidad.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public DateTime? RegisteredAtUtc { get; set; }
        public DateTime? AccountSuspendedUntilUtc { get; set; }
        public DateTime? PublishingSuspendedUntilUtc { get; set; }
        public string SanctionVersion { get; set; } = Guid.NewGuid().ToString();
        public string? Dni { get; set; }
        public bool IsVerified { get; set; } = false;
        public DateTime? VerifiedAtUtc { get; set; }
        public string? VerifiedByAdminId { get; set; }
        
        // Estadísticas de impacto
        public decimal Co2Saved { get; set; } = 0;
        public int ObjectsReused { get; set; } = 0;
        public int FamiliesHelped { get; set; } = 0;
        
        // Reputación
        public decimal Rating { get; set; } = 5.0m;
    }
}
