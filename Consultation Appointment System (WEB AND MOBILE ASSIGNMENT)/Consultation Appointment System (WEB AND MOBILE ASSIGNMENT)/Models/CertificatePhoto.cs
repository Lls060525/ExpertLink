using System.ComponentModel.DataAnnotations;

namespace ExpertLink.Models;

// Multiple certificate / credential photos per consultant (one-to-many)
public class CertificatePhoto
{
    [Key]
    public int Id { get; set; }

    public int ConsultantId { get; set; }
    public Consultant Consultant { get; set; } = null!;

    [Required, MaxLength(100)]
    public string PhotoURL { get; set; } = "";

    [MaxLength(100)]
    public string? Caption { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.Now;
}
