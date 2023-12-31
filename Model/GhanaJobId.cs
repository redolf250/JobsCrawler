using System.ComponentModel.DataAnnotations;

namespace Crawler.Model;

public class GhanaJobId
{
    [Key] public int Id { get; set; }

    public string JobId { get; set; } = string.Empty;
}