using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DockerApi.SignalR
{
    public class SignalRHub : Hub
    {
        public async Task Send(string user, string message)
        {
            await Clients.All.SendAsync("message", "app", message);
        }
    }
}
