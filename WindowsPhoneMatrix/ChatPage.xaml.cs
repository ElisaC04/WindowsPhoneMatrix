using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
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
            }
        }

        private async Task LoadMessagesAsync()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string syncUrl = $"https://{_serverAddress}/_matrix/client/v3/sync?access_token={_accessToken}";
                    HttpResponseMessage response = await client.GetAsync(syncUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        JsonObject rootObject = JsonObject.Parse(jsonResponse);

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

                                            // Grab both the Matrix Username and the Facebook Display Name
                                            string myUser = "";
                                            string myMetaId = "";
                                            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

                                            if (localSettings.Values.ContainsKey("Username"))
                                                myUser = localSettings.Values["Username"].ToString();

                                            if (localSettings.Values.ContainsKey("MetaId"))
                                                myMetaId = localSettings.Values["MetaId"].ToString();

                                            foreach (IJsonValue eventValue in eventsArray)
                                            {
                                                JsonObject evt = eventValue.GetObject();

                                                if (evt.GetNamedString("type") == "m.room.message")
                                                {
                                                    JsonObject content = evt.GetNamedObject("content");
                                                    if (content.ContainsKey("body"))
                                                    {
                                                        string body = content.GetNamedString("body");
                                                        string sender = evt.GetNamedString("sender");

                                                        // Check if the sender matches our Matrix ID OR our Facebook Name
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

                    string jsonPayload = $"{{\"msgtype\":\"m.text\", \"body\":\"{message}\"}}";
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

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
            }
        }
    }
}