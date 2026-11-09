using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExpertLink.Models;

public enum PaymentStatus
{
    Paid,
    Refunded
}

// One-to-one with Appointment (shared primary key)
public class Payment
{
    [Key, ForeignKey(nameof(Appointment))]
    public int AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;

    [Column(TypeName = "decimal(8,2)")]
    public decimal Amount { get; set; }

    [Required, MaxLength(20)]
    public string Method { get; set; } = "Card";   // Card / FPX / E-Wallet

    [Required, MaxLength(30)]
    public string ReferenceNo { get; set; } = "";

    [MaxLength(4)]
    public string? CardLast4 { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Paid;

    public DateTime PaidAt { get; set; } = DateTime.Now;

    public DateTime? RefundedAt { get; set; }
}
