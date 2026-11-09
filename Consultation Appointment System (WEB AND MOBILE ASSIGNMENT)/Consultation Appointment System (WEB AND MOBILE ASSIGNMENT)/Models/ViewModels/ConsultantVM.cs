using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ExpertLink.Models;

public class ConsultantProfileVM
{
    public string? Email { get; set; }
    public string? PhotoURL { get; set; }

    [Required, MaxLength(100)]
    [RegularExpression(@"^[A-Za-z][A-Za-z @'.\-\/]*$", ErrorMessage = "Name may only contain letters, spaces and . ' - / @")]
    public string Name { get; set; } = "";

    [Required, MaxLength(15)]
    [RegularExpression(@"^01\d-?\d{7,8}$", ErrorMessage = "Phone must be in the format 01X-XXXXXXX.")]
    public string Phone { get; set; } = "";

    [Required, MaxLength(100), Display(Name = "Professional Title")]
    public string Title { get; set; } = "";

    [Required, MaxLength(1000), MinLength(30, ErrorMessage = "Bio should be at least 30 characters.")]
    public string Bio { get; set; } = "";

    [Required, MaxLength(200)]
    public string Qualification { get; set; } = "";

    [Range(0, 60), Display(Name = "Years of Experience")]
    public int YearsExperience { get; set; }

    [Required(ErrorMessage = "Please select your specialty."), Display(Name = "Specialty")]
    public int? CategoryId { get; set; }

    [MaxLength(200), Display(Name = "Office Address")]
    public string? OfficeAddress { get; set; }

    [Range(-90, 90)]
    public double? Latitude { get; set; }

    [Range(-180, 180)]
    public double? Longitude { get; set; }

    [Display(Name = "New Photo"), MaxFileSize(2)]
    public IFormFile? Photo { get; set; }
}

public class ServiceVM : IValidatableObject
{
    public int Id { get; set; }

    [Required, MaxLength(100), Display(Name = "Service Name")]
    public string Name { get; set; } = "";

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required, Range(15, 240), Display(Name = "Duration (minutes)")]
    public int? DurationMinutes { get; set; } = 60;

    [Required, Range(typeof(decimal), "0", "10000", ErrorMessage = "Fee must be between RM 0 and RM 10,000."), Display(Name = "Fee (RM)")]
    [DataType(DataType.Currency)]
    public decimal? Fee { get; set; }

    [Display(Name = "Online (video call)")]
    public bool AllowOnline { get; set; } = true;

    [Display(Name = "In-Person (office)")]
    public bool AllowInPerson { get; set; } = true;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!AllowOnline && !AllowInPerson)
            yield return new ValidationResult("Select at least one consultation mode.", [nameof(AllowOnline)]);
        if (DurationMinutes % 15 != 0)
            yield return new ValidationResult("Duration must be in multiples of 15 minutes.", [nameof(DurationMinutes)]);
    }
}

public class AvailabilityRowVM : IValidatableObject
{
    public DayOfWeek Day { get; set; }

    [Required, Display(Name = "Start")]
    public TimeOnly? StartTime { get; set; }

    [Required, Display(Name = "End")]
    public TimeOnly? EndTime { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartTime != null && EndTime != null && EndTime <= StartTime)
            yield return new ValidationResult("End time must be later than start time.", [nameof(EndTime)]);
    }
}

public class AvailabilityVM
{
    public List<AvailabilityRowVM> Rows { get; set; } = [];
}

public class TimeOffVM
{
    [Required, FutureDate(365), Display(Name = "Date")]
    public DateOnly? Date { get; set; }

    [MaxLength(200)]
    public string? Reason { get; set; }
}

// Card data for the public consultant directory (projection)
public class ConsultantCardVM
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Title { get; set; }
    public string? PhotoURL { get; set; }
    public string? Category { get; set; }
    public string? CategoryIcon { get; set; }
    public int YearsExperience { get; set; }
    public double Rating { get; set; }
    public int ReviewCount { get; set; }
    public decimal? MinFee { get; set; }
    public bool Online { get; set; }
    public bool InPerson { get; set; }
}

public class ConsultantSearchVM
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public string? Mode { get; set; }
    public decimal? MaxFee { get; set; }
    public string Sort { get; set; } = "rating";
    public int Page { get; set; } = 1;
    [BindNever]
    public PagedList<ConsultantCardVM>? Result { get; set; }
}
