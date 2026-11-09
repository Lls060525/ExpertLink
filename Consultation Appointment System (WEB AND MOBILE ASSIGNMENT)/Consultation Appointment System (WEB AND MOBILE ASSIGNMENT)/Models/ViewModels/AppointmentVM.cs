using System.ComponentModel.DataAnnotations;

namespace ExpertLink.Models;

public class ReviewVM
{
    public int AppointmentId { get; set; }

    [Required(ErrorMessage = "Please give a rating."), Range(1, 5, ErrorMessage = "Please give a rating.")]
    public int? Rating { get; set; }

    [MaxLength(500)]
    public string? Comment { get; set; }
}

public class CompleteVM
{
    public int AppointmentId { get; set; }

    [Required(ErrorMessage = "Please record the session notes."), MaxLength(2000), MinLength(10, ErrorMessage = "Session notes should be at least 10 characters.")]
    [Display(Name = "Session Notes")]
    public string SessionNotes { get; set; } = "";
}

public class ReasonVM
{
    public int AppointmentId { get; set; }

    [Required(ErrorMessage = "Please state a reason."), MaxLength(300)]
    public string Reason { get; set; } = "";
}

public class ConfirmVM
{
    public int AppointmentId { get; set; }

    [MaxLength(200), Url(ErrorMessage = "Please enter a valid URL."), Display(Name = "Meeting Link")]
    public string? MeetingLink { get; set; }
}

public class ReplyVM
{
    public int AppointmentId { get; set; }

    [Required, MaxLength(500)]
    public string Reply { get; set; } = "";
}
