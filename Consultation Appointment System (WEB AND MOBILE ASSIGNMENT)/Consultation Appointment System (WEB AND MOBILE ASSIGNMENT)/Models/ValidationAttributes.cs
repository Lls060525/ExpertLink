using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ExpertLink.Models;

// ===========================================================================
// Custom validation attributes. Each one validates on the server and also
// emits data-val-* attributes so that wwwroot/js/validation.js can validate
// the same rule on the client (jQuery unobtrusive validation).
// ===========================================================================

internal static class ClientValidation
{
    public static void Merge(ClientModelValidationContext context, string key, string value)
    {
        if (!context.Attributes.ContainsKey(key)) context.Attributes.Add(key, value);
    }
}

// Checkbox that must be ticked (e.g. "I agree to the terms")
public class MustBeTrueAttribute : ValidationAttribute, IClientModelValidator
{
    public MustBeTrueAttribute() : base("You must tick this box.") { }

    public override bool IsValid(object? value) => value is true;

    public void AddValidation(ClientModelValidationContext context)
    {
        ClientValidation.Merge(context, "data-val", "true");
        ClientValidation.Merge(context, "data-val-mustbetrue", FormatErrorMessage(context.ModelMetadata.GetDisplayName()));
    }
}

// Date (DateOnly) between today and today + MaxDays
public class FutureDateAttribute(int maxDays = 365) : ValidationAttribute, IClientModelValidator
{
    public int MaxDays { get; } = maxDays;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null) return ValidationResult.Success;   // [Required] handles empty
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (value is DateOnly d && d >= today && d <= today.AddDays(MaxDays)) return ValidationResult.Success;
        return new ValidationResult(ErrorMessage ?? $"{validationContext.DisplayName} must be between today and the next {MaxDays} days.");
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        var name = context.ModelMetadata.GetDisplayName();
        ClientValidation.Merge(context, "data-val", "true");
        ClientValidation.Merge(context, "data-val-futuredate", ErrorMessage ?? $"{name} must be between today and the next {MaxDays} days.");
        ClientValidation.Merge(context, "data-val-futuredate-max", MaxDays.ToString());
    }
}

// Credit / debit card number checked with the Luhn (mod 10) algorithm
public class LuhnAttribute : ValidationAttribute, IClientModelValidator
{
    public LuhnAttribute() : base("Invalid card number.") { }

    public override bool IsValid(object? value)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s)) return true;
        var digits = s.Replace(" ", "").Replace("-", "");
        if (!Regex.IsMatch(digits, @"^\d{13,19}$")) return false;
        return Check(digits);
    }

    public static bool Check(string digits)
    {
        int sum = 0;
        bool dbl = false;
        for (int i = digits.Length - 1; i >= 0; i--)
        {
            int n = digits[i] - '0';
            if (dbl) { n *= 2; if (n > 9) n -= 9; }
            sum += n;
            dbl = !dbl;
        }
        return sum % 10 == 0;
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        ClientValidation.Merge(context, "data-val", "true");
        ClientValidation.Merge(context, "data-val-luhn", FormatErrorMessage(context.ModelMetadata.GetDisplayName()));
    }
}

// Card expiry in MM/YY format and not in the past
public class CardExpiryAttribute : ValidationAttribute, IClientModelValidator
{
    public CardExpiryAttribute() : base("Expiry must be MM/YY and not expired.") { }

    public override bool IsValid(object? value)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s)) return true;
        var m = Regex.Match(s.Trim(), @"^(0[1-9]|1[0-2])\/(\d{2})$");
        if (!m.Success) return false;
        int month = int.Parse(m.Groups[1].Value);
        int year = 2000 + int.Parse(m.Groups[2].Value);
        var lastDay = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
        return lastDay >= DateTime.Today && year <= DateTime.Today.Year + 20;
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        ClientValidation.Merge(context, "data-val", "true");
        ClientValidation.Merge(context, "data-val-cardexpiry", FormatErrorMessage(context.ModelMetadata.GetDisplayName()));
    }
}

// Uploaded file size limit (single IFormFile or a list of IFormFile)
public class MaxFileSizeAttribute(int maxMB) : ValidationAttribute, IClientModelValidator
{
    public int MaxMB { get; } = maxMB;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var files = value switch
        {
            IFormFile f => new List<IFormFile> { f },
            IEnumerable<IFormFile> list => list.ToList(),
            _ => new List<IFormFile>()
        };
        if (files.Any(f => f.Length > MaxMB * 1024L * 1024L))
            return new ValidationResult(ErrorMessage ?? $"Each file must not exceed {MaxMB} MB.");
        return ValidationResult.Success;
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        ClientValidation.Merge(context, "data-val", "true");
        ClientValidation.Merge(context, "data-val-filesize", ErrorMessage ?? $"Each file must not exceed {MaxMB} MB.");
        ClientValidation.Merge(context, "data-val-filesize-max", (MaxMB * 1024 * 1024).ToString());
    }
}
