using System.Configuration;
using System.Data;
using System.Windows;
using Void.YoutubeAPI;

namespace youtube_chat_integration_wpf_prototype_testing
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static YoutubeDataAPI YTDataAPI = new();

        void App_Exit(object sender, ExitEventArgs e)
        {
            YTDataAPI.QuitCalled();
        }
    }

}
