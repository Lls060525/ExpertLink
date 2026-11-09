using System.ComponentModel.DataAnnotations;

namespace ExpertLink.Models;

public class Notification
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [Required, MaxLength(300)]
    public string Message { get; set; } = "";

    [MaxLength(200)]
    public string? Url { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
