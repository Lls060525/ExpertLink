using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ExpertLink.Models;

public enum AppointmentStatus
{
    Pending,     // booked & paid, waiting for consultant confirmation
    Confirmed,   // accepted by consultant
    CheckedIn,   // client has arrived / session started (QR check-in)
    Completed,   // session done
    Cancelled,   // cancelled by client (or auto-cancelled when unpaid)
    Rejected,    // rejected by consultant
    NoShow       // client did not turn up
}

public enum ConsultationMode
{
    Online,
    [Display(Name = "In-Person")]
    InPerson
}

public class Appointment
{
    [Key]
    public int Id { get; set; }

    public int ClientId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Client Client { get; set; } = null!;

    public int ConsultantId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Consultant Consultant { get; set; } = null!;

    public int ServiceId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Service Service { get; set; } = null!;

    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public ConsultationMode Mode { get; set; }

    [MaxLength(500)]
    public string? Concern { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;

    [Column(TypeName = "decimal(8,2)")]
    public decimal Fee { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [MaxLength(300)]
    public string? CancelReason { get; set; }

    [MaxLength(2000)]
    public string? SessionNotes { get; set; }

    [MaxLength(200)]
    public string? MeetingLink { get; set; }

    // Random code encoded in the QR e-ticket for check-in
    [Required, MaxLength(32)]
    public string CheckInCode { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime? CheckedInAt { get; set; }

    public int RescheduleCount { get; set; }

    public Payment? Payment { get; set; }
    public Review? Review { get; set; }
    public List<ChatMessage> Messages { get; set; } = [];

    [NotMapped]
    public DateTime StartDateTime => Date.ToDateTime(StartTime);

    [NotMapped]
    public string Reference => "EXL-" + Id.ToString("D5");
}
