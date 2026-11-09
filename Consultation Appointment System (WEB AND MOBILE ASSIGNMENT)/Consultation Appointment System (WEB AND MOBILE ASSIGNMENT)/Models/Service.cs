using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExpertLink.Models;

// A consultation package offered by a consultant (e.g. "60-min Tax Planning Session")
public class Service
{
    [Key]
    public int Id { get; set; }

    public int ConsultantId { get; set; }
    public Consultant Consultant { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Name { get; set; } = "";

    [MaxLength(500)]
    public string? Description { get; set; }

    [Range(15, 240)]
    public int DurationMinutes { get; set; } = 60;

    [Column(TypeName = "decimal(8,2)")]
    public decimal Fee { get; set; }

    public bool AllowOnline { get; set; } = true;
    public bool AllowInPerson { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public List<Appointment> Appointments { get; set; } = [];
}
