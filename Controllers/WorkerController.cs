using Crawler.Service;
using Hangfire;
using Microsoft.AspNetCore.Mvc;

namespace Crawler.Controllers;

[ApiController]
public class WorkerController : ControllerBase
{
    [HttpGet("/")]
    public Task<ActionResult> Home()
    {
        return Task.FromResult<ActionResult>(Ok("Welcome"));
    }

    [HttpGet("/ghanaJobs")]
    public Task<ActionResult> Test()
    {
        var enqueue = BackgroundJob.Enqueue<IJobsService>(service =>
            service.BackgroundWorker());
        return Task.FromResult<ActionResult>(Ok(enqueue));
    }
}