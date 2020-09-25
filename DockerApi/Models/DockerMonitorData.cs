using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DockerApi.Models
{
    public class DockerMonitorData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string State { get; set; }
        public string Status { get; set; }
        public string CreatedOn { get; set; }
    }
}
