using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ExpertLink.Models;

// Real-time chat message between client and consultant for an appointment
public class ChatMessage
{
    [Key]
    public int Id { get; set; }

    public int AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;

    public int SenderId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User Sender { get; set; } = null!;

    [Required, MaxLength(1000)]
    public string Content { get; set; } = "";

    public DateTime SentAt { get; set; } = DateTime.Now;
}
