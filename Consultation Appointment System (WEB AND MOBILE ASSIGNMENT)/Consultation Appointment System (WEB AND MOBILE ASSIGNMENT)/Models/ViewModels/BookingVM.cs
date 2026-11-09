using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ExpertLink.Models;

public class BookVM
{
    public int ServiceId { get; set; }

    [Required(ErrorMessage = "Please choose a date."), FutureDate(60), Display(Name = "Date")]
    public DateOnly? Date { get; set; }

    [Required(ErrorMessage = "Please select a time slot."), Display(Name = "Time Slot")]
    public TimeOnly? StartTime { get; set; }

    [Required(ErrorMessage = "Please choose a consultation mode."), Display(Name = "Mode")]
    public ConsultationMode? Mode { get; set; }

    [MaxLength(500), Display(Name = "What would you like to discuss?")]
    public string? Concern { get; set; }

    [BindNever]
    public Service? Service { get; set; }
}

public class RescheduleVM
{
    public int AppointmentId { get; set; }

    [Required(ErrorMessage = "Please choose a date."), FutureDate(60), Display(Name = "New Date")]
    public DateOnly? Date { get; set; }

    [Required(ErrorMessage = "Please select a time slot."), Display(Name = "New Time Slot")]
    public TimeOnly? StartTime { get; set; }

    [BindNever]
    public Appointment? Appointment { get; set; }
}

public class SlotVM
{
    public string Start { get; set; } = "";   // "HH:mm"
    public string End { get; set; } = "";
    public string Label { get; set; } = "";   // "9:00 AM"
    public bool Available { get; set; }
}

public class PaymentVM : IValidatableObject
{
    public int AppointmentId { get; set; }

    [Required, RegularExpression("^(Card|FPX|E-Wallet)$", ErrorMessage = "Invalid payment method.")]
    public string Method { get; set; } = "Card";

    [MaxLength(100), Display(Name = "Name on Card")]
    [RegularExpression(@"^[A-Za-z][A-Za-z .'\-]*$", ErrorMessage = "Invalid cardholder name.")]
    public string? CardName { get; set; }

    [Luhn, Display(Name = "Card Number")]
    public string? CardNumber { get; set; }

    [CardExpiry, Display(Name = "Expiry (MM/YY)")]
    public string? Expiry { get; set; }

    [RegularExpression(@"^\d{3,4}$", ErrorMessage = "CVV must be 3 or 4 digits."), Display(Name = "CVV")]
    public string? Cvv { get; set; }

    public string? Bank { get; set; }

    [RegularExpression(@"^01\d{8,9}$", ErrorMessage = "Enter a valid phone number, e.g. 0123456789."), Display(Name = "E-Wallet Phone No.")]
    public string? WalletPhone { get; set; }

    [BindNever]
    public Appointment? Appointment { get; set; }

    public static readonly string[] Banks = ["Maybank2u", "CIMB Clicks", "Public Bank", "RHB Now", "Hong Leong Connect", "Bank Islam"];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Method == "Card")
        {
            if (string.IsNullOrWhiteSpace(CardName)) yield return new ValidationResult("Name on card is required.", [nameof(CardName)]);
            if (string.IsNullOrWhiteSpace(CardNumber)) yield return new ValidationResult("Card number is required.", [nameof(CardNumber)]);
            if (string.IsNullOrWhiteSpace(Expiry)) yield return new ValidationResult("Expiry is required.", [nameof(Expiry)]);
            if (string.IsNullOrWhiteSpace(Cvv)) yield return new ValidationResult("CVV is required.", [nameof(Cvv)]);
        }
        else if (Method == "FPX")
        {
            if (string.IsNullOrWhiteSpace(Bank) || !Banks.Contains(Bank)) yield return new ValidationResult("Please select your bank.", [nameof(Bank)]);
        }
        else if (Method == "E-Wallet")
        {
            if (string.IsNullOrWhiteSpace(WalletPhone)) yield return new ValidationResult("E-Wallet phone number is required.", [nameof(WalletPhone)]);
        }
    }
}
