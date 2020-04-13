using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Watchalong.Utils;
using Watchalong.Config;

namespace MediaSever
{
    class Program
    {
        static void Main(string[] args)
        {
#if DEBUG
            Task.Delay(6000).Wait();
#endif
            const string CONFIG_FILE_LOCATION = "config.yaml";

            //Load the config
            Configuration config = ConfigParser.LoadConfig<Configuration>(CONFIG_FILE_LOCATION);

            //Validate config
            if (!Directory.Exists(config.PathToMedia))
            {
                ConLog.Log("Configuration", "The path to the media files folder doesn't exist", LogType.Fatal);
            }
            if (config.Name == "Untitled")
            {
                ConLog.Log("Configuration", "Please change the name of the media server from its default value", LogType.Fatal);
            }
            if (string.IsNullOrWhiteSpace(config.Name))
            {
                ConLog.Log("Configuration", "The name must not be left blank", LogType.Fatal);
            }
            if (string.IsNullOrWhiteSpace(config.Password))
            {
                config.Password = "";
            }

            //Delete and create the downloads folder
            if (Directory.Exists(config.PathToDownload))
                Directory.Delete(config.PathToDownload, true);
            Directory.CreateDirectory(config.PathToDownload);
            Directory.CreateDirectory(config.PathToDownload + "\\incomplete");

            //Connect to webserver
            WebMediaClient serverLink = new WebMediaClient(config.WebserverIp, config.WebserverPort, config.HttpHostIp, config.HttpHostPort, config.Name, config.Password, config.PathToMedia, config.PathToDownload);
            serverLink.ConnectToWebserver().Wait();
            Task.Delay(-1, serverLink.CancelToken).Wait();

            ConLog.Log("Program", "End of program", LogType.Info);
            Environment.Exit(0);
        }
    }
}
