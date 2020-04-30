using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebServer.SignalR.Server
{
    public class StateUpdated
    {
        /// <summary>
        /// The media UUID that is currently playing
        /// </summary>
        public int CurrentMediaUuid { get; set; } = int.MaxValue;

        /// <summary>
        /// The path to the currently playing video track
        /// </summary>
        public string CurrentVideoPath { get; set; } = "";

        /// <summary>
        /// The path to the currently playing audio track
        /// </summary>
        public string CurrentAudioPath { get; set; } = "";

        /// <summary>
        /// The title of the currently playing media
        /// </summary>
        public string MediaTitle { get; set; } = "";

        /// <summary>
        /// The duration of the currently playing media
        /// </summary>
        public double Duration { get; set; } = 0;

        /// <summary>
        /// Whether the media player is currently playing
        /// </summary>
        public bool IsPlaying { get; set; } = false;

        /// <summary>
        /// The seek position that was the last time we seeked
        /// </summary>
        public double LastSeekPosition { get; set; } = 0;

        /// <summary>
        /// The available subtitles for the currently playing media
        /// </summary>
        public Subtitle[] Subtitles { get; set; } = new Subtitle[0];

        /// <summary>
        /// The URLs of the available subtitle fonts
        /// </summary>
        public string[] SubtitleFonts { get; set; } = new string[0];

        /// <summary>
        /// Whether the media player is currently buffering
        /// </summary>
        public bool IsBuffering { get; set; } = false;

        /// <summary>
        /// An array of information about the users
        /// </summary>
        public User[] Users { get; set; } = new User[0];
    }

    public class User
    {
        /// <summary>
        /// The UUID of the user
        /// </summary>
        public int Uuid { get; set; } = int.MaxValue;

        /// <summary>
        /// The name of the user
        /// </summary>
        public string Username { get; set; } = "";

        /// <summary>
        /// The current buffer state of the user
        /// </summary>
        public UserBufferState BufferState { get; set; } = UserBufferState.HasNothing;
    }

    public class QueueUpdated
    {
        /// <summary>
        /// The items in the queue
        /// </summary>
        public MediaItem[] QueueItems = new MediaItem[0];
    }


    public class FilesUpdated
    {
        /// <summary>
        /// The offline items that can be played
        /// </summary>
        public MediaItem[] OfflineItems = new MediaItem[0];
    }

    public class MediaItem
    {
        /// <summary>
        /// The UUID of the media item
        /// </summary>
        public int Uuid { get; set; } = int.MaxValue;

        /// <summary>
        /// The title of the media item
        /// </summary>
        public string Title { get; set; } = "";

        /// <summary>
        /// The duration of the media item in seconds
        /// </summary>
        public int Duration { get; set; } = 0;

        /// <summary>
        /// Whether the media item has video
        /// </summary>
        public bool HasVideo { get; set; } = true;

        /// <summary>
        /// Whether the media item is stored or downloaded
        /// </summary>
        public bool IsStored { get; set; } = true;

        /// <summary>
        /// Whether the media item is available to play
        /// </summary>
        public bool IsAvailable { get; set; } = true;
    }

    public class Subtitle
    {
        /// <summary>
        /// The URL of the subtitle file
        /// </summary>
        public string Url { get; set; } = null;

        /// <summary>
        /// The name of the subtitle
        /// </summary>
        public string Name { get; set; } = null;
    }
}
