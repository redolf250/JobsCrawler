using System.Globalization;
using Crawler.Data;
using CsvHelper;
using Hangfire;
using HtmlAgilityPack;

namespace Crawler.Service;

public class GhanaJobsService : IJobsService
{
    private const string GhanaJobBaseUrl = "https://www.ghanajob.com/job-vacancies-search-ghana?page=";

    private readonly ApplicationDbContext _dbContext;

    private readonly ILogger<GhanaJobsService> _logger;

    public GhanaJobsService(ApplicationDbContext dbContext, ILogger<GhanaJobsService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 5)]
    public async Task BackgroundWorker(string filePath)
    {
        _logger.LogInformation("Background service executing ============");
        var scrappedJobsList = await ScrapJobs();
        var readCsv = ReadJobsFromCsv(filePath);
        var filter = FilterOnlyNewJobs(readCsv,scrappedJobsList);
        WriteJobsToCsv(filter,filePath);
        await SaveJobs(filter);
        _logger.LogInformation("Background service done executing ============");
    }

    private async Task SaveJobs(List<Job> jobs)
    {
        try
        {
            if (jobs.Count > 0)
            {
                _logger.LogInformation("Saving jobs to database ============");
                await _dbContext.AddRangeAsync(jobs);
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Saved jobs to database successfully ============");
            }
        }
        catch (Exception e)
        {
            _logger.LogError($"{e.Message}");
            throw;
        }
        
    }

    private async Task<List<Job>> ScrapJobs()
    {
        string targetClass = "job-description-wrapper";
        List<Job> jobList = new List<Job>();
        HtmlDocument htmlDocument = new HtmlDocument();
        using HttpClient client = new HttpClient();
        int currentPage = 1
            ;
        while (true)
        {
            _logger.LogInformation($"Starting scrapping from page {currentPage} ============");
            HttpResponseMessage response = await client.GetAsync($"{GhanaJobBaseUrl}{currentPage}");
            if (response.IsSuccessStatusCode)
            {
                string htmlContent = await response.Content.ReadAsStringAsync();
                htmlDocument.LoadHtml(htmlContent);
                string xpathExpression = $"//*[contains(@class, '{targetClass}')]";
                var nodes = htmlDocument.DocumentNode.SelectNodes(xpathExpression);
                if (nodes != null)
                {
                    foreach (var node in nodes)
                    {
                        var jobTitle = node.SelectSingleNode(".//h5/a")?.InnerText.Trim();
                        var recruiter = node.SelectSingleNode(".//p[@class='job-recruiter']")?.InnerText.Trim();
                        var jobDescription = node.SelectSingleNode(".//div[@class='search-description']")?.InnerText.Trim();
                        var jobRegion = node.SelectSingleNode(".//p[contains(text(),'Region of')]")?.InnerText.Trim();
                        var dataHref = node.GetAttributeValue("data-href", "");
                        var jobId =dataHref.Split("-").Last();
                        jobList.Add(new Job
                        {
                            JobId =jobId ,Title = jobTitle, Employer = recruiter,Location = jobRegion, Description = jobDescription,
                            Source = dataHref, ScrappedDate = DateTime.Today
                        });
                        
                    }
                }
            }
            else
            {
                _logger.LogError($"{response.StatusCode} - {response.ReasonPhrase}");
            }
            var nextPageNode = htmlDocument.DocumentNode.SelectSingleNode("//li[@class='pager-item active']");
            if (nextPageNode == null)
            {
                break;
            }
            currentPage++;
            _logger.LogInformation($"Finished scrapping page {currentPage} ============");
        }
        return jobList;
    }
    private void WriteJobsToCsv(List<Job> jobsList, string filePath)
    {
        if (jobsList.Count > 0)
        {
            _logger.LogInformation("Writing jobs to csv file ============");
            bool fileExists = File.Exists(filePath);
            using var writer = new StreamWriter(filePath, fileExists );
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            if (!fileExists)
            {
                csv.WriteRecords(new List<Job> { new Job() }); // Write a dummy record to create the header
            }
            csv.WriteRecords(jobsList);
            _logger.LogInformation("Writing jobs to csv file done ============");
            csv.Dispose();
            writer.Dispose();
            _logger.LogInformation("Closing stream readers! ============");
        }
    }
    private List<Job> ReadJobsFromCsv(string filePath)
    {
        _logger.LogInformation("Reading jobs from  csv file ============");
        var reader = new StreamReader(filePath, true);
        var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);
        var records = csvReader.GetRecords<Job>().Where(job => job.JobId != "JobId" ).ToList();
        _logger.LogInformation("Reading jobs done finish ============");
        reader.Dispose();
        csvReader.Dispose();
        _logger.LogInformation("Disposing off streams after reading ============");
        return records;
    }
    private List<Job> FilterOnlyNewJobs(List<Job> alreadyExistingJobs, List<Job> scrappedJobs)
    {
        List<string> oldJobsIds = new List<string>();
        List<string> newJobsIds = new List<string>();

        alreadyExistingJobs.ForEach(job => oldJobsIds.Add(job.JobId) );
        scrappedJobs.ForEach(job => newJobsIds.Add(job.JobId));
        _logger.LogInformation("Started performing LINQ operation ============");
        var jobIds = newJobsIds.Except(oldJobsIds).ToList();
        _logger.LogInformation("Finished performing LINQ operation ============");

        _logger.LogInformation("Started filtering for only newly added jobs ============");
        var filteredJobs = scrappedJobs.Where(job => jobIds.Contains(job.JobId)).ToList();
        _logger.LogInformation("Finished filtering jobs ============");
        return filteredJobs;
    }

}