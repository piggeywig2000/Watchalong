using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Watchalong.Utils;

namespace WebServer
{
    public enum UserBufferState
    {
        Ready,
        HasMetadata,
        HasNothing,
        NotStarted
    }

    public class User
    {
        /// <summary>
        /// The UUID of this user
        /// </summary>
        public int UserUuid { get; } = int.MaxValue;

        /// <summary>
        /// This user's SignalR connectionId
        /// </summary>
        public string ConnectionId { get; } = "";

        /// <summary>
        /// The UUID of the media server that this user is connected to
        /// </summary>
        public int MediaServerUuid { get;  } = int.MaxValue;

        /// <summary>
        /// The user's username
        /// </summary>
        public string Username { get; } = "";

        /// <summary>
        /// The media that is currently playing on this user
        /// </summary>
        public int CurrentMediaUuid { get; set; } = int.MaxValue;

        /// <summary>
        /// Whether the media player is currently playing on this user
        /// </summary>
        public bool IsPlaying { get; set; } = false;

        /// <summary>
        /// The seek position that was the last time this user seeked
        /// </summary>
        public double LastSeekPosition { get; set; } = 0;

        /// <summary>
        /// The buffer state of this user
        /// </summary>
        public UserBufferState BufferState { get; set; } = UserBufferState.NotStarted;

        /// <summary>
        /// Create a new user
        /// </summary>
        /// <param name="connectionId">The SignalR connection ID of the user</param>
        /// <param name="mediaServerUuid">The UUID of the media server that this User is connected to</param>
        /// <param name="username">The username of the user</param>
        public User(string connectionId, int mediaServerUuid, string username)
        {
            ConnectionId = connectionId;
            MediaServerUuid = mediaServerUuid;
            Username = username;
            UserUuid = Uuid.GetUuid();
        }
    }
}
