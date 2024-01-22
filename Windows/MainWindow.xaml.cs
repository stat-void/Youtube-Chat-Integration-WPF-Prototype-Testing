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

        public MainWindow()
        {
            InitializeComponent();
            LoginScreen.Visibility = Visibility.Visible;
            OptionsTab.Visibility = Visibility.Hidden;

            ChatMessagesList.ItemsSource = _chatData;
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

                if (_chatData.Count >= 20)
                    _chatData.RemoveAt(0);

                _chatData.Add(new ChatMessage() { Username = message.Username, Message = message.Message});
                Trace.WriteLine($"{message.Username} - \"{message.Message}\"");
            }

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
}