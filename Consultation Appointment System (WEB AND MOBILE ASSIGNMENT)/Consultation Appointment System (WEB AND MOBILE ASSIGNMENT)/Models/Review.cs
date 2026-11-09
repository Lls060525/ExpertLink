using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExpertLink.Models;

// One-to-one with a completed Appointment (shared primary key)
public class Review
{
    [Key, ForeignKey(nameof(Appointment))]
    public int AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;

    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(500)]
    public string? Comment { get; set; }

    [MaxLength(500)]
    public string? Reply { get; set; }   // consultant's reply

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
