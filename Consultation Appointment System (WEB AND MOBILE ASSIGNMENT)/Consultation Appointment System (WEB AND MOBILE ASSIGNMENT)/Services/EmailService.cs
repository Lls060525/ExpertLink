using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace ExpertLink.Services;

// Sends HTML e-mails through SMTP (configure "Smtp" in appsettings.json).
// If SMTP is not configured or sending fails, the e-mail is saved as an .html
// file in the MailDrop folder so that it can still be demonstrated.
public class EmailService(IConfiguration cf, IWebHostEnvironment en, ILogger<EmailService> log)
{
    public bool Send(string to, string subject, string html)
    {
        var s = cf.GetSection("Smtp");
        var user = s["User"];
        var pass = s["Pass"];

        if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(pass))
        {
            try
            {
                using var mail = new MailMessage
                {
                    From = new MailAddress(user, s["Name"] ?? "ExpertLink"),
                    Subject = subject,
                    Body = html,
                    IsBodyHtml = true,
                };
                mail.To.Add(to);

                using var smtp = new SmtpClient
                {
                    Host = s["Host"] ?? "smtp.gmail.com",
                    Port = int.TryParse(s["Port"], out var port) ? port : 587,
                    EnableSsl = true,
                    Credentials = new NetworkCredential(user, pass),
                };
                smtp.Send(mail);
                return true;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Failed to send e-mail to {To}", to);
            }
        }

        SaveToMailDrop(to, subject, html);
        return false;
    }

    private void SaveToMailDrop(string to, string subject, string html)
    {
        try
        {
            var dir = Path.Combine(en.ContentRootPath, "MailDrop");
            Directory.CreateDirectory(dir);
            var safe = Regex.Replace(subject, @"[^A-Za-z0-9\-]+", "_");
            var file = Path.Combine(dir, $"{DateTime.Now:yyyyMMdd_HHmmss}_{safe}.html");
            File.WriteAllText(file, $"<!-- To: {WebUtility.HtmlEncode(to)} -->\n{html}");
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Failed to save e-mail to MailDrop");
        }
    }

    // Consistent branded e-mail layout
    public static string Template(string heading, string bodyHtml, string? buttonText = null, string? buttonUrl = null)
    {
        var button = buttonText != null && buttonUrl != null
            ? $"<p style='text-align:center;margin:28px 0'><a href='{buttonUrl}' style='background:#0f766e;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:bold'>{buttonText}</a></p>" +
              $"<p style='font-size:12px;color:#666'>If the button does not work, copy this link into your browser:<br>{buttonUrl}</p>"
            : "";
        return $@"<div style='font-family:Segoe UI,Arial,sans-serif;background:#f1f5f9;padding:24px'>
  <div style='max-width:560px;margin:auto;background:#fff;border-radius:12px;overflow:hidden'>
    <div style='background:#0f766e;color:#fff;padding:18px 24px;font-size:20px;font-weight:bold'>ExpertLink</div>
    <div style='padding:24px;color:#1e293b;line-height:1.6'>
      <h2 style='margin-top:0'>{heading}</h2>
      {bodyHtml}
      {button}
    </div>
    <div style='padding:14px 24px;font-size:12px;color:#64748b;background:#f8fafc'>This is an automated message from ExpertLink Consultation Appointment System.</div>
  </div>
</div>";
    }
}
