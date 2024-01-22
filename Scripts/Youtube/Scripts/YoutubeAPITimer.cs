using Void.YoutubeAPI.LiveStreamChat.Messages;
using SimpleJSON;
using System.Windows.Threading;
using System.Diagnostics;

namespace Void.YoutubeAPI
{
	/// <summary>
	/// Handles all time based components and requests the API to fetch
	/// messages whenever the conditions (delay reaching 0 seconds) have been met.
	/// </summary>
	public partial class YoutubeAPITimer
	{
		/// <summary>
		/// Event used to share consistent time state. Given float is current time before being cut down by crossing delay.
		/// Use for visuals requiring time state, comparing against delay or listening for OnAPIMessagesRequested.
		/// </summary>
		//public event Action<double> SendCurrentTime;

		/// <summary>
		/// Event used to notify whenever the current API delay to invoke message request was changed.
		/// </summary>
		public event Action<int> OnAPIRequestDelayChanged;

		/// <summary>
		/// Event used to notify if any piece of code externally updated play status of this class timer.
		/// </summary>
		public event Action<bool> OnTimerPlayUpdate;

		/// <summary>
		/// Event used to automate API message requests if this class is used.
		/// </summary>
		public static event Action OnRequest;

		/// <summary> The amount of time (in milliseconds) needed to pass to request an API call. </summary>
		public int APIRequestInterval
		{
			get { return _apiRequestIntervalMilliseconds; }
		}

		//public bool IsPlaying { get; private set; } = false;

		private int _apiRequestIntervalMilliseconds = 3000;

		/// <summary> Decide if this class should use the interval gotten in each chat message request, or set it manually </summary>
		public bool UseYoutubeInterval
		{
			get
			{
				return false;
			}
		}

		private JSONNode _apiData;

		/// <summary> If no JSON data exists, should the application use the suggested wait time from Youtube GET requests?  </summary>
		private const bool _useYTIntervalDefault = false;

		/// <summary>  If no JSON data exists and Youtube GET request wait time is not used, how long should the delay be?  </summary>
		private const int _apiRequestIntervalDefaultMilliseconds = 3000;

        private DispatcherTimer _timer = new();
        
        
		public YoutubeAPITimer()
		{
			StopTimer();

            //YoutubeLiveChatMessages.OnIntervalUpdateMilliseconds += RecommendedWaitUpdate;
            _apiData = YoutubeSaveData.GetData();

            // If JSON data exists, use it, otherwise set default values.
            _apiRequestIntervalMilliseconds = !string.IsNullOrEmpty(_apiData["YT"]["requestInterval"]) ? _apiData["YT"]["requestInterval"].AsInt : _apiRequestIntervalDefaultMilliseconds;

            _timer.Tick += new EventHandler(Timer_Tick);
            _timer.Interval = new TimeSpan(0, 0, 0, 0, _apiRequestIntervalMilliseconds);
        }

		private void Timer_Tick(object sender, EventArgs e)
        {
            // Not sure if a timespan can call this in an async process
            //SendCurrentTime?.Invoke(_currentTime);

			// Send an invoke event that new messages should be checked
            OnRequest?.Invoke();
        }
		/*private void RecommendedWaitUpdate(int waitTimeMilliseconds)
		{
			if (_useYTInterval)
				SetAPIRequestInterval(waitTimeMilliseconds);
		}*/

		/*public void SetAPIRequestInterval(int milliseconds)
		{
			if (milliseconds <= 0)
			{
				Trace.WriteLine("Request delay can't be 0 or negative.");
				return;
			}  

			else if (milliseconds < 500)
			{
                Trace.WriteLine("Going below 0.5 seconds is wasteful on quota and volatile at fetching messages.");
				return;
			}  

			else if (milliseconds < 700)
                Trace.WriteLine("Setting delay below 0.7 seconds can cause duplicate messages to appear as Youtube API corrects timestamps.");

            _apiRequestIntervalMilliseconds = milliseconds;
			_apiData["YT"]["requestInterval"] = _apiRequestIntervalMilliseconds.ToString();
			OnAPIRequestDelayChanged?.Invoke(_apiRequestIntervalMilliseconds);
			
		}*/

		public void StartTimer()
		{
            _timer.Start();
            OnTimerPlayUpdate?.Invoke(true);
            Trace.WriteLine("Timer should be started");
        }

		public void StopTimer()
		{
            _timer.Stop();
			OnTimerPlayUpdate?.Invoke(false);
            Trace.WriteLine("Timer should be stopped");
        }

		public void QuitCalled()
		{
            _timer.Stop();
            //YoutubeLiveChatMessages.OnIntervalUpdateMilliseconds -= RecommendedWaitUpdate;
        }
	}
}