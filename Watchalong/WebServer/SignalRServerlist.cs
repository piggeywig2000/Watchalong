using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebServer.SignalR.Serverlist
{
    public class ServerlistInfo
    {
        public ServerInfo[] Servers { get; set; } = new ServerInfo[0];
    }

    public class ServerInfo
    {
        public int ServerUuid { get; set; } = int.MaxValue;

        public string Name { get; set; } = "";

        public bool HasPassword { get; set; } = false;

        public string ImageUrl { get; set; } = "";

        public int UserCount { get; set; } = 0;
    }
}
