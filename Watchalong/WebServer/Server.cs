using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR.Client;
using Network;
using Newtonsoft.Json;
using Watchalong.Utils;

namespace WebServer
{
    public static class Server
    {
        /// <summary>
        /// The IP to host the web interface on
        /// </summary>
        public static string WebServerIp { get; private set; }

        /// <summary>
        /// The port to host the web interface on
        /// </summary>
        public static ushort WebServerPort { get; private set; }

        /// <summary>
        /// The IP to host the webmedia link on
        /// </summary>
        private static string WebMediaIp { get; set; }

        /// <summary>
        /// The port to host the webmedia link on
        /// </summary>
        private static ushort WebMediaPort { get; set; }

        /// <summary>
        /// The server that media servers connect to
        /// </summary>
        private static ServerConnectionContainer WebMediaServer { get; set; } = null;

        /// <summary>
        /// A dictionary that connects MediaServer UUIDs to their object
        /// </summary>
        public static Dictionary<int, MediaServer> UuidToMediaServer { get; set; } = new Dictionary<int, MediaServer>();

        /// <summary>
        /// A dictionary that connects a MediaServer connection to its UUID
        /// </summary>
        private static Dictionary<TcpConnection, int> ConnectionToUuid { get; set; } = new Dictionary<TcpConnection, int>();

        /// <summary>
        /// The connection to the signalr hub
        /// </summary>
        private static HubConnection SignalRConnection { get; set; } = null;

        /// <summary>
        /// Gets the connection state of the server's SignalR client
        /// </summary>
        public static HubConnectionState SignalRConnectionState { get
            {
                if (SignalRConnection != null)
                {
                    return SignalRConnection.State;
                }
                else
                {
                    return HubConnectionState.Disconnected;
                }
            } }

        /// <summary>
        /// The ID of the server's SignalR connection
        /// </summary>
        public static string ServerSignalRId => SignalRConnection.ConnectionId;

        /// <summary>
        /// Creates a new server with the provided IP and port
        /// </summary>
        /// <param name="webInterfaceIp">The IP to host the web interface on</param>
        /// <param name="webInterfacePort">The port to host the web interface on</param>
        /// <param name="webmediaIp">The IP to host the webmedia link on</param>
        /// <param name="webmediaPort">The port to host the webmedia link on</param>
        public static void Init(string webInterfaceIp, ushort webInterfacePort, string webmediaIp, ushort webmediaPort)
        {
            WebServerIp = webInterfaceIp;
            WebServerPort = webInterfacePort;
            WebMediaIp = webmediaIp;
            WebMediaPort = webmediaPort;
        }

        /// <summary>
        /// Starts the web server with the provided arguments, IP and port
        /// </summary>
        /// <param name="args">The arguments to pass to the builder</param>
        /// <returns>A Task that ends when the web server stops</returns>
        public static async Task<Task> StartWebInterface(string[] args)
        {
            ConLog.Log("WebMedia Link", "Starting web interface", LogType.Info);

            Task webTask = Task.Run(async () => await Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseUrls("http://" + WebServerIp + ":" + WebServerPort + "/");
                }).Build().RunAsync());


            ConLog.Log("WebMedia Link", "Starting SignalR", LogType.Info);

            //Start SignalR
            SignalRConnection = new HubConnectionBuilder()
                .WithAutomaticReconnect()
                .WithUrl("http://" + WebServerIp + ":" + WebServerPort + "/serverlistHub")
                .Build();

            //Create event handlers
            HubConnectionExtensions.On<string>(SignalRConnection, "ClientConnected", SignalRClientConnected);

            await SignalRConnection.StartAsync();

            ConLog.Log("WebMedia Link", "Web interface started", LogType.Ok);

            return webTask;
        }

        /// <summary>
        /// Starts the web media server
        /// </summary>
        public static void StartWebMediaServer()
        {
            //Stop the server if it's already running
            if (WebMediaServer != null)
                if (WebMediaServer.IsTCPOnline)
                    WebMediaServer.Stop();

            //Create webserver
            ConLog.Log("WebMedia Link", "Starting WebMedia link server", LogType.Info);
            WebMediaServer = ConnectionFactory.CreateServerConnectionContainer(/*WebMediaIp,*/ WebMediaPort, false);
            WebMediaServer.AllowUDPConnections = false;

            //Create connection established and lost handlers
            WebMediaServer.ConnectionEstablished += async (connection, type) => await WebMediaConnected(connection, type);
            WebMediaServer.ConnectionLost += async (connection, type, reason) => await WebMediaDisconnected(connection, type, reason);

            //Start webserver
            WebMediaServer.Start();
            ConLog.Log("WebMedia Link", "WebMedia link server started", LogType.Ok);
        }

        /// <summary>
        /// Called when a media server connects. Adds the connection to the appropriate dictionaries
        /// </summary>
        /// <param name="connection">The connection that connected</param>
        /// <param name="type">The type of connection. Should be TCP</param>
        /// <returns>A Task representing the operation</returns>
        private static async Task WebMediaConnected(Connection connection, Network.Enums.ConnectionType type)
        {
            //Check that it's TCP
            if (type != Network.Enums.ConnectionType.TCP) return;

            ConLog.Log("WebMedia Link", "Media server " + connection.IPRemoteEndPoint.ToString() + " connected", LogType.Info);

            //Check that SignalR is connected - if not, disconnect them
            if (SignalRConnectionState != HubConnectionState.Connected)
            {
                ConLog.Log("WebMedia Link", "Disconnecting media server " + connection.IPRemoteEndPoint.ToString() + " because the server is not done setting up", LogType.Info);
                connection.Close(Network.Enums.CloseReason.ClientClosed);
                return;
            }

            //Create a new media server with the given name
            MediaServer newServer = new MediaServer((TcpConnection)connection);

            //Initiate
            await newServer.Init();

            //Add to dictionaries
            UuidToMediaServer.Add(newServer.UUID, newServer);
            ConnectionToUuid.Add(newServer.Connection, newServer.UUID);

            //Update SignalR
            await UpdateAllSignalRClients();

            ConLog.Log("WebMedia Link", "Added media server with name " + newServer.Name + " and address " + newServer.Connection.IPRemoteEndPoint.ToString(), LogType.Ok);
        }

        /// <summary>
        /// Called when a media server disconnects. Removes the connection from the appropriate dictionaries
        /// </summary>
        /// <param name="connection">The connection that disconnected</param>
        /// <param name="type">The type of connection. Should be TCP</param>
        /// <param name="reason">The reason for disconnecting</param>
        /// <returns>A Task representing the operation</returns>
        private static async Task WebMediaDisconnected(Connection connection, Network.Enums.ConnectionType type, Network.Enums.CloseReason reason)
        {
            //Check that it's TCP
            if (type != Network.Enums.ConnectionType.TCP) return;

            //Check that it exists
            if (ConnectionToUuid.ContainsKey((TcpConnection)connection))
            {
                //Get the UUID
                MediaServer deadServer = UuidToMediaServer[ConnectionToUuid[(TcpConnection)connection]];

                ConLog.Log("WebMedia Link", "Media server with name " + deadServer.Name + " and address " + connection.IPRemoteEndPoint.ToString() + " disconnected", LogType.Info);

                //Remove the packet handlers
                connection.UnRegisterRawDataHandler("AvailableFilesUpdated");

                //Close it
                await deadServer.Close(false);

                //Remove from dictionaries
                UuidToMediaServer.Remove(deadServer.UUID);
                ConnectionToUuid.Remove((TcpConnection)connection);

                //Update SignalR
                await UpdateAllSignalRClients();

                ConLog.Log("WebMedia Link", "Removed media server with name " + deadServer.Name, LogType.Ok);
            }
            else
            {
                ConLog.Log("WebMedia Link", "Media server with address " + connection.IPRemoteEndPoint.ToString() + " disconnected", LogType.Info);
            }
            

        }

        /// <summary>
        /// Sends a specific SignalR client updated information
        /// </summary>
        /// <param name="clientId">The client that connected</param>
        /// <returns>A Task representing the operation</returns>
        private static async Task SignalRClientConnected(string clientId)
        {
            string responseContent = GetServerlistJson();

            await HubConnectionExtensions.InvokeAsync(SignalRConnection, "SendUpdateSpecific", responseContent, clientId);
        }

        /// <summary>
        /// Updates all SignalR clients with new information
        /// </summary>
        /// <returns>A Task representing the operation</returns>
        private static async Task UpdateAllSignalRClients()
        {
            string responseContent = GetServerlistJson();

            await HubConnectionExtensions.InvokeAsync(SignalRConnection, "SendUpdateAll", responseContent);
        }

        /// <summary>
        /// Gets the information about all servers as a JSON string
        /// </summary>
        /// <returns>The JSON string representing the information about the servers</returns>
        private static string GetServerlistJson()
        {
            SignalR.Serverlist.ServerlistInfo info = new SignalR.Serverlist.ServerlistInfo();

            info.Servers = new SignalR.Serverlist.ServerInfo[UuidToMediaServer.Count];

            Dictionary<int, MediaServer>.ValueCollection.Enumerator enumerator = UuidToMediaServer.Values.GetEnumerator();
            for (int i = 0; i < UuidToMediaServer.Count; i++)
            {
                enumerator.MoveNext();
                MediaServer thisServer = enumerator.Current;
                info.Servers[i] = new SignalR.Serverlist.ServerInfo() { 
                    Name = thisServer.Name, 
                    HasPassword = thisServer.Password != "", 
                    ImageUrl = thisServer.ImageUrl, 
                    UserCount = thisServer.UserCount, 
                    ServerUuid = thisServer.UUID };
            }

            return JsonConvert.SerializeObject(info);
        }
    }
}
