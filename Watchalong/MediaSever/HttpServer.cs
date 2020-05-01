using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Watchalong.Utils;

namespace MediaSever
{
    class HttpServer
    {
        //2mb
        private const int BUFFER_SIZE = 2097152;
        /// <summary>
        /// The IP address of the http server
        /// </summary>
        private string Ip { get; }

        /// <summary>
        /// The Port address of the http server
        /// </summary>
        private ushort Port { get; }

        /// <summary>
        /// The path to the folder containing the media files
        /// </summary>
        private string MediaFilesPath { get; }

        /// <summary>
        /// The path to the folder containing the download files
        /// </summary>
        private string DownloadFilesPath { get; }

        /// <summary>
        /// The path to the folder containing the subtitle files
        /// </summary>
        private string SubtitleFilesPath { get; }

        /// <summary>
        /// The http server object
        /// </summary>
        private HttpListener Server { get; set; }

        /// <summary>
        /// Stops the HTTP server when triggered
        /// </summary>
        private CancellationToken CancelToken { get; }

        /// <summary>
        /// The file names of the offline media files
        /// </summary>
        public string[] AvailableMediaFiles { get; set; } = new string[0];

        /// <summary>
        /// The file names of the downloaded media files
        /// </summary>
        public string[] AvailableDownloadFiles { get; set; } = new string[0];

        /// <summary>
        /// The file names of the downloaded media files
        /// </summary>
        public string[] AvailableSubtitleFiles { get; set; } = new string[0];

        /// <summary>
        /// Whether we have an image
        /// </summary>
        public bool HasImage { get; } = false;

        /// <summary>
        /// Creates a new http server, hosting it on the provided ip and port, and offering the media files found in the provided folder
        /// </summary>
        /// <param name="ip">The IP to host from</param>
        /// <param name="port">The port to host on</param>
        /// <param name="pathToMediaFiles">The location of the folder containing the media files</param>
        /// <param name="pathToDownloadFiles">The location of the folder containing the download files</param>
        /// <param name="pathToSubtitleFiles">The location of the folder containing the subtitle files</param>
        /// <param name="cancel">The cancellation token that halts the server</param>
        public HttpServer(string ip, ushort port, string pathToMediaFiles, string pathToDownloadFiles, string pathToSubtitleFiles, CancellationToken cancel)
        {
            Ip = ip;
            Port = port;
            MediaFilesPath = pathToMediaFiles;
            DownloadFilesPath = pathToDownloadFiles;
            SubtitleFilesPath = pathToSubtitleFiles;

            CancelToken = cancel;

            //Check if we have an image
            if (File.Exists("server-image.png"))
            {
                HasImage = true;
                ConLog.Log("WebMedia Link", "server-image.png found. Using that as the server's image", LogType.Ok);
            }
            else
            {
                ConLog.Log("WebMedia Link", "server-image.png not found. This server will not have an image in the server list", LogType.Warning);
            }
        }

        /// <summary>
        /// Attempts to start the HTTP server
        /// </summary>
        /// <returns>True if it started successfully, false if not</returns>
        public bool StartServer()
        {
            ConLog.Log("HTTP Server", "Starting HTTP server", LogType.Info);

            bool hasStarted = false;
            try
            {
                Server = new HttpListener();

                //Add server prefixes
                Server.Prefixes.Add("http://*:" + Port + "/");
                Server.Start();

                //Register the cancellation token to stop the server
                CancelToken.Register(() => Server.Stop());

                //Start get context loop
                _ = Task.Run(() => MainLoop());

                ConLog.Log("HTTP Server", "HTTP Server started", LogType.Ok);
                hasStarted = true;
            }
            catch (HttpListenerException e)
            {
                ConLog.Log("HTTP Server", "Error while starting HTTP server: " + e.Message, LogType.Error);
                hasStarted = false;
            }

            return hasStarted;
        }

        /// <summary>
        /// The main loop. Keeps getting contexts, and never ends unless the cancellation token is triggered
        /// </summary>
        /// <returns>A Task representing the operation</returns>
        private async Task MainLoop()
        {
            while (true)
            {
                HttpListenerContext context = await Server.GetContextAsync();
                if (CancelToken.IsCancellationRequested) return;
                _ = Task.Run(() => HandleContext(context));
            }
        }

        /// <summary>
        /// Serve a particular context
        /// </summary>
        /// <param name="context">The context to server</param>
        /// <returns>A Task representing the operation</returns>
        private async Task HandleContext(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            string filePath;
            string fileName;
            string[] arrayToSearchIn;

            //Check whether it's media or download or subtitle or server image
            if (request.Url.LocalPath.StartsWith("/media/"))
            {
                fileName = request.Url.LocalPath.Substring(7);
                filePath = Path.Combine(MediaFilesPath, fileName);
                arrayToSearchIn = AvailableMediaFiles;
            }
            else if (request.Url.LocalPath.StartsWith("/download/"))
            {
                fileName = request.Url.LocalPath.Substring(10);
                filePath = Path.Combine(DownloadFilesPath, fileName);
                arrayToSearchIn = AvailableDownloadFiles;
            }
            else if (request.Url.LocalPath.StartsWith("/subtitle/"))
            {
                fileName = request.Url.LocalPath.Substring(10);
                filePath = Path.Combine(SubtitleFilesPath, fileName);
                arrayToSearchIn = AvailableSubtitleFiles;
            }
            else if (request.Url.LocalPath == "/server-image.png")
            {
                fileName = "server-image.png";
                filePath = Path.GetFullPath("server-image.png");
                arrayToSearchIn = new string[] { "server-image.png" };
            }
            else
            {
                //File not found
                response.StatusCode = 404;
                ConLog.Log("HTTP Server", "404 not found", LogType.Warning);
                response.Close();
                return;
            }

            //Check if the file doesn't exist
            if (!File.Exists(filePath))
            {
                //File not found
                response.StatusCode = 404;
                ConLog.Log("HTTP Server", "404 not found", LogType.Warning);
                response.Close();
                return;
            }
            //Check if the file exists, but we don't have access to it
            else if (!arrayToSearchIn.Contains(fileName))
            {
                //File forbidden
                response.StatusCode = 403;
                ConLog.Log("HTTP Server", "403 forbidden", LogType.Warning);
                response.Close();
                return;
            }

            //Alright, we're good to go
            await SendFile(context, filePath);
        }

        /// <summary>
        /// Sends a file over a context
        /// </summary>
        /// <param name="context">The HTTP context to send it over</param>
        /// <param name="filePath">The file to send</param>
        /// <returns>A Task representing the process</returns>
        private async Task SendFile(HttpListenerContext context, string filePath)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            Stream fileStream = null;
            byte[] buffer = new byte[BUFFER_SIZE];
            long bytesLeft;

            try
            {
                fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                bytesLeft = fileStream.Length;
                response.AddHeader("Accept-Ranges", "bytes");
                response.AddHeader("Access-Control-Allow-Origin", "*");

                //Check for a provided range
                if (!string.IsNullOrEmpty(request.Headers["Range"]))
                {
                    string[] range = request.Headers["Range"].Split(new char[] { '=', '-' });

                    long startPos;
                    long endPos;

                    //Validate
                    if (range[1] == "")
                    {
                        startPos = 0;
                    }
                    else
                    {
                        startPos = Convert.ToInt32(range[1]);
                    }
                    
                    if (range[2] == "")
                    {
                        endPos = bytesLeft - 1;
                    }
                    else
                    {
                        endPos = Convert.ToInt32(range[2]);
                    }

                    response.StatusCode = 206;

                    //Add header
                    response.AddHeader("Content-Range", "bytes " + startPos + "-" + endPos + "/" + bytesLeft);

                    //Move the start position
                    fileStream.Seek(startPos, SeekOrigin.Begin);

                    //Change the bytes left
                    bytesLeft = (endPos + 1) - startPos;
                }

                //Send the data
                while (bytesLeft > 0)
                {
                    //Read the data into the buffer
                    int bytesRead = await fileStream.ReadAsync(buffer, 0, BUFFER_SIZE);
                    if (CancelToken.IsCancellationRequested) return;

                    //Write the data to the response output stream
                    await response.OutputStream.WriteAsync(buffer, 0, bytesRead);
                    if (CancelToken.IsCancellationRequested) return;

                    //Reduce the bytes left, and clear the buffer
                    buffer = new byte[BUFFER_SIZE];
                    bytesLeft -= bytesRead;

                    //Flush the stream to send it
                    await response.OutputStream.FlushAsync();
                    if (CancelToken.IsCancellationRequested) return;
                }
            }
            catch (Exception e)
            {
                response.StatusCode = 500;

                if (e.Message != "The specified network name is no longer available.")
                    ConLog.Log("HTTP Server", "500 internal error: " + e.Message, LogType.Error);
            }

            //Close the file streamd if it's not null
            if (fileStream != null)
            {
                fileStream.Close();
            }

            //Send the response off
            response.Close();
        }
    }
}
