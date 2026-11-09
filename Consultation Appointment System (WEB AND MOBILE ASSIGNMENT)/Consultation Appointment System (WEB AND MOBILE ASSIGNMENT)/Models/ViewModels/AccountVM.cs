using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace ExpertLink.Models;

public class LoginVM
{
    [Required, EmailAddress, MaxLength(100)]
    public string Email { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Display(Name = "Remember Me")]
    public bool RememberMe { get; set; }

    // Only required after a failed login attempt (checked in the controller)
    public string? Captcha { get; set; }

    public string? ReturnUrl { get; set; }
}

public class RegisterVM
{
    [Required, MaxLength(100)]
    [RegularExpression(@"^[A-Za-z][A-Za-z @'.\-\/]*$", ErrorMessage = "Name may only contain letters, spaces and . ' - / @")]
    public string Name { get; set; } = "";

    [Required, EmailAddress, MaxLength(100)]
    [Remote("CheckEmail", "Account", ErrorMessage = "This email has already been registered.")]
    public string Email { get; set; } = "";

    [Required, MaxLength(15)]
    [RegularExpression(@"^01\d-?\d{7,8}$", ErrorMessage = "Phone must be in the format 01X-XXXXXXX.")]
    public string Phone { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "Password must contain an uppercase letter, a lowercase letter and a digit.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required, DataType(DataType.Password), Display(Name = "Confirm Password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string Confirm { get; set; } = "";

    [Display(Name = "Profile Photo")]
    [MaxFileSize(2)]
    public IFormFile? Photo { get; set; }

    [Required(ErrorMessage = "Please enter the captcha code.")]
    public string Captcha { get; set; } = "";

    [MustBeTrue(ErrorMessage = "You must agree to the terms and privacy policy.")]
    [Display(Name = "I agree to the terms and privacy policy")]
    public bool AcceptTerms { get; set; }
}

public class ConsultantRegisterVM : RegisterVM
{
    [Required(ErrorMessage = "Please select your specialty."), Display(Name = "Specialty")]
    public int? CategoryId { get; set; }

    [Required, MaxLength(100), Display(Name = "Professional Title")]
    public string Title { get; set; } = "";

    [Required, MaxLength(200)]
    public string Qualification { get; set; } = "";

    [Range(0, 60), Display(Name = "Years of Experience")]
    public int YearsExperience { get; set; }
}

public class ProfileVM
{
    public string? Email { get; set; }
    public string? Role { get; set; }

    [Required, MaxLength(100)]
    [RegularExpression(@"^[A-Za-z][A-Za-z @'.\-\/]*$", ErrorMessage = "Name may only contain letters, spaces and . ' - / @")]
    public string Name { get; set; } = "";

    [Required, MaxLength(15)]
    [RegularExpression(@"^01\d-?\d{7,8}$", ErrorMessage = "Phone must be in the format 01X-XXXXXXX.")]
    public string Phone { get; set; } = "";

    [Display(Name = "New Photo"), MaxFileSize(2)]
    public IFormFile? Photo { get; set; }

    public string? PhotoURL { get; set; }

    [Display(Name = "Remove current photo")]
    public bool RemovePhoto { get; set; }
}

public class ChangePasswordVM
{
    [Required, DataType(DataType.Password), Display(Name = "Current Password")]
    public string Current { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "Password must contain an uppercase letter, a lowercase letter and a digit.")]
    [DataType(DataType.Password), Display(Name = "New Password")]
    public string New { get; set; } = "";

    [Required, DataType(DataType.Password), Display(Name = "Confirm New Password")]
    [Compare(nameof(New), ErrorMessage = "Passwords do not match.")]
    public string Confirm { get; set; } = "";
}

public class ForgotPasswordVM
{
    [Required, EmailAddress, MaxLength(100)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Please enter the captcha code.")]
    public string Captcha { get; set; } = "";
}

public class ResetPasswordVM
{
    [Required]
    public string Token { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "Password must contain an uppercase letter, a lowercase letter and a digit.")]
    [DataType(DataType.Password), Display(Name = "New Password")]
    public string New { get; set; } = "";

    [Required, DataType(DataType.Password), Display(Name = "Confirm New Password")]
    [Compare(nameof(New), ErrorMessage = "Passwords do not match.")]
    public string Confirm { get; set; } = "";
}
