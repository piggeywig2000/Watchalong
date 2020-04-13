using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Network;
using Watchalong.Utils;
using WebMediaLink;
using MediaInfo;

namespace MediaSever
{
    class WebMediaClient
    {
        /// <summary>
        /// The name of the webserver
        /// </summary>
        private string Name { get; }

        /// <summary>
        /// The password of the webserver
        /// </summary>
        private string Password { get; }

        /// <summary>
        /// The IP address of the webserver
        /// </summary>
        private string WebserverIp { get; set; }

        /// <summary>
        /// The Port address of the webserver
        /// </summary>
        private ushort WebserverPort { get; set; }

        /// <summary>
        /// The IP address of the mediaserver
        /// </summary>
        private string MediaserverIp { get; set; }

        /// <summary>
        /// The Port address of the mediaserver
        /// </summary>
        private ushort MediaserverPort { get; set; }

        /// <summary>
        /// The path to the folder containing the media files
        /// </summary>
        private string MediaFilesPath { get; }

        /// <summary>
        /// The path to the folder containing the download files
        /// </summary>
        private string DownloadFilesPath { get; }

        /// <summary>
        /// The connection to the webserver
        /// </summary>
        private TcpConnection WebServer { get; set; } = null;

        /// <summary>
        /// The media server
        /// </summary>
        private HttpServer MediaServer { get; set; } = null;

        /// <summary>
        /// A list of all the available files
        /// </summary>
        private List<PlayableFile> Files { get; set; } = new List<PlayableFile>();

        /// <summary>
        /// Provides methods for downloading media
        /// </summary>
        private YoutubeDL Downloader { get; } = null;

        /// <summary>
        /// The source of the cancellation token
        /// </summary>
        private CancellationTokenSource CancelTokenSource { get; set; } = new CancellationTokenSource();

        /// <summary>
        /// A cancellation token that is called when the server disconnects
        /// </summary>
        public CancellationToken CancelToken { get { return CancelTokenSource.Token; } }

        /// <summary>
        /// Creates a new webmedia client with the given IP, Port, and HTTP server
        /// </summary>
        /// <param name="ip">The IP of the webmedia link server</param>
        /// <param name="port">The port of the webmedia link server</param>
        /// <param name="httpIp">The IP to host the HTTP server from</param>
        /// <param name="httpPort">The port to host the HTTP server on</param>
        /// <param name="name">The name of the media server</param>
        /// <param name="password">The password of the media server</param>
        /// <param name="pathToMediaFiles">The location of the folder containing the media files</param>
        /// <param name="pathToDownloadFiles">The location of the folder containing the download files</param>
        public WebMediaClient(string ip, ushort port, string httpIp, ushort httpPort, string name, string password, string pathToMediaFiles, string pathToDownloadFiles)
        {
            WebserverIp = ip;
            WebserverPort = port;
            MediaserverIp = httpIp;
            MediaserverPort = httpPort;
            Name = name;
            Password = password;
            MediaFilesPath = pathToMediaFiles;
            DownloadFilesPath = pathToDownloadFiles;

            //Use the .exe if windows, use the . nothing otherwise
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                Downloader = new YoutubeDL(Helpful.GetExecutingDirectory() + "\\youtubedl.exe", CancelToken);
            }
            else
            {
                Downloader = new YoutubeDL(Helpful.GetExecutingDirectory() + "\\youtubedl", CancelToken);
            }

            //Create the media server
            MediaServer = new HttpServer(httpIp, httpPort, pathToMediaFiles, pathToDownloadFiles, CancelTokenSource.Token);

            //Populate the media server's list of available files
            RescanForFiles();

            //Try to start media server. If it fails, stop the program
            if (!MediaServer.StartServer())
            {
                CancelTokenSource.Cancel();
                ConLog.Log("HTTP Server", "Failed to start HTTP server. Try running as an administrator, and check that your firewall or antivirus isn't preventing this program from starting the HTTP server", LogType.Fatal);
            }

            //Register file system watchers
            FileSystemWatcher mediaWatcher = new FileSystemWatcher(pathToMediaFiles, "*.*");
            mediaWatcher.EnableRaisingEvents = true;
            mediaWatcher.IncludeSubdirectories = false;
            mediaWatcher.Changed += (sender, e) => UpdateAvailableFiles();
            mediaWatcher.Created += (sender, e) => UpdateAvailableFiles();
            mediaWatcher.Deleted += (sender, e) => UpdateAvailableFiles();
            mediaWatcher.Renamed += (sender, e) => UpdateAvailableFiles();
            FileSystemWatcher downloadWatcher = new FileSystemWatcher(pathToDownloadFiles, "*.*");
            downloadWatcher.EnableRaisingEvents = true;
            downloadWatcher.IncludeSubdirectories = false;
            downloadWatcher.Changed += (sender, e) => UpdateAvailableFiles();
            downloadWatcher.Created += (sender, e) => UpdateAvailableFiles();
            downloadWatcher.Deleted += (sender, e) => UpdateAvailableFiles();
            downloadWatcher.Renamed += (sender, e) => UpdateAvailableFiles();
        }

        private void UpdateAvailableFiles()
        {
            //Repopulate the list of available files
            if (RescanForFiles())
            {
                //Send updated message if available files has changed
                WebServer.SendRawData("AvailableFilesUpdated", new byte[0]);
            }
        }

        /// <summary>
        /// Connects to the webserver
        /// </summary>
        /// <returns>A Task representing the process</returns>
        public async Task ConnectToWebserver()
        {
            //If the connection is alive, close it
            if (WebServer != null)
                if (WebServer.IsAlive)
                    WebServer.Close(Network.Enums.CloseReason.ServerClosed, false);

            //Initiate youtubedl
            await Downloader.Init();

            //Connect to webserver
            ConLog.Log("WebMedia Link", "Connecting to WebMediaLink server", LogType.Info);
            Tuple<TcpConnection, ConnectionResult> result = await ConnectionFactory.CreateTcpConnectionAsync(WebserverIp, WebserverPort);
            
            //Check if connection worked
            if (result.Item2 != ConnectionResult.Connected) 
            {
                ConLog.Log("WebMedia Link", "Failed to connect to WebMediaLink server", LogType.Error);
                await Disconnected(Network.Enums.CloseReason.Timeout, result.Item1);
            }

            WebServer = result.Item1;
            ConLog.Log("WebMedia Link", "Connected to WebMediaLink server", LogType.Ok);

            //Register cancel token triggered
            CancelToken.Register(() =>
            {
                if (WebServer != null)
                    if (WebServer.IsAlive)
                        WebServer.Close(Network.Enums.CloseReason.ServerClosed, false);
            });

            //Register connection lost handler
            WebServer.ConnectionClosed += async (reason, connection) => await Disconnected(reason, connection);

            //Register packet handlers
            WebServer.RegisterPacketHandler<GetInfoRequest>(GetInfoRequestReceived, this);
            WebServer.RegisterRawDataHandler("Download", async (rawData, connection) => await DownloadFromRequest(rawData, connection));
        }

        private async Task Disconnected(Network.Enums.CloseReason reason, Connection connection)
        {
            //Stop program
            CancelTokenSource.Cancel();
            ConLog.Log("WebMedia Link", "Disconnected from WebMediaLink server", LogType.Fatal);
        }

        private void GetInfoRequestReceived(GetInfoRequest packet, Connection connection)
        {
            string imageUrl = "";
            if (MediaServer.HasImage)
            {
                imageUrl = "http://" + MediaserverIp + ":" + MediaserverPort + "/server-image.png";
            }

            connection.Send(new GetInfoResponse(Name, Password, imageUrl, Files.ToArray(), packet));
        }

        /// <summary>
        /// Rescans the media file directory and updates the files available for download
        /// </summary>
        private bool RescanForFiles()
        {
            ConLog.Log("HTTP Server", "Rescanning media and download folders for available files", LogType.Info);

            bool hasAnythingChanged = false;

            string[] filesFound = Directory.GetFiles(MediaFilesPath, "*.*", SearchOption.TopDirectoryOnly);

            //Repopulate playable file array
            List<PlayableFile> newMediaFiles = new List<PlayableFile>();

            //Add back in all the downloads that are still there
            foreach(PlayableFile file in Files)
            {
                if (file.Type == FileType.Downloaded)
                {
                    if (File.Exists(DownloadFilesPath + "\\" + Path.GetFileName(file.VideoUrl)) || File.Exists(DownloadFilesPath + "\\" + Path.GetFileName(file.AudioUrl)))
                    {
                        newMediaFiles.Add(new PlayableFile(file.VideoUrl, file.AudioUrl, file.Title, file.Duration, true, FileType.Downloaded));
                        if (!file.IsAvailable)
                        {
                            hasAnythingChanged = true;
                            file.IsAvailable = true;
                        }
                    }
                    else if (!file.IsAvailable)
                    {
                        newMediaFiles.Add(file);
                    }
                    else
                    {
                        hasAnythingChanged = true;
                        ConLog.Log("HTTP Server", "Download titled " + file.Title + " was removed because the file was deleted", LogType.Warning);
                    }
                }
            }

            //Add the media
            foreach(string file in filesFound)
            {
                //Check if it's audio or video
                MediaInfoWrapper wrapper = new MediaInfoWrapper(file);

                //Check that we have media
                if (wrapper.AudioStreams.Count == 0 && wrapper.VideoStreams.Count == 0)
                {
                    continue;
                }

                string fileName = Path.GetFileName(file);

                //We have media. Check if it's audio or video
                if (wrapper.HasVideo)
                {
                    newMediaFiles.Add(new PlayableFile("http://" + MediaserverIp + ":" + MediaserverPort + "/media/" + fileName, "", fileName, wrapper.Duration / 1000.0, true, FileType.Offline));
                }
                else
                {
                    newMediaFiles.Add(new PlayableFile("", "http://" + MediaserverIp + ":" + MediaserverPort + "/media/" + fileName, fileName, wrapper.Duration / 1000.0, true, FileType.Offline));
                }
            }

            //Check if this differs from the old one
            foreach(PlayableFile file in newMediaFiles)
            {
                bool matchFound = false;

                foreach(PlayableFile possibleMatch in Files)
                {
                    if (file.VideoUrl == possibleMatch.VideoUrl && 
                        file.AudioUrl == possibleMatch.AudioUrl && 
                        file.Title == possibleMatch.Title && 
                        file.Duration == possibleMatch.Duration && 
                        file.IsAvailable == possibleMatch.IsAvailable)
                    {
                        matchFound = true;
                    }
                }

                //If we didn't find a match, something has changed
                if (!matchFound)
                {
                    hasAnythingChanged = true;
                }
            }
            if (newMediaFiles.Count != Files.Count) hasAnythingChanged = true;

            Files = newMediaFiles;

            //Set the HTTP server's arrays
            SetHttpServerFiles();

            if (hasAnythingChanged)
            {
                ConLog.Log("HTTP Server", "Scan complete and the available files has changed", LogType.Ok);
            }
            else
            {
                ConLog.Log("HTTP Server", "Scan complete and the available files has not changed", LogType.Ok);
            }

            return hasAnythingChanged;
        }

        /// <summary>
        /// Sets the HTTP server's available files based on the available files
        /// </summary>
        private void SetHttpServerFiles()
        {
            List<string> mediaFiles = new List<string>();
            List<string> downloadFiles = new List<string>();

            foreach(PlayableFile file in Files)
            {
                if (!file.IsAvailable) continue;

                string videoFile = Path.GetFileName(file.VideoUrl);
                string audioFile = Path.GetFileName(file.AudioUrl);

                if (file.Type == FileType.Offline)
                {
                    if (!string.IsNullOrEmpty(videoFile)) mediaFiles.Add(videoFile);
                    if (!string.IsNullOrEmpty(audioFile)) mediaFiles.Add(audioFile);
                }
                else if (file.Type == FileType.Downloaded)
                {
                    if (!string.IsNullOrEmpty(videoFile)) downloadFiles.Add(videoFile);
                    if (!string.IsNullOrEmpty(audioFile)) downloadFiles.Add(audioFile);
                }
            }

            MediaServer.AvailableMediaFiles = mediaFiles.ToArray();
            MediaServer.AvailableDownloadFiles = downloadFiles.ToArray();
        }

        /// <summary>
        /// Handle a download request
        /// </summary>
        /// <param name="rawData">The raw data sent</param>
        /// <param name="connection">The connection that sent the request</param>
        /// <returns>A Task representing the download</returns>
        private async Task DownloadFromRequest(Network.Packets.RawData rawData, Connection connection)
        {
            string url = Encoding.Unicode.GetString(rawData.Data);
            ConLog.Log("WebMedia Link", "Received request to download " + url, LogType.Info);

            if (!await DownloadUrl(url))
            {
                ConLog.Log("WebMedia Link", url + " failed", LogType.Warning);
            }
            else
            {
                ConLog.Log("WebMedia Link", url + " completed", LogType.Info);
            }
        }

        /// <summary>
        /// Downloads a file from a URL
        /// </summary>
        /// <param name="url">The URL to download from</param>
        /// <returns>True if it was successful, false if not</returns>
        private async Task<bool> DownloadUrl(string url)
        {
            //Get the info
            DownloadInfo info = await Downloader.GetDownloadInfoFromUrl(url);

            //Validate
            if (info == null) return false;
            if (info.VideoCodec == null && info.AudioCodec == null) return false;
            if (info.RequestedUrls == null && info.Url == null) return false;

            //Create a new file object
            string httpVideoUrl = "";
            string httpAudioUrl = "";
            int uuid = Uuid.GetUuid();
            if (!string.IsNullOrEmpty(info.VideoCodec) && info.VideoCodec != "none")
            {
                httpVideoUrl = "http://" + MediaserverIp + ":" + MediaserverPort + "/download/" + uuid + "." + info.Extension;
            }
            else
            {
                httpAudioUrl = "http://" + MediaserverIp + ":" + MediaserverPort + "/download/" + uuid + "." + info.Extension;
            }

            PlayableFile newFile = new PlayableFile(httpVideoUrl, httpAudioUrl, info.Title, info.Duration, false, FileType.Downloaded);

            //Add it to the list of files
            Files.Add(newFile);

            //Send available files updated message
            WebServer.SendRawData("AvailableFilesUpdated", new byte[0]);

            //Download the file to the temporary folder
            bool isSuccess = await Downloader.DownloadFileFromUrl(url, DownloadFilesPath + "\\incomplete\\" + uuid + "." + info.Extension);

            //If it was a success, change the availability to true, and add to the http server
            if (isSuccess)
            {
                File.Move(DownloadFilesPath + "\\incomplete\\" + uuid + "." + info.Extension, DownloadFilesPath + "\\" + uuid + "." + info.Extension);
                //SetHttpServerFiles();
            }
            //If it wasn't a success, remove it from the list of files
            else
            {
                Files.Remove(newFile);
                newFile.IsAvailable = true;
                WebServer.SendRawData("AvailableFilesUpdated", new byte[0]);
            }

            //Send available files updated message
            //WebServer.SendRawData("AvailableFilesUpdated", new byte[0]);

            //Return whether it was success
            return isSuccess;
        }
    }
}
