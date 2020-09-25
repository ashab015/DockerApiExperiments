using Docker.DotNet.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DockerApi.Services
{
    public interface IDockerService
    {
        Task<IList<ContainerListResponse>> GetAllContainers(int take = int.MaxValue, bool serviceCreated = false);
        Task<bool> StopDockerContainer(string containerId);
        Task<bool> CreateMySqlDatabase(bool removeIfExist = true);
        Task<bool> CreateAmazonDynamoDb(bool removeIfExist = true);
    }
}
