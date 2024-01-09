using System.Net;
using System.Security.Authentication;
using Crawler.Data;
using Crawler.Model;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;

namespace Crawler.Service;

public class GhanaJobsService : IJobsService
{
    private const string GhanaJobBaseUrl = "https://www.ghanajob.com/job-vacancies-search-ghana?page=";

    private static readonly HttpClient Client = new();

    private static List<string> _newJobsIds = new();

    private static readonly List<string> UrlList = new();

    private readonly ApplicationDbContext _dbContext;

    private readonly ILogger<GhanaJobsService> _logger;

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
        var oldJobsIds = await FetchAlreadyExistingJobsIds();
        var filter = FilterOnlyNewJobsUrls(oldJobsIds, scrapJobsIds);
        if (filter.Count > 0)
        {
            Console.WriteLine($"About to processing {filter.Count} new jobs============");
            var newJobDetails = await DestructureScrapJobUrls(filter);
            var tracker = ConvertNewJobIdsToGhanaJobId(_newJobsIds);
            await SaveCrawledJobsData(newJobDetails, tracker);
            _logger.LogInformation("Background service done executing ============");
            Console.WriteLine("Background service done executing ============");
        }
        else
        {
            Console.WriteLine("Background service done executing, no new jobs found ============");
        }
    }

    private async Task SaveCrawledJobsData(IEnumerable<CrawledJob> jobs, IEnumerable<GhanaJobId> newJobsIds)
    {
        try
        {
            _logger.LogInformation("Saving jobs to database ============");
            Console.WriteLine("Saving jobs to database ============");
            await _dbContext.Staging_GhanaJob!.AddRangeAsync(jobs);
            await _dbContext.Staging_GhanaJobIds!.AddRangeAsync(newJobsIds);
            await _dbContext.SaveChangesAsync();
            Console.WriteLine("Saved jobs to database successfully ============");
            _logger.LogInformation("Saved jobs to database successfully ============");
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            Console.WriteLine(e.Message);
            throw;
        }
    }

    private async Task<List<CrawledJob>> DestructureScrapJobUrls(IEnumerable<string> urlList)
    {
        var jobList = new List<CrawledJob>();
        var htmlDocument = new HtmlDocument();
        _logger.LogInformation(
            "Visiting job urls for detailed extraction, this process is quite network intensive and time consuming");
        Console.WriteLine(
            "Visiting job urls for detailed extraction, this process is quite network intensive and time consuming");
        foreach (var url in urlList)
        {
            const SslProtocols tls12 = (SslProtocols)0x00000C00;
            const SecurityProtocolType securityProtocolType = (SecurityProtocolType)tls12;
            ServicePointManager.SecurityProtocol = securityProtocolType;

            Console.WriteLine("Still visiting urls");
            var response = await Client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Work has started======2");
                var htmlContent = await response.Content.ReadAsStringAsync();
                htmlDocument.LoadHtml(htmlContent);
                var jobDetails = GetJobDetails(htmlDocument, url);
                jobList.Add(jobDetails);
            }

            Console.WriteLine("Still visiting urls");
        }

        _logger.LogInformation("Visited all job urls for detailed extraction done!");
        Console.WriteLine("Visited all job urls for detailed extraction done!");
        Client.Dispose();
        return jobList;
    }

    private CrawledJob GetJobDetails(HtmlDocument doc, string url)
    {
        Console.WriteLine("Extracting job details has started......");
        _logger.LogInformation("Extracting job details has started......");
        var jobDetails = new CrawledJob();
        jobDetails.JobId = url.Split("-").Last();
        jobDetails.JobSourceUrl = url;
        jobDetails.JobTitle = doc.DocumentNode.SelectSingleNode(".//span[@class='ad-ss-title']")?.InnerText.Trim()
            .Split(":").Last();
        jobDetails.EmploymentType = doc.DocumentNode
            .SelectSingleNode(
                ".//div[@class='field field-name-field-offre-contrat-type field-type-taxonomy-term-reference field-label-hidden']")
            ?.InnerText?.Trim();
        jobDetails.Location = doc.DocumentNode
            .SelectSingleNode(
                ".//div[@class='field field-name-field-offre-region field-type-taxonomy-term-reference field-label-hidden']")
            ?.InnerText?.Trim();
        jobDetails.YearsOfExperience = doc.DocumentNode
            .SelectSingleNode(
                ".//div[@class='field field-name-field-offre-niveau-experience field-type-taxonomy-term-reference field-label-hidden']")
            ?.InnerText?.Trim();
        jobDetails.AccademicQualification = doc.DocumentNode
            .SelectSingleNode(
                ".//div[@class='field field-name-field-offre-niveau-etude field-type-taxonomy-term-reference field-label-hidden']")
            ?.InnerText?.Trim();
        var descriptionNodes = doc.DocumentNode.SelectNodes("//div[@class='inner clearfix']//li")?.ToList();
        jobDetails.JobDescription = TransformHtmlListElements(descriptionNodes);
        var requirementsNodes = doc.DocumentNode
            .SelectNodes(
                "//div/span[@class='ad-ss-title' and contains(text(), 'Required profile')]/following-sibling::ul\n")
            ?.ToList();
        jobDetails.JobRequirement = TransformHtmlListElements(requirementsNodes);
        jobDetails.EmployerName =
            doc.DocumentNode.SelectSingleNode(".//div[@class='company-title']")?.InnerText?.Trim();
        jobDetails.CreatedAt = DateTime.Now;
        Console.WriteLine("Extracting job details finished......");
        _logger.LogInformation("Extracting job details finished......");
        return jobDetails;
    }

    private string TransformHtmlListElements(IEnumerable<HtmlNode>? results)
    {
        var records = new List<string>();
        if (results != null)
        {
            Console.WriteLine("Started transforming html list elements for a given url");
            _logger.LogInformation("Started transforming html list elements for a given url");
            records.AddRange(results.Select(result => result.InnerText.Trim()));
        }

        Console.WriteLine("Done transforming html list elements for a given url");
        _logger.LogInformation("Done transforming html list elements for a given url");
        return string.Join(" ", records);
    }

    private static List<string> ExtractJobIdsFromUrls(IEnumerable<string> jobUrls)
    {
        var jobIds = new List<string>();
        Console.WriteLine("Stated extracting job ids from urls ============");
        foreach (var jobId in jobUrls.Select(url => url.Split("-").Last()))
        {
            jobIds.Add(jobId);
            Console.WriteLine("Extracting Job Ids From Urls ============");
        }

        Console.WriteLine("Done extracting Job Ids From Urls ============");
        return jobIds;
    }

    private async Task<List<string>> ScrapJobUrls()
    {
        var targetClass = "job-description-wrapper"; //results-table
        var htmlDocument = new HtmlDocument();
        _logger.LogInformation("Started extracting job urls from target domain");
        _logger.LogInformation("This process might take long especially if the website has large records");
        Console.WriteLine("Started extracting job urls from target domain");
        Console.WriteLine("This process might take long especially if the website has large records");
        var currentPage = 1;
        while (true)
        {
            const SslProtocols tls13 = (SslProtocols)0x00000C00;
            const SecurityProtocolType securityProtocolType = (SecurityProtocolType)tls13;
            ServicePointManager.SecurityProtocol = securityProtocolType;
            Console.WriteLine("Starting from ====>>> " + currentPage);
            var response = await Client.GetAsync($"{GhanaJobBaseUrl}{currentPage}");
            response.EnsureSuccessStatusCode();
            if (response.IsSuccessStatusCode)
            {
                var htmlContent = await response.Content.ReadAsStringAsync();
                htmlDocument.LoadHtml(htmlContent);
                var xpathExpression = $"//*[contains(@class, '{targetClass}')]";
                var nodes = htmlDocument.DocumentNode.SelectNodes(xpathExpression);
                if (nodes != null)
                    foreach (var node in nodes)
                    {
                        var actualJobUrl = node.GetAttributeValue("data-href", "");
                        Console.WriteLine(actualJobUrl);
                        UrlList.Add(actualJobUrl);
                    }
            }
            else
            {
                Console.WriteLine($"Error from jobs url processing: {response.StatusCode} - {response.ReasonPhrase}");
                _logger.LogError($"Error from jobs url processing: {response.StatusCode} - {response.ReasonPhrase}");
            }

            var nextPageNode = htmlDocument.DocumentNode.SelectSingleNode("//li[@class='pager-item active']");
            if (nextPageNode == null) break;
            currentPage++;
            Console.WriteLine("Finished with ====>>> " + currentPage);
            _logger.LogInformation("Finished with ====>>> " + currentPage);
        }

        Console.WriteLine("Finished extracting job urls from target domain");
        _logger.LogInformation("Finished extracting job urls from target domain");
        return UrlList;
    }

    private List<string> FilterOnlyNewJobsUrls(IEnumerable<string> alreadyExistingJobsIds, IEnumerable<string> scrappedJobsIds)
    {
        Console.WriteLine("Started filtering only new jobs urls ============");
        _logger.LogInformation("Started performing LINQ operation ============");
        _newJobsIds = scrappedJobsIds.Except(alreadyExistingJobsIds).ToList();
        _logger.LogInformation("Finished performing LINQ operation ============");
        _logger.LogInformation("Started filtering for only newly added jobs urls ============");
        var filteredJobsUrls = UrlList
            .Where(url => _newJobsIds.Any(id => url.Contains($"-{id}")))
            .ToList();
        _logger.LogInformation("Finished filtering job urls ============");
        Console.WriteLine("Done filtering only new jobs urls ============");
        return filteredJobsUrls;
    }

    private async Task<List<string>> FetchAlreadyExistingJobsIds()
    {
        try
        {
            var results = await _dbContext.Staging_GhanaJobIds!.ToListAsync();
            var list = results.Select(id => id.JobId).ToList();
            Console.WriteLine("Getting old job ids from database ============");
            _logger.LogInformation("Getting jobs old job ids from database ============");
            return list;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            _logger.LogError(e.Message);
            throw;
        }
    }

    private List<GhanaJobId> ConvertNewJobIdsToGhanaJobId(List<string> newJobIds)
    {
        var records = new List<GhanaJobId>();
        foreach (var item in newJobIds)
        {
            records.Add(new GhanaJobId { JobId = item });
            Console.WriteLine("Mapping new jobs new job ids to GhanaJobId object ============");
            _logger.LogInformation("Mapping new jobs new job ids to GhanaJobId class ============");
        }

        return records;
    }
}