using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Network;
using Newtonsoft.Json;
using WebMediaLink;
using Watchalong.Utils;
using Microsoft.AspNetCore.SignalR.Client;

namespace WebServer
{
    public class MediaServer
    {
        /// <summary>
        /// The UUID of the MediaServer
        /// </summary>
        public int UUID { get; } = int.MaxValue;

        /// <summary>
        /// The name of the MediaServer
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// The URL of the MediaServer's image
        /// </summary>
        public string ImageUrl { get; set; } = "";

        /// <summary>
        /// The users connected to this media server
        /// </summary>
        public Dictionary<int, User> Users { get; set; } = new Dictionary<int, User>();

        /// <summary>
        /// Converts a SignalR connection ID to a user
        /// </summary>
        public Dictionary<string, int> SignalRConnectionToUser { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// The number of SignalR users on this media server
        /// </summary>
        public int UserCount { get => Users.Count; }

        /// <summary>
        /// The available files on the MediaServer
        /// </summary>
        private Dictionary<int, ServerFile> Files { get; set; } = new Dictionary<int, ServerFile>();

        /// <summary>
        /// The current media queue
        /// </summary>
        private List<int> Queue { get; set; } = new List<int>();

        /// <summary>
        /// The UUID of the currently playing media
        /// </summary>
        private int CurrentMediaUuid { get
            {
                if (Queue.Count == 0)
                {
                    return int.MaxValue;
                }
                else
                {
                    if (Files[Queue[0]].IsAvailable)
                    {
                        return Queue[0];
                    }
                    else
                    {
                        return int.MaxValue;
                    }
                }
            } }

        /// <summary>
        /// Whether the media player is currently playing (disregarding faking the playstate)
        /// </summary>
        private bool IsPlaying { get; set; } = false;

        /// <summary>
        /// The seek position that was the last time we seeked
        /// </summary>
        private double LastSeekPosition { get; set; } = 0;

        private enum FakingState
        {
            NotFaking,
            Faking,
            PostFaking
        }

        /// <summary>
        /// The current state at which we're faking
        /// </summary>
        private FakingState FakeState { get; set; } = FakingState.NotFaking;

        /// <summary>
        /// Whether the media player is currently playing, taking into account the faking of the playstate
        /// </summary>
        private bool ActualIsPlaying { get { 
                if (FakeState != FakingState.Faking)
                {
                    return IsPlaying;
                }
                else 
                {
                    return false;
                }
            } }

        /// <summary>
        /// The available fonts for subtitles
        /// </summary>
        private string[] SubtitleFonts { get; set; } = new string[0];

        /// <summary>
        /// Keeps track of the current position in the media
        /// </summary>
        private SettableStopwatch PositionTracker { get; } = new SettableStopwatch();

        /// <summary>
        /// The password of the media server
        /// </summary>
        public string Password { get; set; } = "";

        /// <summary>
        /// The connection to the signalr hub
        /// </summary>
        private HubConnection SignalRConnection { get; set; } = null;

        /// <summary>
        /// Gets the connection state of the server's SignalR client
        /// </summary>
        public HubConnectionState SignalRConnectionState
        {
            get
            {
                if (SignalRConnection != null)
                {
                    return SignalRConnection.State;
                }
                else
                {
                    return HubConnectionState.Disconnected;
                }
            }
        }

        /// <summary>
        /// The ID of the server's SignalR connection
        /// </summary>
        public string ServerSignalRId => SignalRConnection.ConnectionId;

        /// <summary>
        /// The connection to the MediaServer
        /// </summary>
        public TcpConnection Connection { get; } = null;

        /// <summary>
        /// Creates a new MediaServer, generating a new UUID for it
        /// </summary>
        /// <param name="connection">The connection object representing the connection to the media server</param>
        /// <param name="name">The name of the media server</param>
        public MediaServer(TcpConnection connection)
        {
            UUID = Uuid.GetUuid();
            Connection = connection;
        }

        /// <summary>
        /// Initiates the media server, fetching the name and files
        /// </summary>
        /// <returns>A Task representing the operation</returns>
        public async Task Init()
        {
            ConLog.Log("WebMedia Link", "Initiating " + Name, LogType.Info);

            //Get the name and files on the media server
            GetInfoResponse response = await Connection.SendAsync<GetInfoResponse>(new GetInfoRequest());

            //Set the name, password, and server image
            Name = response.Name;
            Password = response.Password;
            ImageUrl = response.ImageUrl;

            //Set the files
            foreach(PlayableFile file in response.MediaUrls)
            {
                ServerFile fileToAdd = new ServerFile(file.VideoUrl, file.AudioUrl, file.Title, file.Subtitles, file.Duration, file.IsAvailable, FileType.Offline);
                Files.Add(fileToAdd.UUID, fileToAdd);
            }

            //Set the fonts
            SubtitleFonts = response.SubtitleFonts;

            ResetPlayback();

            //Add the media item ended event handler
            PositionTracker.ThresholdReached += async (sender, e) => await PositionTracker_ThresholdReached(sender, e);

            //Add the packet handlers
            Connection.RegisterRawDataHandler("AvailableFilesUpdated", async (rawData, connection) => await AvailableFilesUpdated(rawData, connection));

            ConLog.Log("WebMedia Link", "Starting SignalR", LogType.Info);

            //Start SignalR
            SignalRConnection = new HubConnectionBuilder()
                .WithAutomaticReconnect()
                .WithUrl("http://" + Server.WebServerIp + ":" + Server.WebServerPort + "/serverHub")
                .Build();

            //Create event handlers
            HubConnectionExtensions.On<string, string>(SignalRConnection, "LoginRequest", AddUser);
            HubConnectionExtensions.On<int>(SignalRConnection, "UserDisconnect", RemoveUser);
            HubConnectionExtensions.On<int, int, bool, double, UserBufferState>(SignalRConnection, "ServerUpdateUserState", UpdateUserState);
            HubConnectionExtensions.On<PlaybackStateUpdateType, string>(SignalRConnection, "ServerUpdatePlaybackState", UpdatePlaybackState);
            HubConnectionExtensions.On<int[], bool>(SignalRConnection, "ServerModifyQueue", ModifyQueue);
            HubConnectionExtensions.On<string>(SignalRConnection, "ServerDownloadMedia", DownloadMedia);

            await SignalRConnection.StartAsync();

            ConLog.Log("WebMedia Link", "Initiated " + Name, LogType.Ok);

            //Debug: Request download
            //Connection.SendRawData("Download", System.Text.Encoding.Unicode.GetBytes("https://www.youtube.com/watch?v=Cytf5Ncr9bQ"));
            //Connection.SendRawData("Download", System.Text.Encoding.Unicode.GetBytes("https://soundcloud.com/monstercat/pegboard-nerds-tristam-razor"));
            //Connection.SendRawData("Download", System.Text.Encoding.Unicode.GetBytes("https://timhaywood.bandcamp.com/track/bmc-street-party"));
        }

        /// <summary>
        /// Close all SignalR connections and media server connection
        /// </summary>
        /// <param name="closeMediaServerConnection">Whether to close the connection to the media server</param>
        /// <returns>A Task representing the operation</returns>
        public async Task Close(bool closeMediaServerConnection)
        {
            //Close the media server connection if it's alive
            if (closeMediaServerConnection)
            {
                Connection.Close(Network.Enums.CloseReason.ClientClosed);
            }

            //Close all signalR clients
            await SignalRConnection.InvokeAsync("ServerClosed", UUID);
        }

        /// <summary>
        /// Called when the available files are updated
        /// </summary>
        /// <param name="rawData">The data sent. Should be nothing</param>
        /// <param name="connection">The connection that sent it</param>
        /// <returns>A Task representing the operation</returns>
        private async Task AvailableFilesUpdated(Network.Packets.RawData rawData, Connection connection)
        {
            //Check that it's TCP
            if (connection.GetType() != typeof(TcpConnection)) return;

            await RequestForUpdatedFiles();
        }

        /// <summary>
        /// Fetches a list of files from the media server
        /// </summary>
        /// <returns>A Task representing the operation</returns>
        public async Task RequestForUpdatedFiles()
        {
            ConLog.Log("WebMedia Link", "Updating available files from " + Name, LogType.Info);

            //Send the request
            GetInfoResponse response = await Connection.SendAsync<GetInfoResponse>(new GetInfoRequest());

            //Set the fonts
            SubtitleFonts = response.SubtitleFonts;

            //Parse response
            List<ServerFile> filesToAdd = new List<ServerFile>();
            List<ServerFile> filesToRemove = new List<ServerFile>();

            //Store the old current UUID
            var oldCurrentMedia = CurrentMediaUuid;

            //Add the files in the response to the filesToAdd
            foreach (PlayableFile file in response.MediaUrls)
            {
                filesToAdd.Add(new ServerFile(file.VideoUrl, file.AudioUrl, file.Title, file.Subtitles, file.Duration, file.IsAvailable, file.Type));
            }

            //Iterate over our current 
            foreach(ServerFile file in Files.Values)
            {
                ServerFile outputFile = null;
                foreach (ServerFile newFile in filesToAdd)
                {
                    if ((file.VideoUrl == newFile.VideoUrl && !string.IsNullOrEmpty(file.VideoUrl)) || (file.AudioUrl == newFile.AudioUrl && !string.IsNullOrEmpty(file.AudioUrl)))
                    {
                        //This one is a match!
                        outputFile = newFile;
                        break;
                    }
                }

                //File was found. Update the current one's values, and remove from the filesToAdd list
                if (outputFile != null)
                {
                    file.Title = outputFile.Title;
                    file.IsAvailable = outputFile.IsAvailable;
                    filesToAdd.Remove(outputFile);
                }
                //File was not found. Remove it
                else
                {
                    filesToRemove.Add(file);
                }
            }

            //Add the files to add
            foreach(ServerFile fileToAdd in filesToAdd)
            {
                Files.Add(fileToAdd.UUID, fileToAdd);

                //If this is a new downloaded file, add it to the queue as well
                if (fileToAdd.Type == FileType.Downloaded)
                {
                    Queue.Add(fileToAdd.UUID);
                }
            }
            //Remove the files to remove
            foreach(ServerFile fileToRemove in filesToRemove)
            {
                Files.Remove(fileToRemove.UUID);
            }

            //Send files update
            await SendFilesUpdate();

            //Update the queue and send a queue update
            RemoveMissingQueueItems();
            await SendQueueUpdate();

            //If the current song was changed, send a current state update
            if (oldCurrentMedia != CurrentMediaUuid)
            {
                ResetPlayback();
                FakeState = FakingState.NotFaking;
                await UpdateBufferStateAndSendStateUpdate();
            }

            ConLog.Log("WebMedia link", "Updated available files on " + Name, LogType.Ok);
        }

        /// <summary>
        /// Start or stop the position tracker depending on the value of ActualIsPlaying
        /// </summary>
        private void UpdatePositionTracker()
        {
            //Check if we should be stopped
            if (PositionTracker.IsRunning && !ActualIsPlaying)
            {
                PositionTracker.Stop();
                LastSeekPosition = PositionTracker.Elapsed.TotalSeconds;
            }

            //Check if we should be going
            if (!PositionTracker.IsRunning && ActualIsPlaying)
            {
                PositionTracker.Start();
            }
        }

        /// <summary>
        /// Reset the playback of the current media item
        /// </summary>
        private void ResetPlayback()
        {
            LastSeekPosition = 0;
            PositionTracker.Reset();
            if (CurrentMediaUuid == int.MaxValue)
            {
                PositionTracker.SetThresholdTime(-1);
                IsPlaying = false;
            }
            else
            {
                PositionTracker.SetThresholdTime(Files[Queue[0]].Duration);
            }
        }

        /// <summary>
        /// Adds a SignalR user
        /// </summary>
        /// <param name="connectionId">The SignalR connection ID of the user</param>
        /// <param name="username">The new user's username</param>
        /// <returns>A Task representing the operation</returns>
        public async Task AddUser(string connectionId, string username)
        {
            User newUser = new User(connectionId, UUID, username);

            //Add to dictionaries
            Users.Add(newUser.UserUuid, newUser);
            SignalRConnectionToUser.Add(connectionId, newUser.UserUuid);

            ConLog.Log("SignalR", "Media server " + Name + " added user " + username, LogType.Ok);

            //Send SignalR response to accept the user
            await SignalRConnection.SendAsync("ServerLoginAccept", UUID, connectionId);

            //Send user update packet
            FakeState = FakingState.NotFaking;
            await UpdateBufferStateAndSendStateUpdate();
            await SendQueueUpdate();
            await SendFilesUpdate();
        }

        /// <summary>
        /// Remove a user with a given UUID
        /// </summary>
        /// <param name="uuid">The UUID of the user to remove</param>
        /// <returns>A Task representing the operation</returns>
        public async Task RemoveUser(int uuid)
        {
            User deadUser = Users[uuid];

            //Remove from dictionaries
            Users.Remove(uuid);
            SignalRConnectionToUser.Remove(deadUser.ConnectionId);

            //Send user update packet
            FakeState = FakingState.NotFaking;
            await UpdateBufferStateAndSendStateUpdate();
        }

        /// <summary>
        /// Update a user's state
        /// </summary>
        /// <param name="userUuid">The UUID of the user to update</param>
        /// <param name="currentMediaUuid">The current media playing on the user</param>
        /// <param name="isPlaying">True if the current media is playing</param>
        /// <param name="lastSeekPosition">The user's last seek position</param>
        /// <param name="bufferState">The user's buffer state</param>
        /// <returns>A Task representing the operation</returns>
        public async Task UpdateUserState(int userUuid, int currentMediaUuid, bool isPlaying, double lastSeekPosition, UserBufferState bufferState)
        {
            User user = Users[userUuid];

            //Update the buffer state
            user.BufferState = bufferState;

            //Update this user's current playback state
            user.CurrentMediaUuid = currentMediaUuid;
            user.IsPlaying = isPlaying;
            user.LastSeekPosition = lastSeekPosition;
            
            //Send update message to everyone
            await UpdateBufferStateAndSendStateUpdate();
        }

        /// <summary>
        /// Update the playback state
        /// </summary>
        /// <param name="type">The thing to change</param>
        /// <param name="operand">More information dependant on what it is we're changing</param>
        /// <returns>A Task representing the operation</returns>
        private async Task UpdatePlaybackState(PlaybackStateUpdateType type, string operand)
        {
            switch (type)
            {
                case PlaybackStateUpdateType.PlayPause:
                    //Is it play or pause
                    if (operand == "play")
                    {
                        IsPlaying = true;
                    }
                    else if (operand == "pause")
                    {
                        IsPlaying = false;
                    }
                    else return;
                    break;

                case PlaybackStateUpdateType.Seek:
                    //Get the position to seek to
                    int position = 0;
                    if (!int.TryParse(operand, out position)) return;

                    //Seek to that position
                    PositionTracker.Elapsed = new TimeSpan(0, 0, position);
                    LastSeekPosition = PositionTracker.Elapsed.TotalSeconds;
                    break;

                default:
                    return;
            }

            //Send out an update
            if (CurrentMediaUuid == int.MaxValue) IsPlaying = false;
            FakeState = FakingState.NotFaking;
            await UpdateBufferStateAndSendStateUpdate();
        }

        /// <summary>
        /// Sends a user update packet to all SignalR clients
        /// </summary>
        /// <returns>A Task representing the operation</returns>
        private async Task SendCurrentStateUpdate()
        {
            SignalR.Server.StateUpdated info = new SignalR.Server.StateUpdated();

            //Set information
            info.CurrentMediaUuid = CurrentMediaUuid;
            info.IsPlaying = ActualIsPlaying;
            info.LastSeekPosition = LastSeekPosition;
            info.IsBuffering = FakeState == FakingState.Faking;

            if (CurrentMediaUuid != int.MaxValue)
            {
                ServerFile currentFile = Files[Queue[0]];
                info.CurrentVideoPath = currentFile.VideoUrl;
                info.CurrentAudioPath = currentFile.AudioUrl;
                info.MediaTitle = currentFile.Title;
                info.Duration = currentFile.Duration;

                //Add subtitles
                info.Subtitles = new SignalR.Server.Subtitle[currentFile.Subtitles.Length];
                for (int i = 0; i < info.Subtitles.Length; i++)
                {
                    info.Subtitles[i] = new SignalR.Server.Subtitle
                    {
                        Url = currentFile.Subtitles[i].Url,
                        Name = "[" + currentFile.Subtitles[i].Language + "] " + currentFile.Subtitles[i].Name
                    };
                }
            }

            info.SubtitleFonts = SubtitleFonts;

            //Set users
            info.Users = new SignalR.Server.User[Users.Count];

            Dictionary<int, User>.ValueCollection.Enumerator enumerator = Users.Values.GetEnumerator();
            for (int i = 0; i < Users.Count; i++)
            {
                enumerator.MoveNext();
                User thisUser = enumerator.Current;
                info.Users[i] = new SignalR.Server.User { Uuid = thisUser.UserUuid, Username = thisUser.Username, BufferState = thisUser.BufferState };
            }

            string jsonToSend = JsonConvert.SerializeObject(info);

            await SignalRConnection.SendAsync("ServerUpdateCurrentState", UUID, jsonToSend);
        }

        /// <summary>
        /// Updates everyone's buffer state based on whether their current state is correct, and sends a current state update to everyone
        /// </summary>
        /// <returns>A Task representing the operation</returns>
        private async Task UpdateBufferStateAndSendStateUpdate()
        {
            bool fakeStateChanged = true;

            while (fakeStateChanged)
            {
                fakeStateChanged = false;

                //If we're not faking
                if (FakeState == FakingState.NotFaking)
                {
                    //Move on to faking if somebody is not correct
                    bool isEveryoneCorrect = true;
                    foreach(User user in Users.Values)
                    {
                        if (user.CurrentMediaUuid != CurrentMediaUuid || user.IsPlaying != IsPlaying || user.LastSeekPosition != LastSeekPosition || user.BufferState != UserBufferState.Ready)
                        {
                            isEveryoneCorrect = false;
                        }
                    }

                    //If we should fake, set fake state to faking and loop again
                    if (!isEveryoneCorrect)
                    {
                        FakeState = FakingState.Faking;
                        fakeStateChanged = true;
                    }
                }
                //If we're currently faking
                else if (FakeState == FakingState.Faking)
                {
                    //Update the position tracker
                    UpdatePositionTracker();

                    //Move on to post faking if everyone is correct
                    bool isEveryoneCorrect = true;
                    foreach (User user in Users.Values)
                    {
                        if (user.CurrentMediaUuid != CurrentMediaUuid || user.IsPlaying != false || user.LastSeekPosition != LastSeekPosition || user.BufferState != UserBufferState.Ready)
                        {
                            isEveryoneCorrect = false;
                        }
                    }

                    //If we should be in post faking, set fake state to post faking and loop again
                    if (isEveryoneCorrect)
                    {
                        FakeState = FakingState.PostFaking;
                        fakeStateChanged = true;
                    }
                }
                //If we've just been faking
                else if (FakeState == FakingState.PostFaking)
                {
                    //Update the position tracker
                    UpdatePositionTracker();

                    //Move on to not faking if everyone is correct
                    //Move back to faking if somebody has something other than the isplaying incorrect
                    bool isEveryoneCorrect = true;
                    bool isSomethingBadlyWrong = false;
                    foreach (User user in Users.Values)
                    {
                        if (user.CurrentMediaUuid != CurrentMediaUuid || user.IsPlaying != IsPlaying || user.LastSeekPosition != LastSeekPosition || user.BufferState != UserBufferState.Ready)
                        {
                            isEveryoneCorrect = false;
                            if (user.CurrentMediaUuid != CurrentMediaUuid || user.LastSeekPosition != LastSeekPosition || user.BufferState != UserBufferState.Ready)
                            {
                                isSomethingBadlyWrong = true;
                            }
                        }
                    }

                    //If we should be in not faking, set fake state to not faking and loop again
                    if (isEveryoneCorrect)
                    {
                        FakeState = FakingState.NotFaking;
                        fakeStateChanged = true;
                    }

                    //If we should be in faking, set fake state to faking and loop again
                    if (isSomethingBadlyWrong)
                    {
                        FakeState = FakingState.Faking;
                        fakeStateChanged = true;
                    }
                }
            }

            //Send a current state update to everyone
            await SendCurrentStateUpdate();
        }

        /// <summary>
        /// Remove items from the queue that are no longer in the library of files
        /// </summary>
        /// <returns>True if something was removed</returns>
        private bool RemoveMissingQueueItems()
        {
            bool hasAnythingChanged = false;

            int queuePointer = 0;
            while (queuePointer < Queue.Count)
            {
                if (!Files.ContainsKey(Queue[queuePointer]))
                {
                    hasAnythingChanged = true;
                    Queue.RemoveAt(queuePointer);
                }
                else
                {
                    queuePointer++;
                }
            }

            return hasAnythingChanged;
        }

        /// <summary>
        /// Send a queue update to all signalr clients
        /// </summary>
        /// <returns>A Task representing the operation</returns>
        private async Task SendQueueUpdate()
        {
            SignalR.Server.QueueUpdated info = new SignalR.Server.QueueUpdated();

            //Only add to the queue if it is not empty
            if (Queue.Count != 0)
            {
                //If the first item is not available, we include it in the queue
                if (!Files[Queue[0]].IsAvailable)
                {
                    info.QueueItems = new SignalR.Server.MediaItem[Queue.Count];
                    for (int i = 0; i < Queue.Count; i++)
                    {
                        ServerFile file = Files[Queue[i]];

                        info.QueueItems[i] = new SignalR.Server.MediaItem { Uuid = file.UUID, HasVideo = file.VideoUrl != "", Title = file.Title, Duration = (int)Math.Floor(file.Duration), IsAvailable = file.IsAvailable, IsStored = file.Type == FileType.Offline };
                    }
                }
                else
                {
                    info.QueueItems = new SignalR.Server.MediaItem[Queue.Count - 1];
                    for (int i = 1; i < Queue.Count; i++)
                    {
                        ServerFile file = Files[Queue[i]];

                        info.QueueItems[i - 1] = new SignalR.Server.MediaItem { Uuid = file.UUID, HasVideo = file.VideoUrl != "", Title = file.Title, Duration = (int)Math.Floor(file.Duration), IsAvailable = file.IsAvailable, IsStored = file.Type == FileType.Offline };
                    }
                }
            }
            else
            {
                info.QueueItems = new SignalR.Server.MediaItem[0];
            }

            string jsonString = JsonConvert.SerializeObject(info);

            await SignalRConnection.InvokeAsync("ServerUpdateQueue", UUID, jsonString);
        }

        /// <summary>
        /// Called when a SignalR user changes the queue
        /// </summary>
        /// <param name="newQueue">The new queue provided by the SignalR user</param>
        /// <param name="shouldResetPlayback">Whether playback should be reset</param>
        /// <returns>A Task representing the operation</returns>
        private async Task ModifyQueue(int[] newQueue, bool shouldResetPlayback)
        {
            int oldCurrentMedia = CurrentMediaUuid;
            Queue = newQueue.ToList();
            RemoveMissingQueueItems();

            //If the current item has changed, reset the playback things and send a current state update
            if (shouldResetPlayback)
            {
                ResetPlayback();
                FakeState = FakingState.NotFaking;
                await UpdateBufferStateAndSendStateUpdate();
            }

            //Send a queue update
            await SendQueueUpdate();
        }
         

        private async Task PositionTracker_ThresholdReached(object sender, EventArgs e)
        {
            Console.WriteLine("threshold reached");
            //Remove from the queue
            Queue.RemoveAt(0);

            ResetPlayback();
            FakeState = FakingState.NotFaking;
            await UpdateBufferStateAndSendStateUpdate();

            await SendQueueUpdate();
        }

        /// <summary>
        /// Send a files update to all SignalR clients
        /// </summary>
        /// <returns>A Task representing the operation</returns>
        private async Task SendFilesUpdate()
        {
            //Only send the stored files
            SignalR.Server.FilesUpdated info = new SignalR.Server.FilesUpdated();

            List<SignalR.Server.MediaItem> filesToSend = new List<SignalR.Server.MediaItem>();

            foreach(ServerFile file in Files.Values)
            {
                if (file.Type == FileType.Offline)
                {
                    filesToSend.Add(new SignalR.Server.MediaItem { Uuid = file.UUID, HasVideo = file.VideoUrl != "", Title = file.Title, Duration = (int)Math.Floor(file.Duration), IsAvailable = file.IsAvailable, IsStored = true });
                }
            }

            info.OfflineItems = filesToSend.ToArray();

            string jsonString = JsonConvert.SerializeObject(info);
            await SignalRConnection.InvokeAsync("ServerUpdateFiles", UUID, jsonString);
        }

        /// <summary>
        /// Send a download media request to the media server
        /// </summary>
        /// <param name="urlToDownload">The URL to download</param>
        /// <returns>A Task representing the operation</returns>
        private async Task DownloadMedia(string urlToDownload)
        {
            Connection.SendRawData("Download", System.Text.Encoding.Unicode.GetBytes(urlToDownload));
        }
    }
}
