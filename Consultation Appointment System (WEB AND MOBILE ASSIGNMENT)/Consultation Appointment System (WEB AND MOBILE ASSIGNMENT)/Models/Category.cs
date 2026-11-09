using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ExpertLink.Models;

[Index(nameof(Name), IsUnique = true)]
public class Category
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Name { get; set; } = "";

    [MaxLength(300)]
    public string? Description { get; set; }

    // Bootstrap icon class, e.g. "bi-bank"
    [Required, MaxLength(50)]
    public string Icon { get; set; } = "bi-briefcase";

    public bool IsActive { get; set; } = true;

    public List<Consultant> Consultants { get; set; } = [];
}
