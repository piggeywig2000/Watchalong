using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Diagnostics;
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
        /// The path to the folder containing the download files
        /// </summary>
        private string SubtitleFilesPath { get; }

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
        /// The subtitle fonts available
        /// </summary>
        private string[] SubtitleFonts { get; set; } = new string[0];

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
        /// <param name="pathToSubtitleFiles">The location of the folder containing the subtitle files</param>
        public WebMediaClient(string ip, ushort port, string httpIp, ushort httpPort, string name, string password, string pathToMediaFiles, string pathToDownloadFiles, string pathToSubtitleFiles)
        {
            WebserverIp = ip;
            WebserverPort = port;
            MediaserverIp = httpIp;
            MediaserverPort = httpPort;
            Name = name;
            Password = password;
            MediaFilesPath = pathToMediaFiles;
            DownloadFilesPath = pathToDownloadFiles;
            SubtitleFilesPath = pathToSubtitleFiles;

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
            MediaServer = new HttpServer(httpIp, httpPort, pathToMediaFiles, pathToDownloadFiles, pathToSubtitleFiles, CancelTokenSource.Token);

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

            connection.Send(new GetInfoResponse(Name, Password, imageUrl, Files.ToArray(), SubtitleFonts, packet));
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
                        newMediaFiles.Add(new PlayableFile(file.VideoUrl, file.AudioUrl, file.Title, file.Sha1, file.Subtitles, file.Duration, true, FileType.Downloaded));
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
                    newMediaFiles.Add(new PlayableFile("http://" + MediaserverIp + ":" + MediaserverPort + "/media/" + fileName, "", fileName, null, new SubtitleInfo[0], wrapper.Duration / 1000.0, true, FileType.Offline));
                }
                else
                {
                    newMediaFiles.Add(new PlayableFile("", "http://" + MediaserverIp + ":" + MediaserverPort + "/media/" + fileName, fileName, null, new SubtitleInfo[0], wrapper.Duration / 1000.0, true, FileType.Offline));
                }
            }

            //Check if this differs from the old one
            foreach(PlayableFile file in newMediaFiles)
            {
                PlayableFile matchFound = null;

                foreach(PlayableFile possibleMatch in Files)
                {
                    if (file.VideoUrl == possibleMatch.VideoUrl && 
                        file.AudioUrl == possibleMatch.AudioUrl && 
                        file.Title == possibleMatch.Title && 
                        file.Duration == possibleMatch.Duration && 
                        file.IsAvailable == possibleMatch.IsAvailable)
                    {
                        matchFound = possibleMatch;
                    }
                }

                //If we didn't find a match, something has changed
                if (matchFound == null)
                {
                    hasAnythingChanged = true;

                    //If it's not a download, generate a sha1 hash
                    if (file.Type == FileType.Offline)
                    {
                        //Get the file path
                        string fileName;
                        if (!string.IsNullOrEmpty(file.VideoUrl))
                            fileName = Path.GetFileName(file.VideoUrl);
                        else
                            fileName = Path.GetFileName(file.AudioUrl);

                        string filePath = MediaFilesPath + "/" + fileName;

                        file.Sha1 = GenerateSha1OfFile(filePath);
                    }
                }
                //If we did find a match, copy the sha1 hash
                else
                {
                    file.Sha1 = matchFound.Sha1;
                }
            }
            if (newMediaFiles.Count != Files.Count) hasAnythingChanged = true;

            Files = newMediaFiles;

            //Set the HTTP server's arrays
            SetHttpServerFiles();

            if (hasAnythingChanged)
            {
                ConLog.Log("HTTP Server", "Scan complete and the available files has changed.  Extracting subtitles and fonts from all media files", LogType.Ok);
                ExtractAllSubtitlesAndFontsForAllMediaFiles();
                ConLog.Log("HTTP Server", "Subtitles and fonts extracted", LogType.Ok);
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
            List<string> subtitleFiles = new List<string>();

            //Add the offline and downloaded files
            foreach (PlayableFile file in Files)
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

            //Add the fonts in the subtitle folder
            UpdateAvailableFonts();
            foreach (string file in SubtitleFonts)
            {
                subtitleFiles.Add(Path.GetFileName(file));
            }

            //Add the ass files in the subtitle folder
            string[] filesInSubtitleFolder = Directory.GetFiles(SubtitleFilesPath, "*.ass", SearchOption.TopDirectoryOnly);
            foreach (string file in filesInSubtitleFolder)
            {
                subtitleFiles.Add(Path.GetFileName(file));
            }

            MediaServer.AvailableMediaFiles = mediaFiles.ToArray();
            MediaServer.AvailableDownloadFiles = downloadFiles.ToArray();
            MediaServer.AvailableSubtitleFiles = subtitleFiles.ToArray();
        }

        /// <summary>
        /// Update the available subtitle fonts by looking through the subtitle folder for fonts
        /// </summary>
        private void UpdateAvailableFonts()
        {
            List<string> fonts = new List<string>();

            string[] filesInSubtitleFolder = Directory.GetFiles(SubtitleFilesPath, "*.*", SearchOption.TopDirectoryOnly);
            foreach (string file in filesInSubtitleFolder)
            {
                string extension = Path.GetExtension(file).ToLower();
                if (extension == ".ttf" ||
                    extension == ".otf" ||
                    extension == ".woff" ||
                    extension == ".woff2")
                {
                    fonts.Add("http://" + MediaserverIp + ":" + MediaserverPort + "/subtitle/" + Path.GetFileName(file));
                }
            }

            SubtitleFonts = fonts.ToArray();
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

            PlayableFile newFile = new PlayableFile(httpVideoUrl, httpAudioUrl, info.Title, null, new SubtitleInfo[0], info.Duration, false, FileType.Downloaded);

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

        /// <summary>
        /// Generate hashes for all files
        /// </summary>
        private void GenerateSha1ForAllFiles()
        {
            foreach (PlayableFile file in Files)
            {
                if (!file.IsAvailable) continue;

                //Get the file path
                string fileName;
                string filePath = "";
                if (!string.IsNullOrEmpty(file.VideoUrl))
                    fileName = Path.GetFileName(file.VideoUrl);
                else
                    fileName = Path.GetFileName(file.AudioUrl);

                if (file.Type == FileType.Offline)
                    filePath = MediaFilesPath + "/" + fileName;
                else if (file.Type == FileType.Downloaded)
                    filePath = DownloadFilesPath + "/" + fileName;

                file.Sha1 = GenerateSha1OfFile(filePath);
            }
        }

        /// <summary>
        /// Get the sha1 hash of a particular file
        /// </summary>
        /// <param name="path">The path to the file</param>
        /// <returns>The Sha1 hash of the file</returns>
        private string GenerateSha1OfFile(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            {
                using (SHA1Managed sha1 = new SHA1Managed())
                {
                    byte[] sha1Bytes = sha1.ComputeHash(stream);

                    StringBuilder sb = new StringBuilder();
                    foreach (byte b in sha1Bytes)
                    {
                        sb.Append(b.ToString("X2"));
                    }
                    return sb.ToString();
                }
            }
        }

        /// <summary>
        /// Extract all the fonts and subtitles from all media files
        /// </summary>
        private void ExtractAllSubtitlesAndFontsForAllMediaFiles()
        {
            //Delete and recreate incomplete folder
            if (Directory.Exists(SubtitleFilesPath + "/incomplete"))
                Directory.Delete(SubtitleFilesPath + "/incomplete", true);
            Directory.CreateDirectory(SubtitleFilesPath + "/incomplete");

            foreach (PlayableFile file in Files)
            {
                if (file.Type != FileType.Offline) continue;

                //Get the file path
                string fileName;
                if (!string.IsNullOrEmpty(file.VideoUrl))
                    fileName = Path.GetFileName(file.VideoUrl);
                else
                    fileName = Path.GetFileName(file.AudioUrl);

                string filePath = MediaFilesPath + "/" + fileName;

                //Get the subtitles
                MediaInfoWrapper wrapper = new MediaInfoWrapper(filePath);

                if (wrapper.HasSubtitles)
                {
                    //Extract fonts
                    ExtractAllFonts(filePath);
                    file.Subtitles = ExtractAllSubtitles(filePath, wrapper.Subtitles.ToList());
                }
                else
                {
                    file.Subtitles = new SubtitleInfo[0];
                }
            }

            //Remove files from main folder
            string[] oldFiles = Directory.GetFiles(SubtitleFilesPath);
            foreach (string file in oldFiles)
            {
                File.Delete(file);
            }

            //Copy incomplete folder into main folder
            string[] newFiles = Directory.GetFiles(SubtitleFilesPath + "/incomplete");
            foreach (string file in newFiles)
            {
                File.Copy(file, SubtitleFilesPath + "/" + Path.GetFileName(file));
            }

            //Delete incomplete folder
            Directory.Delete(SubtitleFilesPath + "/incomplete", true);

            //Set the HTTP server's arrays
            SetHttpServerFiles();
        }

        /// <summary>
        /// Extracts all the fonts from the media file
        /// </summary>
        /// <param name="mediaFilePath">The media file to extract fonts from</param>
        private void ExtractAllFonts(string mediaFilePath)
        {
            //Fonts: ffmpeg -y -hide_banner -loglevel panic -dump_attachment:t "" -i "D:\My Files\Documents\GitHub\Watchalong\Watchalong\MediaSever\bin\Debug\netcoreapp3.1\test\Place to Place - S01E01 - Here ⇔ There_S01E01.mkv"
            //Run in subtitle folder
            Process extractProcess = new Process();
            extractProcess.StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-n -hide_banner -loglevel panic -dump_attachment:t \"\" -i \"" + mediaFilePath + "\"",
                WorkingDirectory = SubtitleFilesPath + "/incomplete"
            };
            extractProcess.Start();
            extractProcess.WaitForExit();
        }

        private int currentSubtitleUuid = 0;
        /// <summary>
        /// Get the subtitles from a media file
        /// </summary>
        /// <param name="mediaFilePath">The path to the media file</param>
        /// <param name="subtitles">The subtitles information</param>
        /// <returns>An array of subtitle information represeting all the subtitles that were extracted</returns>
        private SubtitleInfo[] ExtractAllSubtitles(string mediaFilePath, List<MediaInfo.Model.SubtitleStream> subtitles)
        {
            List<SubtitleInfo> returnValue = new List<SubtitleInfo>();

            foreach (MediaInfo.Model.SubtitleStream stream in subtitles)
            {
                if (stream.Codec == MediaInfo.Model.SubtitleCodec.Ass ||
                    stream.Codec == MediaInfo.Model.SubtitleCodec.TextAss ||
                    stream.Codec == MediaInfo.Model.SubtitleCodec.Ssa ||
                    stream.Codec == MediaInfo.Model.SubtitleCodec.TextSsa)
                {
                    //Set metadata
                    SubtitleInfo valueToAdd = new SubtitleInfo();
                    valueToAdd.Name = stream.Name;
                    valueToAdd.Language = stream.Language;
                    valueToAdd.CodecId = (int)stream.Codec;

                    //Set url
                    valueToAdd.Url = "http://" + MediaserverIp + ":" + MediaserverPort + "/subtitle/" + currentSubtitleUuid + ".ass";

                    //Extract using ffmpeg
                    Process extractProcess = new Process();
                    extractProcess.StartInfo = new ProcessStartInfo()
                    {
                        FileName = "ffmpeg",
                        Arguments = "-y -hide_banner -loglevel panic -i \"" + mediaFilePath + "\" -map 0:" + stream.StreamNumber + " \"" + SubtitleFilesPath + "/incomplete/" + currentSubtitleUuid.ToString() + ".ass" + "\"",
                        WorkingDirectory = SubtitleFilesPath
                    };
                    extractProcess.Start();
                    extractProcess.WaitForExit();

                    //Increase uuid
                    currentSubtitleUuid += 1;

                    //Add to list
                    returnValue.Add(valueToAdd);
                }
            }

            return returnValue.ToArray();
        }
    }
}
