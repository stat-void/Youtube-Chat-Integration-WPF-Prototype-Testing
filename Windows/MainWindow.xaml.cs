using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Void.YoutubeAPI;
using Void.YoutubeAPI.LiveStreamChat.Messages;
using youtube_chat_integration_wpf_prototype_testing.Scripts;

namespace youtube_chat_integration_wpf_prototype_testing
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IYoutubeRequestBinder
    {
        //private readonly List<ChatMessage> _chatData = new();
        private readonly ObservableCollection<ChatMessage> _chatData = new();
        private readonly ObservableCollection<PollMessage> _pollData = new();

        private readonly Dictionary<string, string> _userNameMessageDict = new();
        private readonly Dictionary<string, int> _messageCountDict = new();

        public MainWindow()
        {
            InitializeComponent();
            LoginScreen.Visibility = Visibility.Visible;
            OptionsTab.Visibility = Visibility.Hidden;

            ChatMessagesList.ItemsSource = _chatData;
            ChatPollingList.ItemsSource = _pollData;
        }

        public void BindToYoutube()
        {
            YoutubeLiveChatMessages.OnChatMessages += ReceivedYTMessages;
        }

        public void UnbindFromYoutube()
        {
            YoutubeLiveChatMessages.OnChatMessages -= ReceivedYTMessages;
        }

        public void ReceivedYTMessages(List<YoutubeChatMessage> messages)
        {
            for (int i = messages.Count-1; i >= 0; i--)
            {
                YoutubeChatMessage message = messages[i];

                // 1. Update chat message data
                if (_chatData.Count >= 20)
                    _chatData.RemoveAt(0);
                _chatData.Add(new ChatMessage() { Username = message.Username, Message = message.Message});


                // 2. Update polling data
                string messageKey = message.Message.Trim().ToLower();
                if (!_userNameMessageDict.ContainsKey(messageKey))
                {
                    _messageCountDict[messageKey] = 1;
                }
                else
                {
                    _messageCountDict[_userNameMessageDict[message.Username]]--;
                    _messageCountDict[messageKey]++;
                }

                // Finish changing the message of the user
                _userNameMessageDict[message.Username] = messageKey;

                Trace.WriteLine($"{message.Username} - \"{message.Message}\"");
            }

            //Linq isn't ideal, but is quick enough for a prototype
            var sortedDict = (from entry in _messageCountDict orderby entry.Value descending select entry).Take(10);
            _pollData.Clear();

            foreach (KeyValuePair<string, int> pair in sortedDict)
                _pollData.Add(new() { Message = pair.Key, Count = pair.Value });

            //ChatMessagesList.Items.Refresh();
        }

        private async void ActivateButton_Click(object sender, RoutedEventArgs e)
        {
            string apiKey = APIKeyTextBox.Text;
            string videoID = VideoIDTextBox.Text;

            if (String.IsNullOrWhiteSpace(apiKey) || String.IsNullOrWhiteSpace(videoID))
                return;

            ActivateButton.IsEnabled = false;

            bool result = await App.YTDataAPI.ConnectToLivestreamChat(videoID, apiKey);

            if (result)
            {
                LoginScreen.Visibility = Visibility.Hidden;
                OptionsTab.Visibility = Visibility.Visible;
                
                // Activate regular chatting tab and start the call process
                BindToYoutube();
                App.YTDataAPI.APITimer.StartTimer();
            }

            ActivateButton.IsEnabled = true;
        }
    }

    public class ChatMessage
    {
        public string? Username { get; set; }
        public string? Message { get; set; }
    }

    public class PollMessage
    {
        public string? Message { get; set; }
        public int? Count { get; set; }
    }
}