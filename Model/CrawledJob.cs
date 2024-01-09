using System.ComponentModel.DataAnnotations;

namespace Crawler.Model;

public class CrawledJob
{
    [Key] public Guid UUId { get; set; }
    public string? JobId { get; set; }
    public string? JobTitle { get; set; }
    public string? JobSourceUrl { get; set; }
    public string? JobDescription { get; set; }
    public string? JobRequirement { get; set; }
    public string? EmploymentType { get; set; }
    public string? AccademicQualification { get; set; }
    public string? YearsOfExperience { get; set; }
    public string? Location { get; set; }
    public string? EmployerName { get; set; }
    public DateTime? CreatedAt { get; set; }

    public string? Salary { get; set; }
    public string? Currency { get; set; }
}