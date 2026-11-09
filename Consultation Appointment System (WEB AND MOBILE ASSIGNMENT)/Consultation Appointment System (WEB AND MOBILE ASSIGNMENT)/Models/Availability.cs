using System.ComponentModel.DataAnnotations;

namespace ExpertLink.Models;

// Weekly recurring working hours of a consultant (e.g. Monday 09:00 - 12:00)
public class Availability
{
    [Key]
    public int Id { get; set; }

    public int ConsultantId { get; set; }
    public Consultant Consultant { get; set; } = null!;

    public DayOfWeek Day { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }
}

// A specific date on which the consultant is not available (leave / holiday)
public class TimeOff
{
    [Key]
    public int Id { get; set; }

    public int ConsultantId { get; set; }
    public Consultant Consultant { get; set; } = null!;

    public DateOnly Date { get; set; }

    [MaxLength(200)]
    public string? Reason { get; set; }
}
