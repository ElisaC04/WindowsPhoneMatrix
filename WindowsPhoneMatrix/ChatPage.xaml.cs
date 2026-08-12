using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace WindowsPhoneMatrix
{
    public class MessageItem
    {
        public string Sender { get; set; }
        public string Body { get; set; }
        public HorizontalAlignment MessageAlignment { get; set; }
        public Windows.UI.Xaml.Media.SolidColorBrush BubbleColor { get; set; }
    }

    public sealed partial class ChatPage : Page
    {
        private string _accessToken;
        private string _serverAddress;
        private string _roomId;
        private string _roomName;

        private string _nextBatchToken;
        private CancellationTokenSource _pollingCts;

        public ObservableCollection<MessageItem> MessagesList { get; set; }

        public ChatPage()
        {
            this.InitializeComponent();
            MessagesList = new ObservableCollection<MessageItem>();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            string[] chatData = e.Parameter as string[];

            if (chatData != null && chatData.Length >= 4)
            {
                _accessToken = chatData[0];
                _serverAddress = chatData[1];
                _roomId = chatData[2];
                _roomName = chatData[3];

                RoomNameText.Text = _roomName;
                StatusText.Text = "Loading messages...";

                MessagesListView.ItemsSource = MessagesList;
                await LoadMessagesAsync();

                StartLongPolling();
            }
        }

        private async Task LoadMessagesAsync()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string filterJson = "{\"room\":{\"timeline\":{\"limit\":50}}}";
                    string encodedFilter = Uri.EscapeDataString(filterJson);

                    string syncUrl = $"https://{_serverAddress}/_matrix/client/v3/sync?filter={encodedFilter}&access_token={_accessToken}";
                    HttpResponseMessage response = await client.GetAsync(syncUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        JsonObject rootObject = JsonObject.Parse(jsonResponse);

                        if (rootObject.ContainsKey("next_batch"))
                        {
                            _nextBatchToken = rootObject.GetNamedString("next_batch");
                        }

                        if (rootObject.ContainsKey("rooms"))
                        {
                            JsonObject roomsObj = rootObject.GetNamedObject("rooms");
                            if (roomsObj.ContainsKey("join"))
                            {
                                JsonObject joinedRooms = roomsObj.GetNamedObject("join");
                                if (joinedRooms.ContainsKey(_roomId))
                                {
                                    JsonObject roomData = joinedRooms.GetNamedObject(_roomId);
                                    if (roomData.ContainsKey("timeline"))
                                    {
                                        JsonObject timelineObj = roomData.GetNamedObject("timeline");
                                        if (timelineObj.ContainsKey("events"))
                                        {
                                            JsonArray eventsArray = timelineObj.GetNamedArray("events");
                                            MessagesList.Clear();

                                            string myUser = "";
                                            string myMetaId = "";
                                            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

                                            if (localSettings.Values.ContainsKey("Username"))
                                                myUser = localSettings.Values["Username"].ToString();

                                            if (localSettings.Values.ContainsKey("MetaId"))
                                                myMetaId = localSettings.Values["MetaId"].ToString();

                                            string lastEventId = null;

                                            foreach (IJsonValue eventValue in eventsArray)
                                            {
                                                JsonObject evt = eventValue.GetObject();

                                                if (evt.GetNamedString("type") == "m.room.message")
                                                {
                                                    if (evt.ContainsKey("event_id"))
                                                    {
                                                        lastEventId = evt.GetNamedString("event_id");
                                                    }

                                                    JsonObject content = evt.GetNamedObject("content");
                                                    if (content.ContainsKey("body"))
                                                    {
                                                        string body = content.GetNamedString("body");
                                                        string sender = evt.GetNamedString("sender");

                                                        bool isMe = (!string.IsNullOrEmpty(myUser) && sender.Contains(myUser)) ||
                                                                    (!string.IsNullOrEmpty(myMetaId) && sender.Contains(myMetaId));

                                                        string displaySender = isMe ? "Me" : _roomName;

                                                        var bubbleColor = isMe ? new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 215)) : new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 45, 45, 48));
                                                        var align = isMe ? HorizontalAlignment.Right : HorizontalAlignment.Left;

                                                        MessagesList.Add(new MessageItem
                                                        {
                                                            Sender = displaySender,
                                                            Body = body,
                                                            MessageAlignment = align,
                                                            BubbleColor = bubbleColor
                                                        });
                                                    }
                                                }
                                            }

                                            StatusText.Text = "";
                                            if (MessagesList.Count > 0)
                                            {
                                                MessagesListView.ScrollIntoView(MessagesList[MessagesList.Count - 1]);
                                            }

                                            if (!string.IsNullOrEmpty(lastEventId))
                                            {
                                                await MarkRoomAsReadAsync(lastEventId);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
            }
        }

        private async void StartLongPolling()
        {
            _pollingCts = new CancellationTokenSource();
            CancellationToken token = _pollingCts.Token;

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    while (!token.IsCancellationRequested)
                    {
                        if (string.IsNullOrEmpty(_nextBatchToken))
                        {
                            await Task.Delay(2000, token);
                            continue;
                        }

                        string syncUrl = $"https://{_serverAddress}/_matrix/client/v3/sync?access_token={_accessToken}&since={_nextBatchToken}&timeout=30000";

                        HttpResponseMessage response = await client.GetAsync(syncUrl, token);

                        if (response.IsSuccessStatusCode)
                        {
                            string jsonResponse = await response.Content.ReadAsStringAsync();
                            JsonObject rootObject = JsonObject.Parse(jsonResponse);

                            if (rootObject.ContainsKey("next_batch"))
                            {
                                _nextBatchToken = rootObject.GetNamedString("next_batch");
                            }

                            if (rootObject.ContainsKey("rooms"))
                            {
                                JsonObject roomsObj = rootObject.GetNamedObject("rooms");
                                if (roomsObj.ContainsKey("join"))
                                {
                                    JsonObject joinedRooms = roomsObj.GetNamedObject("join");
                                    if (joinedRooms.ContainsKey(_roomId))
                                    {
                                        JsonObject roomData = joinedRooms.GetNamedObject(_roomId);
                                        if (roomData.ContainsKey("timeline"))
                                        {
                                            JsonObject timelineObj = roomData.GetNamedObject("timeline");
                                            if (timelineObj.ContainsKey("events"))
                                            {
                                                JsonArray eventsArray = timelineObj.GetNamedArray("events");

                                                string myUser = "";
                                                string myMetaId = "";
                                                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

                                                if (localSettings.Values.ContainsKey("Username"))
                                                    myUser = localSettings.Values["Username"].ToString();
                                                if (localSettings.Values.ContainsKey("MetaId"))
                                                    myMetaId = localSettings.Values["MetaId"].ToString();

                                                bool scrollNeeded = false;

                                                string lastEventId = null;

                                                foreach (IJsonValue eventValue in eventsArray)
                                                {
                                                    JsonObject evt = eventValue.GetObject();
                                                    if (evt.GetNamedString("type") == "m.room.message")
                                                    {

                                                        if (evt.ContainsKey("event_id"))
                                                        {
                                                            lastEventId = evt.GetNamedString("event_id");
                                                        }

                                                        JsonObject content = evt.GetNamedObject("content");
                                                        if (content.ContainsKey("body"))
                                                        {
                                                            string body = content.GetNamedString("body");
                                                            string sender = evt.GetNamedString("sender");

                                                            bool isMe = (!string.IsNullOrEmpty(myUser) && sender.Contains(myUser)) ||
                                                                        (!string.IsNullOrEmpty(myMetaId) && sender.Contains(myMetaId));

                                                            string displaySender = isMe ? "Me" : _roomName;

                                                            bool isDuplicate = false;
                                                            if (MessagesList.Count > 0)
                                                            {
                                                                var lastMsg = MessagesList[MessagesList.Count - 1];
                                                                if (lastMsg.Sender == displaySender && lastMsg.Body == body)
                                                                {
                                                                    isDuplicate = true;
                                                                }
                                                            }

                                                            if (!isDuplicate)
                                                            {
                                                                var bubbleColor = isMe ? new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 215)) : new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 45, 45, 48));
                                                                var align = isMe ? HorizontalAlignment.Right : HorizontalAlignment.Left;

                                                                MessagesList.Add(new MessageItem
                                                                {
                                                                    Sender = displaySender,
                                                                    Body = body,
                                                                    MessageAlignment = align,
                                                                    BubbleColor = bubbleColor
                                                                });
                                                                scrollNeeded = true;
                                                            }
                                                        }
                                                    }
                                                }

                                                if (scrollNeeded)
                                                {
                                                    MessagesListView.ScrollIntoView(MessagesList[MessagesList.Count - 1]);
                                                }

                                                if (!string.IsNullOrEmpty(lastEventId))
                                                {
                                                    await MarkRoomAsReadAsync(lastEventId);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            await Task.Delay(5000, token);
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Polling Error: {ex.Message}");
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string message = MessageInputBox.Text;
            if (string.IsNullOrWhiteSpace(message)) return;

            MessageInputBox.Text = "";
            MessageInputBox.IsEnabled = false;
            SendButton.IsEnabled = false;

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string txnId = Guid.NewGuid().ToString();
                    string url = $"https://{_serverAddress}/_matrix/client/v3/rooms/{_roomId}/send/m.room.message/{txnId}?access_token={_accessToken}";

                    JsonObject messageJson = new JsonObject();
                    messageJson.SetNamedValue("msgtype", JsonValue.CreateStringValue("m.text"));
                    messageJson.SetNamedValue("body", JsonValue.CreateStringValue(message));

                    string jsonPayload = messageJson.Stringify();
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PutAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        MessagesList.Add(new MessageItem
                        {
                            Sender = "Me",
                            Body = message,
                            MessageAlignment = HorizontalAlignment.Right,
                            BubbleColor = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 215))
                        });

                        StatusText.Text = "";
                        MessagesListView.ScrollIntoView(MessagesList[MessagesList.Count - 1]);
                    }
                    else
                    {
                        StatusText.Text = "Failed to send message.";
                    }
                }
            }
            catch (Exception)
            {
                StatusText.Text = "Network error sending message.";
            }
            finally
            {
                MessageInputBox.IsEnabled = true;
                SendButton.IsEnabled = true;
                MessageInputBox.Focus(FocusState.Programmatic);
            }
        }

        private async Task MarkRoomAsReadAsync(string eventId)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string url = $"https://{_serverAddress}/_matrix/client/v3/rooms/{_roomId}/receipt/m.read/{eventId}?access_token={_accessToken}";

                    var content = new StringContent("{}", Encoding.UTF8, "application/json");
                    await client.PostAsync(url, content);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending read receipt: {ex.Message}");
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _pollingCts?.Cancel();

            if (this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
            }
        }
    }
}