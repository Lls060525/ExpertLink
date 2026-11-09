using System.Security.Claims;
using ExpertLink.Models;

namespace ExpertLink.Services;

public static class ClaimsExtensions
{
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public static string GetPhoto(this ClaimsPrincipal user) =>
        Helper.Avatar(user.FindFirstValue("Photo"));
}

public static class DisplayExtensions
{
    public static string Text(this ConsultationMode mode) => mode == ConsultationMode.InPerson ? "In-Person" : "Online";

    public static string Icon(this ConsultationMode mode) => mode == ConsultationMode.InPerson ? "bi-geo-alt" : "bi-camera-video";

    public static string Badge(this AppointmentStatus s) => s switch
    {
        AppointmentStatus.Pending => "text-bg-warning",
        AppointmentStatus.Confirmed => "text-bg-primary",
        AppointmentStatus.CheckedIn => "text-bg-info",
        AppointmentStatus.Completed => "text-bg-success",
        AppointmentStatus.Cancelled => "text-bg-secondary",
        AppointmentStatus.Rejected => "text-bg-danger",
        AppointmentStatus.NoShow => "text-bg-dark",
        _ => "text-bg-light"
    };

    public static string Text(this AppointmentStatus s) => s switch
    {
        AppointmentStatus.CheckedIn => "Checked In",
        AppointmentStatus.NoShow => "No Show",
        _ => s.ToString()
    };

    public static string Time12(this TimeOnly t) => t.ToString("h:mm tt");
}
