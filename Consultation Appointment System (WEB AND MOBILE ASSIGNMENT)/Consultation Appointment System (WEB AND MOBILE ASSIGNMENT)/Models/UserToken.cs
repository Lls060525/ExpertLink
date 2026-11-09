using System.ComponentModel.DataAnnotations;

namespace ExpertLink.Models;

// Token for e-mail verification and password reset links
public class UserToken
{
    [Key, MaxLength(32)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [Required, MaxLength(10)]
    public string Purpose { get; set; } = "";   // "Verify" or "Reset"

    public DateTime Expiry { get; set; }
}
