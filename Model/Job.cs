using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crawler;

public class Job
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public string? JobId { get; set; }
    public string? Title { get; set; }
    public string? Employer { get; set; }
    public string? Source { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? Industry { get; set; }
    public string? ExpiryDate { get; set; }
    public DateTime  ScrappedDate { get; set; }
}