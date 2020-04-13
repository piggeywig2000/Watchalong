using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Watchalong.Utils;

namespace WebServer
{
    public class ServerlistHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            //Check that the connection ID is not of the server
            if (Server.SignalRConnectionState != Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected)
            {
                return;
            }

            if (Server.ServerSignalRId == Context.ConnectionId)
                return;
            
            await Clients.Client(Server.ServerSignalRId).SendAsync("ClientConnected", Context.ConnectionId);
        }

        public async Task SendUpdateSpecific(string dataToSend, string connectionId)
        {
            //Check that it's the server that sent this
            if (Context.ConnectionId != Server.ServerSignalRId) return;

            IClientProxy client = Clients.Client(connectionId);

            if (client == null) return;

            //Client exists, send them the message
            await client.SendAsync("ListUpdated", dataToSend);
        }

        public async Task SendUpdateAll(string dataToSend)
        {
            //Check that it's the server that sent this
            if (Context.ConnectionId != Server.ServerSignalRId) return;

            await Clients.Others.SendAsync("ListUpdated", dataToSend);
        }
    }

    public enum PlaybackStateUpdateType
    {
        PlayPause,
        Seek
    }

    public class ServerHub : Hub
    {
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            MediaServer server = null;

            //Find the media server that this user was in
            foreach (MediaServer thisMediaServer in Server.UuidToMediaServer.Values)
            {
                //Check all users
                foreach (User thisUser in thisMediaServer.Users.Values)
                {
                    if (thisUser.ConnectionId == Context.ConnectionId)
                    {
                        server = thisMediaServer;
                    }
                }
            }

            //If we didn't find one, that means the user wasn't logged in, so forget about them
            if (server != null)
            {
                //Send the client disconnected
                IClientProxy client = Clients.Client(server.ServerSignalRId);

                if (client != null)
                {
                    //Client exists, send them the message
                    await client.SendAsync("UserDisconnect", server.SignalRConnectionToUser[Context.ConnectionId]);
                }
            }

            await base.OnDisconnectedAsync(exception);
            return;
        }

        public async Task Login(int serverUuid, string username, string password)
        {
            string errorMessage = "";

            //Check that this connection ID isn't already logged in
            foreach (MediaServer thisMediaServer in Server.UuidToMediaServer.Values)
            {
                //Check the server's signalR client
                if (thisMediaServer.ServerSignalRId == Context.ConnectionId) 
                    errorMessage = "Error: Already logged in. Try reloading";

                //Check all users
                foreach(User thisUser in thisMediaServer.Users.Values)
                {
                    if (thisUser.ConnectionId == Context.ConnectionId)
                        errorMessage = "Error: Already logged in. Try reloading";
                }
            }

            //Check that the media server exists
            if (!Server.UuidToMediaServer.ContainsKey(serverUuid))
            {
                errorMessage = "Error: Media server not found. Maybe the media server went offline?";
            }

            //Check that the username is not whitespace
            if (string.IsNullOrWhiteSpace(username))
            {
                errorMessage = "Error: Username cannot be blank";
            }

            //Check that the username is longer than 32 characters
            if (username.Length > 32)
            {
                errorMessage = "Error: Username cannot be longer than 32 characters";
            }

            //Send error message
            if (errorMessage != "")
            {
                await Clients.Caller.SendAsync("LoginError", errorMessage);
                return;
            }

            //Get media server
            MediaServer mediaServer = Server.UuidToMediaServer[serverUuid];

            //Check that the passwords match
            if (mediaServer.Password != password)
            {
                errorMessage = "Error: Incorrect password";
            }

            //Check that the username isn't already taken
            foreach (User thisUser in mediaServer.Users.Values)
            {
                if (thisUser.Username == username)
                {
                    errorMessage = "Error: Username taken";
                }
            }

            //Send error message
            if (errorMessage != "")
            {
                await Clients.Caller.SendAsync("LoginError", errorMessage);
                return;
            }

            //Right, we're good to go
            await Clients.Client(mediaServer.ServerSignalRId).SendAsync("LoginRequest", Context.ConnectionId, username);
        }

        public async Task ServerLoginAccept(int mediaServerUuid, string connectionIdOfUser)
        {
            //Check that the media server exists
            if (!Server.UuidToMediaServer.ContainsKey(mediaServerUuid)) return;

            //Check that it's the server that sent this
            if (Context.ConnectionId != Server.UuidToMediaServer[mediaServerUuid].ServerSignalRId) return;

            //Send the login accepted
            IClientProxy client = Clients.Client(connectionIdOfUser);

            if (client == null) return;

            //Client exists, send them the message
            await client.SendAsync("LoginAccept", mediaServerUuid);
        }

        public async Task ServerUpdateCurrentState(int mediaServerUuid, string jsonData)
        {
            //Check that the media server exists
            if (!Server.UuidToMediaServer.ContainsKey(mediaServerUuid)) return;

            //Check that it's the server that sent this
            if (Context.ConnectionId != Server.UuidToMediaServer[mediaServerUuid].ServerSignalRId) return;

            //Send to all clients
            foreach(User user in Server.UuidToMediaServer[mediaServerUuid].Users.Values)
            {
                await Clients.Client(user.ConnectionId).SendAsync("CurrentStateUpdated", jsonData);
            }
        }

        public async Task UpdateUserState(int mediaServerUuid, int currentMediaUuid, bool isPlaying, double lastSeekPosition, UserBufferState bufferState)
        {
            //Check that the media server exists
            if (!Server.UuidToMediaServer.ContainsKey(mediaServerUuid)) return;

            MediaServer mediaServer = Server.UuidToMediaServer[mediaServerUuid];

            //Check that the user exists
            if (!mediaServer.SignalRConnectionToUser.ContainsKey(Context.ConnectionId)) return;

            int userUuid = mediaServer.SignalRConnectionToUser[Context.ConnectionId];
            User user = mediaServer.Users[userUuid];

            //If nothing changed, drop it
            if (user.CurrentMediaUuid == currentMediaUuid && user.IsPlaying == isPlaying && user.LastSeekPosition == lastSeekPosition && user.BufferState == bufferState)
            {
                return;
            }

            await Clients.Client(mediaServer.ServerSignalRId).SendAsync("ServerUpdateUserState", userUuid, currentMediaUuid, isPlaying, lastSeekPosition, bufferState);
        }

        public async Task UpdatePlaybackState(int mediaServerUuid, PlaybackStateUpdateType opcode, string operand)
        {
            //Check that the media server exists
            if (!Server.UuidToMediaServer.ContainsKey(mediaServerUuid)) return;

            MediaServer mediaServer = Server.UuidToMediaServer[mediaServerUuid];

            //Check that the user exists
            if (!mediaServer.SignalRConnectionToUser.ContainsKey(Context.ConnectionId)) return;

            int userUuid = mediaServer.SignalRConnectionToUser[Context.ConnectionId];

            await Clients.Client(mediaServer.ServerSignalRId).SendAsync("ServerUpdatePlaybackState", opcode, operand);
        }

        public async Task ServerUpdateQueue(int mediaServerUuid, string jsonData)
        {
            //Check that the media server exists
            if (!Server.UuidToMediaServer.ContainsKey(mediaServerUuid)) return;

            //Check that it's the server that sent this
            if (Context.ConnectionId != Server.UuidToMediaServer[mediaServerUuid].ServerSignalRId) return;

            //Send to all clients
            foreach (User user in Server.UuidToMediaServer[mediaServerUuid].Users.Values)
            {
                await Clients.Client(user.ConnectionId).SendAsync("QueueUpdated", jsonData);
            }
        }

        public async Task ModifyQueue(int mediaServerUuid, int[] newQueue, bool shouldResetPlayback)
        {
            //Check that the media server exists
            if (!Server.UuidToMediaServer.ContainsKey(mediaServerUuid)) return;

            MediaServer mediaServer = Server.UuidToMediaServer[mediaServerUuid];

            //Check that the user exists
            if (!mediaServer.SignalRConnectionToUser.ContainsKey(Context.ConnectionId)) return;

            int userUuid = mediaServer.SignalRConnectionToUser[Context.ConnectionId];

            await Clients.Client(mediaServer.ServerSignalRId).SendAsync("ServerModifyQueue", newQueue, shouldResetPlayback);
        }

        public async Task ServerUpdateFiles(int mediaServerUuid, string jsonData)
        {
            //Check that the media server exists
            if (!Server.UuidToMediaServer.ContainsKey(mediaServerUuid)) return;

            //Check that it's the server that sent this
            if (Context.ConnectionId != Server.UuidToMediaServer[mediaServerUuid].ServerSignalRId) return;

            //Send to all clients
            foreach (User user in Server.UuidToMediaServer[mediaServerUuid].Users.Values)
            {
                await Clients.Client(user.ConnectionId).SendAsync("FilesUpdated", jsonData);
            }
        }

        public async Task DownloadMedia(int mediaServerUuid, string urlToDownload)
        {
            //Check that the media server exists
            if (!Server.UuidToMediaServer.ContainsKey(mediaServerUuid)) return;

            MediaServer mediaServer = Server.UuidToMediaServer[mediaServerUuid];

            //Check that the user exists
            if (!mediaServer.SignalRConnectionToUser.ContainsKey(Context.ConnectionId)) return;

            int userUuid = mediaServer.SignalRConnectionToUser[Context.ConnectionId];

            await Clients.Client(mediaServer.ServerSignalRId).SendAsync("ServerDownloadMedia", urlToDownload);
        }

        public async Task ServerClosed(int mediaServerUuid)
        {
            //Check that the media server exists
            if (!Server.UuidToMediaServer.ContainsKey(mediaServerUuid)) return;

            //Check that it's the server that sent this
            if (Context.ConnectionId != Server.UuidToMediaServer[mediaServerUuid].ServerSignalRId) return;

            //Send to all clients
            foreach (User user in Server.UuidToMediaServer[mediaServerUuid].Users.Values)
            {
                await Clients.Client(user.ConnectionId).SendAsync("Closed");
            }
        }
    }
}
