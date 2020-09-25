using Docker.DotNet;
using Docker.DotNet.Models;
using DockerApi.Models;
using DockerApi.SignalR;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Timer = System.Timers.Timer;

namespace DockerApi.Services
{
    public class DockerService : IDockerService
    {
        private readonly static DockerClient _dockerClient;
        private const string CONTAINER_TAG = "dockerservice";
        private readonly Timer _signalrMonitorTimer;
        private readonly static Progress _progress;

        static DockerService()
        {
            _dockerClient = new DockerClientConfiguration(new Uri("npipe://./pipe/docker_engine")).CreateClient();
            _progress = new Progress();
            _progress.OnUpdate += (object sender, JSONMessage e) => SignalRUpdate(sender, e);
        }

        private async void MonitorTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            var containers = await GetAllContainers(serviceCreated: true);
            var monitorData = containers.Select(x => new DockerMonitorData()
            {
                Name = x.Names.FirstOrDefault(),
                Id = x.ID,
                State = x.State,
                Status = x.Status,
                CreatedOn = x.Created.ToString("MM/dd/yyyy hh:mm"),
            });

            SignalRUpdate(this, new JSONMessage()
            {
                Aux = new ObjectExtensionData()
                {
                    ExtensionData = new Dictionary<string, object>()
                    {
                        { "data", monitorData }
                    }
                }
            }, "monitor");
        }

        public static IHubContext<SignalRHub> HubContext;

        public DockerService(IHubContext<SignalRHub> hubContext)
        {
            HubContext = hubContext;

            _signalrMonitorTimer = new Timer();
            _signalrMonitorTimer.Elapsed += MonitorTimerElapsed;
            _signalrMonitorTimer.Interval = 1000; // 1000 ms is one second
            _signalrMonitorTimer.Start();
        }

        class Progress : IProgress<JSONMessage>
        {
            public event EventHandler<JSONMessage> OnUpdate;

            void IProgress<JSONMessage>.Report(JSONMessage value)
            {
                OnUpdate(this, value);
            }
        }

        public async Task<IList<ContainerListResponse>> GetAllContainers(int take = int.MaxValue, bool serviceCreated = false)
        {
            IList<ContainerListResponse> containers = await _dockerClient.Containers.ListContainersAsync(
                new ContainersListParameters()
                {
                    Limit = take,
                    All = true,
                });

            if (serviceCreated)
            {
                containers = containers.Where(x => x.Names.Any(x=>x.EndsWith(CONTAINER_TAG))).ToList();
            }

            return containers;
        }

        public async Task<bool> StopDockerContainer(string containerId)
        {
            return await _dockerClient.Containers.StopContainerAsync(
               containerId,
                new ContainerStopParameters
                {
                    WaitBeforeKillSeconds = 30
                },
                CancellationToken.None);
        }

        public async Task<bool> CreateMySqlDatabase(bool removeIfExist = true)
        {
            string containerName = "MySqlServer";
            string imageName = "mysql/mysql-server";
            string imageTag = "latest";

            var containers = await _dockerClient.Containers.ListContainersAsync(new ContainersListParameters() { All = true });
            var container = containers.FirstOrDefault(c => c.Names.Contains($"/{containerName}_{CONTAINER_TAG}"));

            try
            {
                if (removeIfExist && container != null)
                {
                    await DeleteContainerAndImage(container.ID, imageName);
                }

                if (container == null || removeIfExist == true)
                {
                    // Download image
                    await _dockerClient.Images.CreateImageAsync(new ImagesCreateParameters() { FromImage = imageName, Tag = imageTag }, new AuthConfig(), _progress);

                    // Create a empty docker container
                    var config = new Config()
                    {
                        Hostname = "localhost",
                    };

                    // Configure the ports to expose
                    var hostConfig = new HostConfig()
                    {
                        PortBindings = new Dictionary<string, IList<PortBinding>>
                        {
                            { "80/tcp", new List<PortBinding> { new PortBinding { HostIP = "127.0.0.1", HostPort = "8080" } } }
                        }
                    };

                    SignalRUpdate(this, new JSONMessage() { Status = "Create the docker container..." });

                    // Create the container
                    var response = await _dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters(config)
                    {
                        Image = imageName + ":" + imageTag,
                        Name = $"{containerName}_{CONTAINER_TAG}",
                        Tty = false,
                        HostConfig = hostConfig,
                        Env = new List<string>()
                        {
                            "MYSQL_USER=root",
                            "MYSQL_ROOT_PASSWORD=password"
                        },
                    });

                    SignalRUpdate(this, new JSONMessage() { Status = $"The docker container was successfully created. Id: {response.ID}" });

                    // Get the container object
                    containers = await _dockerClient.Containers.ListContainersAsync(new ContainersListParameters() { All = true });
                    container = containers.First(c => c.ID == response.ID);
                }

                SignalRUpdate(this, new JSONMessage() { Status = $"The docker container has been started. Id: {container.ID}" });

                return await _dockerClient.Containers.StartContainerAsync(container.ID, new ContainerStartParameters());
            }
            catch (Exception ex)
            {
                SignalRUpdate(this, new JSONMessage() { Status = $"Error: {ex.Message}" });

                return false;
            }
        }

        public async Task<bool> CreateAmazonDynamoDb(bool removeIfExist = true)
        {
            string containerName = "DynamoDb";
            var containers = await _dockerClient.Containers.ListContainersAsync(new ContainersListParameters() { All = true });
            var container = containers.FirstOrDefault(c => c.Names.Contains("/" + containerName));

            try
            {
                if (container == null)
                {
                    // Create and pull the image for dynamo db.
                    var containerResponse = await _dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters
                    {
                        Name = $"{containerName}_{CONTAINER_TAG}",
                        Image = "amazon/dynamodb-local",

                        ExposedPorts = new Dictionary<string, EmptyStruct>
                    {
                        {
                            "8000", default(EmptyStruct)
                        }
                    },
                        HostConfig = new HostConfig
                        {
                            PortBindings = new Dictionary<string, IList<PortBinding>>
                        {
                            {"8000", new List<PortBinding> {new PortBinding {HostPort = "8000"}}}
                        },
                            PublishAllPorts = true
                        }
                    });

                    // Get the container object
                    containers = await _dockerClient.Containers.ListContainersAsync(new ContainersListParameters() { All = true });
                    container = containers.First(c => c.ID == containerResponse.ID);
                }
            }
            catch (Exception)
            {

            }

            return await _dockerClient.Containers.StartContainerAsync(container.ID, null);
        }

        public async Task DeleteContainerAndImage(string containerId, string image)
        {
            SignalRUpdate(this, new JSONMessage() { Status = $"Attempting to delete container {containerId}." });

            // Stop container
            await _dockerClient.Containers.StopContainerAsync(containerId, new ContainerStopParameters()
            {
                WaitBeforeKillSeconds = 1,
            });

            // Forcefully delete the container, its links and volumes.
            await _dockerClient.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters()
            {
                Force = true,
            });

            SignalRUpdate(this, new JSONMessage() { Status = $"Stopped and removed container {containerId}." });

            // Forcefully delete the image and children.
            await _dockerClient.Images.DeleteImageAsync(image, new ImageDeleteParameters()
            {
                Force = true,
                PruneChildren = true
            });

            SignalRUpdate(this, new JSONMessage() { Status = $"Existing docker image {image} being deleted." });
        }

        private static void SignalRUpdate(object sender, JSONMessage message, string type = "progress")
        {
            var obj = new
            {
                type = type,
                progress = message.Progress,
                progressMessage = message.ProgressMessage,
                status = message.Status,
                objects = message.Aux
            };
            string json = JsonConvert.SerializeObject(obj);
            HubContext.Clients.All.SendAsync("message", "app", json);
        }
    }
}
