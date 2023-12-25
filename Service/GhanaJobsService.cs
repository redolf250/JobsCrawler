using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Authentication;
using Crawler.Data;
using CsvHelper;
using Hangfire;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;

namespace Crawler.Service;

public class GhanaJobsService : IJobsService
{
    private const string GhanaJobBaseUrl = "https://www.ghanajob.com/job-vacancies-search-ghana?page=";

    private readonly ApplicationDbContext _dbContext;

    private readonly ILogger<GhanaJobsService> _logger;

    private static readonly HttpClient Client = new();

    static List<string> _newJobsIds = new();

    static readonly List<string> UrlList = new();

    public GhanaJobsService(ApplicationDbContext dbContext, ILogger<GhanaJobsService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task BackgroundWorker()
    {
        _logger.LogInformation("Background service executing ============");
        Console.WriteLine("Background service executing ============");
        var scrapJobsUrls = await ScrapJobUrls();
        var scrapJobsIds = ExtractJobIdsFromUrls(scrapJobsUrls);
        var oldJobsIds  = await AlreadyExistingJobsIds();
        var filter = FilterOnlyNewJobsUrls(oldJobsIds,scrapJobsIds);
        if (filter.Count > 0)
        {
            Console.WriteLine($"About to processing {filter.Count} new jobs============"); 
            var newJobDetails = await DestructureScrapJobUrls(filter);
            var tracker = await ConvertNewJobIdsToGhanaJobId(_newJobsIds);
            await SaveCrawledJobsData(newJobDetails, tracker);
            _logger.LogInformation("Background service done executing ============");
            Console.WriteLine("Background service done executing ============"); 
        }
        Console.WriteLine("Background service done executing, no new jobs found ============");
        
    }

    private async Task SaveCrawledJobsData(List<CrawledJob> jobs, List<GhanaJobId> newJobsIds)
    {
        try
        {
            if (jobs.Count > 0)
            {
                _logger.LogInformation("Saving jobs to database ============");
                await _dbContext.CrawledJobs.AddRangeAsync(jobs);
                await _dbContext.GhanaJobIds.AddRangeAsync(newJobsIds);
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

    private static async Task<List<CrawledJob>> DestructureScrapJobUrls(List<string> urlList)
    {
        List<CrawledJob> jobList = new List<CrawledJob>();
        HtmlDocument htmlDocument = new HtmlDocument();

        foreach (var url in urlList)
        {
            const SslProtocols _Tls12 = (SslProtocols)0x00000C00;
            const SecurityProtocolType Tls12= (SecurityProtocolType)_Tls12;
            ServicePointManager.SecurityProtocol = Tls12;
            
            Console.WriteLine("Work has started=========1");
            var response = await Client.GetAsync(url);
            
            response.EnsureSuccessStatusCode();
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Work has started======2");
                string htmlContent = await response.Content.ReadAsStringAsync();
                htmlDocument.LoadHtml(htmlContent);
                var jobDetails = GetJobDetails(htmlDocument, url);
                jobList.Add(jobDetails);
            }
        }
        Client.Dispose();
        return jobList;
    }
   
    static CrawledJob GetJobDetails(HtmlDocument doc, string url)
    {   
        Console.WriteLine("Extracting job details has started......");
        var jobDetails = new CrawledJob ();
        jobDetails.JobId = url.Split("-").Last();
        jobDetails.JobSourceUrl = url;
        jobDetails.JobTitle = doc.DocumentNode.SelectSingleNode(".//span[@class='ad-ss-title']")?.InnerText.Trim().Split(":")?.Last();
        jobDetails.EmploymentType = doc.DocumentNode.SelectSingleNode(".//div[@class='field field-name-field-offre-contrat-type field-type-taxonomy-term-reference field-label-hidden']")?.InnerText?.Trim();
        jobDetails.Location = doc.DocumentNode.SelectSingleNode(".//div[@class='field field-name-field-offre-region field-type-taxonomy-term-reference field-label-hidden']")?.InnerText?.Trim();
        jobDetails.YearsOfExperience = doc.DocumentNode.SelectSingleNode(".//div[@class='field field-name-field-offre-niveau-experience field-type-taxonomy-term-reference field-label-hidden']")?.InnerText?.Trim();
        jobDetails.AccademicQualification = doc.DocumentNode.SelectSingleNode(".//div[@class='field field-name-field-offre-niveau-etude field-type-taxonomy-term-reference field-label-hidden']")?.InnerText?.Trim();
        var descriptionNodes = doc.DocumentNode.SelectNodes("//div[@class='inner clearfix']//li")?.ToList();
        jobDetails.JobDescription = Transform(descriptionNodes);
        var requirementsNodes = doc.DocumentNode.SelectNodes("//div/span[@class='ad-ss-title' and contains(text(), 'Required profile')]/following-sibling::ul\n")?.ToList();
        jobDetails.JobRequirement = Transform(requirementsNodes);
        jobDetails.EmployerName=doc.DocumentNode.SelectSingleNode(".//div[@class='company-title']")?.InnerText?.Trim();
        jobDetails.CreatedAt = DateTime.Now;
        Console.WriteLine("Extracting job details finished......");
        return jobDetails;
    }
    static string Transform(List<HtmlNode>? results)
    {
        List<string> records = new List<string>();
        if (results != null)      {
            foreach (var result in results)
            {
                records.Add(result.InnerText.Trim());
            }  
        }
        
        return string.Join(" ", records);
    }

    static List<string> ExtractJobIdsFromUrls(List<string> jobUrls)
    {
        List<string> jobIds = new List<string>();
        Console.WriteLine("Stated extracting job ids from urls ============");
        foreach (var url in jobUrls)
        {
            var jobId = url.Split("-").Last();
            jobIds.Add(jobId);
            Console.WriteLine("Extracting Job Ids From Urls ============");
        }
        Console.WriteLine("Done extracting Job Ids From Urls ============");
        return jobIds;
    }

    private static async Task<List<string>> ScrapJobUrls()
    {
        string targetClass = "job-description-wrapper";//results-table
        HtmlDocument htmlDocument = new HtmlDocument();
        
        int currentPage = 1;
        while (true)
        {
            const SslProtocols tls13 = (SslProtocols)0x00000C00;
            const SecurityProtocolType Tls13 = (SecurityProtocolType)tls13;
            ServicePointManager.SecurityProtocol = Tls13;
            Console.WriteLine("Starting from ====>>> " + currentPage);
            HttpResponseMessage response = await Client.GetAsync($"{GhanaJobBaseUrl}{currentPage}");
            response.EnsureSuccessStatusCode();
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
                        var actualJobUrl = node.GetAttributeValue("data-href", "");
                        Console.WriteLine(actualJobUrl);
                        UrlList.Add(actualJobUrl);
                    }
                }
            }
            else
            {
                Console.WriteLine($"Error from jobs url processing: {response.StatusCode} - {response.ReasonPhrase}");
            }
            var nextPageNode = htmlDocument.DocumentNode.SelectSingleNode("//li[@class='pager-item active']");
            if (nextPageNode == null)
            {
                break;
            }
            currentPage++;
            Console.WriteLine("Finished with ====>>> " + currentPage);
        }
        return UrlList;
    }
    private List<string> FilterOnlyNewJobsUrls(List<string> alreadyExistingJobsIds, List<string> scrappedJobsIds)
    {
        Console.WriteLine("Started filtering only new jobs urls ============");
        _logger.LogInformation("Started performing LINQ operation ============");
        _newJobsIds = scrappedJobsIds.Except(alreadyExistingJobsIds).ToList();
        _logger.LogInformation("Finished performing LINQ operation ============");
        _logger.LogInformation("Started filtering for only newly added jobs urls ============");
         List<string> filteredJobsUrls = UrlList
            .Where(url =>  _newJobsIds.Any(id => url.Contains($"-{id}")))
            .ToList();
        _logger.LogInformation("Finished filtering job urls ============");
        Console.WriteLine("Done filtering only new jobs urls ============");
        return filteredJobsUrls;
    }

    private async Task<List<string>> AlreadyExistingJobsIds()
    {
        var results = await _dbContext.GhanaJobIds.ToListAsync();
        var list = results.Select(id => id.JobId ).ToList();
        Console.WriteLine("Getting jobs old job ids from database ============");
        return list;
    }

    private async Task<List<GhanaJobId>> ConvertNewJobIdsToGhanaJobId(List<string> newJobIds)
    {
        List<GhanaJobId> records = new List<GhanaJobId>();
        foreach (var item in newJobIds)
        {
            records.Add(new GhanaJobId{JobId = item});
            Console.WriteLine("Mapping new jobs new job ids to GhanaJobId object ============");
        } 
        return records;
    }

}