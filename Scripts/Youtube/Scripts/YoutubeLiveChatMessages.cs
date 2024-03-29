using System.Net.Http;
using System.Diagnostics;
using SimpleJSON;

namespace Void.YoutubeAPI.LiveStreamChat.Messages
{
	/// <summary>
	/// Main class that handles API initialization and
	/// sending web queries to fetch the latest messages from a given chat.
	///
	/// Does not require OAuth 2.0 authentication.
	/// </summary>
	public class YoutubeLiveChatMessages
	{

		public static event Action<List<YoutubeChatMessage>> OnChatMessages;
		public static event Action<int> OnIntervalUpdateMilliseconds;
		public static event Action<string> OnFeedback;

		// Chat ID collected from the video ID and tokens to keep updating chat polls
		private string _apiKey = "";
		private string _chatID = "";
		private string _nextPageToken = "";

		// Temporarily store the newest publishing time, _publishedAt inherits this at the end of each chat message check
		private DateTime _publishedAt;
		private DateTime _newPublishedAt;

		// Usernames/Messages to invoke when extracted from polling.
		private List<YoutubeChatMessage> _fullMessages = new List<YoutubeChatMessage>();

		private System.Net.Http.HttpClient _requestClient = new();


		public YoutubeLiveChatMessages()
		{
			YoutubeDataAPI.OnRequest += GetChatMessages;
			YoutubeDataAPI.OnQuit += QuitCalled;
			_requestClient.Timeout = TimeSpan.FromSeconds(5);
		}


		/// <summary>
		/// Attempt to connect to a Youtube Livestream Chat
		/// </summary>
		/// <param name="videoID">The video ID of the stream to connect to.</param>
		/// <param name="apiKey"></param>
		/// <returns>Was the call successful and the explanation for it.</returns>
		public async Task<(bool, string)> Connect(string videoID, string apiKey)
		{
			if (string.IsNullOrWhiteSpace(apiKey))
				return (false, "No valid API Key provided.");

			_apiKey = apiKey;
			_nextPageToken = "";

            Trace.WriteLine("Starting GetChatIDAsync");

			(bool idFetched, string reason) = await GetChatIDAsync(videoID);
			if (!idFetched)
				return (idFetched, reason);

            Trace.WriteLine("Starting InitializeChatAsync");
			(bool connected, string reason2) = await InitializeChatAsync();
			return (connected, reason2);
		}


		/// <summary>
		/// Get the livestream Chat ID based on the given Video ID.
		/// If successful, Chat ID is stored locally in this class.
		/// Youtube API Query - 1 point.
		/// </summary>
		/// <returns>
		/// bool - Was fetching the Chat ID successful?
		/// string - Chat ID fetch case explanation.
		/// </returns>
		private async Task<(bool, string)> GetChatIDAsync(string videoID)
		{
			_chatID = "";

			Uri URL = new Uri($"https://www.googleapis.com/youtube/v3/videos?part=liveStreamingDetails&key={_apiKey}&id={videoID}");
			
			YoutubeKeyManager.AddQuota(1);
			
			// Perform a GET request for the Chat ID
			(bool success, string result) = await GetRequest(URL);
			
			// Check if any internal GET request errors had occurred.
			if (!success)
				return (false, result);
			
			JSONNode data = JSON.Parse(result);
			_chatID = data["items"][0]["liveStreamingDetails"]["activeLiveChatId"];

			if (string.IsNullOrWhiteSpace(_chatID))
				return (false, "No livestream chat was found on the given video ID. Check if you typed it in correctly.");

			return (true, "Youtube Chat ID successfully found and stored. Use YoutubeLiveChatMessages.GetChatMessages to get all the messages since the last call.");
		}

		/// <summary>
		/// Make the first Youtube Chat API call to initialize the most recent timestamp and
		/// wait the needed amount of time before next page tokens start functioning with any set delay.
		/// Youtube API Query - 5 points.
		/// </summary>
		/// <returns> bool - Was the call successful? </returns>
		/// >returns> string - Was the call successful? </returns>
		private async Task<(bool, string)> InitializeChatAsync()
		{
			if (string.IsNullOrWhiteSpace(_chatID))
				return (false, "No Chat ID detected to initialize. use YoutubeLiveStreamChat.GetChatID first.");

			_nextPageToken = "";

			Uri URL = new Uri($"https://www.googleapis.com/youtube/v3/liveChat/messages?part=snippet,authorDetails&key={_apiKey}&liveChatId={_chatID}");

			YoutubeKeyManager.AddQuota(5);

			// Perform a GET request for the first sequence of messages, but only to verify it working.
			(bool success, string result) = await GetRequest(URL);

			// Check if any internal GET request errors had occurred.
			if (!success)
				return (false, result);

			JSONNode data = JSON.Parse(result);
			JSONNode content = data["items"].AsArray[^1];           // arg[arg.Count - 1]

			if (content.Count == 0)
				_publishedAt = DateTime.ParseExact("1970-01-01T00:00:00.00", "yyyy-MM-ddTHH:mm:ss.FF", System.Globalization.CultureInfo.InvariantCulture);
			else
				_publishedAt = DateTime.ParseExact(content["snippet"]["publishedAt"].Value.Substring(0, 22), "yyyy-MM-ddTHH:mm:ss.FF", System.Globalization.CultureInfo.InvariantCulture);

			_newPublishedAt = _publishedAt;
			_nextPageToken = data["nextPageToken"];

			int waitTimeMilliseconds = int.Parse(data["pollingIntervalMillis"]);
			OnIntervalUpdateMilliseconds?.Invoke(waitTimeMilliseconds);

			return (true, "Youtube chat initialization successful.");
		}

		/// <summary>
		/// Make a Youtube Chat API call to fetch and send newest messages (OnChatMessages event) 
		/// up until the currently saved publishing time is passed.
		/// Invoked list order is newest to oldest, so be wary of iterating in reverse when using chat displays.
		/// Youtube API Query - 5 points.
		/// </summary>
		public async void GetChatMessages()
		{
			if (string.IsNullOrWhiteSpace(_nextPageToken))
				throw new MissingFieldException("Next page token should not be empty. Have you connected to a video before calling this?");

			Uri URL = new Uri($"https://www.googleapis.com/youtube/v3/liveChat/messages?part=snippet,authorDetails&key={_apiKey}&liveChatId={_chatID}&pageToken={_nextPageToken}");
			
			YoutubeKeyManager.AddQuota(5);
			
			// Perform a GET request for any chat messages since the last call.
			(bool success, string result) = await GetRequest(URL);
			
			// Check if any internal GET request errors had occurred.
			if (!success)
			{
				_fullMessages.Clear();
				OnChatMessages?.Invoke(_fullMessages);
				return;
			}

			JSONNode data = JSON.Parse(result);
			JSONArray arg = data["items"].AsArray;

			_nextPageToken = data["nextPageToken"]; // Use this to go to the next page
			OnIntervalUpdateMilliseconds?.Invoke(int.Parse(data["pollingIntervalMillis"]));

			// Invoke and send all new messages
			PrepareAndInvokeMessages(ref arg);
			_publishedAt = _newPublishedAt;
		}

		private void PrepareAndInvokeMessages(ref JSONArray arg)
		{
			_fullMessages.Clear();

			int i = arg.Count - 1;
			for (; i >= 0; i--)
			{
				JSONNode content = arg[i];
				DateTime published = DateTime.ParseExact(content["snippet"]["publishedAt"].Value.Substring(0, 22), "yyyy-MM-ddTHH:mm:ss.FF", System.Globalization.CultureInfo.InvariantCulture);

				if (i == arg.Count - 1) // Special instance to record the most newest message publishing string
					_newPublishedAt = published;

				if (DateTime.Compare(_publishedAt, published) >= 0)
					break;

				_fullMessages.Add(new YoutubeChatMessage(content, published));
			}
			OnChatMessages?.Invoke(_fullMessages);
		}
		
		/// <summary>
		/// Perform a GET request to the provided URL, assuming it's a website where you get a JSON message in return
		/// </summary>
		/// <param name="url">The provided Uri page f</param>
		/// <returns>
		/// A bool on if the request was successful. If false, the string is a message explaining the failure.
		/// If true, the string is the resulting JSON message to be parsed.
		///</returns>
		private async Task<(bool, string)> GetRequest(Uri url)
		{
			// Phase 1 - attempt to get a JSON message
			string request;
			
			try
			{
				request = await _requestClient.GetStringAsync(url);
			}
			
			catch (HttpRequestException e)
			{
				return (false, $"{e.StatusCode} - {e.Message}");
			}
			
			catch (InvalidOperationException e)
			{
				return (false, $"Invalid operation, exception message - {e.Message}");
			}
			
			catch (TaskCanceledException e)
			{
				return (false, $"The task was canceled, exception message - {e.Message}");
			}
			
			// Phase 2 -  Check if the JSON message was fetched, but got an error.
			(bool isError, string contents) = HasError(request);
			if (isError)
				return (false, contents);
			
			// No issues found
			return (true, request);
		}

		private (bool, string) HasError(string message)
		{
			JSONNode data = JSON.Parse(message);
			string cause = data["error"]["errors"].AsArray[0]["message"];

			// Case - No errors found
			if (string.IsNullOrWhiteSpace(cause))
				return (false, "");
			
			// Case - This occurs if interval is less than Youtube wants and no messages are found for the first time on this loop.
			if (cause.Contains("The request was sent too soon after the previous one."))
				return (false, "");

			string answer = $"Error, cause - {cause}";

			// Example: "HTTP/1.1 403 Forbidden - The request cannot be completed because you have exceeded your quota."
			OnFeedback?.Invoke(answer);
			return (true, answer);
		}

		private void QuitCalled()
		{
			YoutubeDataAPI.OnRequest -= GetChatMessages;
			YoutubeDataAPI.OnQuit -= QuitCalled;
		}
	}
}

