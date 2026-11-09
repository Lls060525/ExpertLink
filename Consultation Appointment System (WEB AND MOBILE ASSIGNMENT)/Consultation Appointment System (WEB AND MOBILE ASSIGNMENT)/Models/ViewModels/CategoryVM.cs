using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace ExpertLink.Models;

public class CategoryVM
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    [Remote("CheckName", "Category", AdditionalFields = nameof(Id), ErrorMessage = "This category name already exists.")]
    public string Name { get; set; } = "";

    [MaxLength(300)]
    public string? Description { get; set; }

    [Required, MaxLength(50)]
    [RegularExpression(@"^bi-[a-z0-9\-]+$", ErrorMessage = "Icon must be a Bootstrap Icons class, e.g. bi-bank.")]
    public string Icon { get; set; } = "bi-briefcase";

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

public class CategoryImportVM
{
    [Required(ErrorMessage = "Please choose a CSV file."), Display(Name = "CSV File"), MaxFileSize(1)]
    public IFormFile? File { get; set; }
}
