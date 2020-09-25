using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DockerApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DockerApi.Controllers
{
    [ApiController]
    public class DockerController : Controller
    {
        private readonly IDockerService _dockerService;
        private readonly ILogger<DockerController> _logger;

        public DockerController(ILogger<DockerController> logger, IDockerService dockerService)
        {
            _logger = logger;
            _dockerService = dockerService;
        }

        [HttpGet]
        [Route("api/getallcontainers")]
        public async Task<IActionResult> GetAllContainers(bool serviceCreated = false)
        {
            var results = await _dockerService.GetAllContainers(serviceCreated: serviceCreated);
            return View("JsonViewer", JsonConvert.SerializeObject(results));
        }

        [HttpGet]
        [Route("api/createmysqldatabase")]
        public async Task<IActionResult> CreateMySqlDatabase()
        {
            var results = await _dockerService.CreateMySqlDatabase();
            return View("JsonViewer", JsonConvert.SerializeObject(results));
        }

        [HttpGet]
        [Route("api/createamazondynamodb")]
        public async Task<IActionResult> CreateAmazonDynamoDb()
        {
            var results = await _dockerService.CreateAmazonDynamoDb();
            return View("JsonViewer", JsonConvert.SerializeObject(results));
        }

        [HttpGet]
        [Route("docker/orchestration")]
        public async Task<IActionResult> Orchestration()
        {
            return View();
        }
    }
}
