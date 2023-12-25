using Crawler.Service;
using Hangfire;
using Microsoft.AspNetCore.Mvc;

namespace Crawler.Controllers;

[ApiController]
public class WorkerController : ControllerBase
{
     private readonly IJobsService _jobsService;

     public WorkerController(IJobsService jobsService)
     {
          _jobsService = jobsService;
     }

     [HttpGet("/")]
     public async Task<ActionResult> Home()
     {
          return Ok("Welcome");
     }

     [HttpGet("/ghanaJobs")]
     public async Task<ActionResult> Test()
     {
          var enqueue = BackgroundJob.Enqueue<IJobsService>(service =>
               service.BackgroundWorker());
          return Ok(enqueue);
     }

}