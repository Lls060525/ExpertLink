using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ExpertLink.Models;

// ---------------------------------------------------------------------------
// User hierarchy (Table-Per-Hierarchy: one "Users" table with a Discriminator)
// ---------------------------------------------------------------------------
[Index(nameof(Email), IsUnique = true)]
public abstract class User
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Email { get; set; } = "";

    [Required, MaxLength(100)]
    public string Hash { get; set; } = "";

    [Required, MaxLength(100)]
    public string Name { get; set; } = "";

    [MaxLength(15)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? PhotoURL { get; set; }

    public bool IsActive { get; set; } = true;

    public bool EmailVerified { get; set; }

    // Temporary login blocking
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEnd { get; set; }

    // User preference
    [Required, MaxLength(10)]
    public string Theme { get; set; } = "light";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [NotMapped]
    public string Role => GetType().Name;   // "Admin", "Consultant" or "Client"

    public List<Notification> Notifications { get; set; } = [];
    public List<UserToken> Tokens { get; set; } = [];
}

public class Admin : User
{
}

public class Client : User
{
    [InverseProperty("Client")]
    public List<Appointment> Appointments { get; set; } = [];
}

public class Consultant : User
{
    [MaxLength(100)]
    public string? Title { get; set; }

    [MaxLength(1000)]
    public string? Bio { get; set; }

    [MaxLength(200)]
    public string? Qualification { get; set; }

    [Range(0, 60)]
    public int YearsExperience { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    [MaxLength(200)]
    public string? OfficeAddress { get; set; }

    [Range(-90, 90)]
    public double? Latitude { get; set; }

    [Range(-180, 180)]
    public double? Longitude { get; set; }

    public bool IsApproved { get; set; }

    public List<Service> Services { get; set; } = [];
    public List<Availability> Availabilities { get; set; } = [];
    public List<TimeOff> TimeOffs { get; set; } = [];
    public List<CertificatePhoto> Certificates { get; set; } = [];

    [InverseProperty("Consultant")]
    public List<Appointment> Appointments { get; set; } = [];
}
