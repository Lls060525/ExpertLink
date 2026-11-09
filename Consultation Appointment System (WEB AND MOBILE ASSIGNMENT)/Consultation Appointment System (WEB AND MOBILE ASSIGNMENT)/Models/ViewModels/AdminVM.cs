using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace ExpertLink.Models;

public class CreateUserVM
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
    [DataType(DataType.Password), Display(Name = "Temporary Password")]
    public string Password { get; set; } = "";

    [Required, RegularExpression("^(Admin|Client)$", ErrorMessage = "Invalid role.")]
    public string Role { get; set; } = "Admin";
}

public class DashboardVM
{
    public int Clients { get; set; }
    public int Consultants { get; set; }
    public int PendingApprovals { get; set; }
    public int AppointmentsThisMonth { get; set; }
    public int UpcomingAppointments { get; set; }
    public decimal RevenueThisMonth { get; set; }
    public decimal TotalRevenue { get; set; }
    public double AverageRating { get; set; }
    public List<Appointment> Recent { get; set; } = [];
    public List<Consultant> AwaitingApproval { get; set; } = [];
}
