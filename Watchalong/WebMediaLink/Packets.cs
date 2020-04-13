using System;
using Network.Packets;
using Newtonsoft.Json;

namespace WebMediaLink
{
    public class GetInfoRequest : RequestPacket
    {
        public GetInfoRequest()
        {

        }
    }

    [Network.Attributes.PacketRequest(typeof(GetInfoRequest))]
    public class GetInfoResponse : ResponsePacket
    {
        public string Name { get; set; } = "";

        public string Password { get; set; } = "";

        public string ImageUrl { get; set; } = "";

        [JsonIgnore]
        public string MediaUrlsJson { get; set; } = "";

        [Network.Attributes.PacketIgnoreProperty]
        public PlayableFile[] MediaUrls { get; set; } = new PlayableFile[0];

        public GetInfoResponse(string name, string password, string imageUrl, PlayableFile[] mediaFiles, GetInfoRequest requestPacket) : base(requestPacket)
        {
            Name = name;
            Password = password;
            ImageUrl = imageUrl;
            MediaUrls = mediaFiles;
        }

        public override void BeforeSend()
        {
            MediaUrlsJson = JsonConvert.SerializeObject(MediaUrls);
            base.BeforeSend();
        }

        public override void BeforeReceive()
        {
            MediaUrls = JsonConvert.DeserializeObject<PlayableFile[]>(MediaUrlsJson);
            base.BeforeReceive();
        }
    }
}
