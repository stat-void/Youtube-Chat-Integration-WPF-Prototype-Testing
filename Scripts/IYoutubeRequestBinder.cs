using Void.YoutubeAPI.LiveStreamChat.Messages;

namespace youtube_chat_integration_wpf_prototype_testing.Scripts
{
    public interface IYoutubeRequestBinder
    {
        public void BindToYoutube();
        public void UnbindFromYoutube();
        protected void ReceivedYTMessages(List<YoutubeChatMessage> messages);
    }
}
