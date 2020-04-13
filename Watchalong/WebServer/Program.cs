using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Watchalong.Config;

namespace WebServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            const string CONFIG_FILE_LOCATION = "config.yaml";

            //Load the config
            Configuration config = ConfigParser.LoadConfig<Configuration>(CONFIG_FILE_LOCATION);

            Server.Init(config.WebHostIp, config.WebHostPort, config.MediaserverHostIp, config.MediaserverHostPort);

            //Start the WebMedia server
            Server.StartWebMediaServer();

            //Start the web interface
            Task webTask = Server.StartWebInterface(args).Result;
            webTask.Wait();
        }
    }
}
