namespace Crawler.Service;

public interface IJobsService
{
   Task BackgroundWorker(string filePath);
}