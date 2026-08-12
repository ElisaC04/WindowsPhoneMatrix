using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

using Windows.Security.Credentials;

namespace WindowsPhoneMatrix
{
    public class RoomItem
    {
        public string RoomId { get; set; }
        public string RoomName { get; set; }
    }

    public sealed partial class RoomsPage : Page
    {
        private string _accessToken;
        private string _serverAddress;

        public ObservableCollection<RoomItem> RoomsList { get; set; }

        public RoomsPage()
        {
            this.InitializeComponent();
            RoomsList = new ObservableCollection<RoomItem>();

            RoomsListView.ItemsSource = RoomsList;

            this.NavigationCacheMode = NavigationCacheMode.Required;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            string[] sessionData = e.Parameter as string[];

            if (sessionData != null)
            {
                _accessToken = sessionData[0];
                _serverAddress = sessionData[1];

                if (e.NavigationMode != NavigationMode.Back)
                {
                    await SyncRoomsAsync();
                }
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RoomsList.Clear();
            WelcomeText.Text = "Refreshing chats...";
            await SyncRoomsAsync();
        }

        private async Task SyncRoomsAsync()
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

                                foreach (string roomId in joinedRooms.Keys)
                                {
                                    string roomName = "Unknown Chat";

                                    JsonObject roomData = joinedRooms.GetNamedObject(roomId);

                                    if (roomData.ContainsKey("state"))
                                    {
                                        JsonObject stateObj = roomData.GetNamedObject("state");
                                        if (stateObj.ContainsKey("events"))
                                        {
                                            JsonArray eventsArray = stateObj.GetNamedArray("events");

                                            foreach (IJsonValue eventValue in eventsArray)
                                            {
                                                JsonObject evt = eventValue.GetObject();
                                                if (evt.GetNamedString("type") == "m.room.name")
                                                {
                                                    roomName = evt.GetNamedObject("content").GetNamedString("name");
                                                    break;
                                                }
                                            }
                                        }
                                    }

                                    if (roomName == "Unknown Chat" && roomData.ContainsKey("timeline"))
                                    {
                                        JsonObject timelineObj = roomData.GetNamedObject("timeline");
                                        if (timelineObj.ContainsKey("events"))
                                        {
                                            JsonArray timelineEvents = timelineObj.GetNamedArray("events");
                                            foreach (IJsonValue timelineVal in timelineEvents)
                                            {
                                                JsonObject timelineEvt = timelineVal.GetObject();
                                                if (timelineEvt.GetNamedString("type") == "m.room.name")
                                                {
                                                    JsonObject content = timelineEvt.GetNamedObject("content");
                                                    if (content.ContainsKey("name"))
                                                    {
                                                        roomName = content.GetNamedString("name");
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    RoomsList.Add(new RoomItem { RoomId = roomId, RoomName = roomName });
                                }
                            }
                        }

                        WelcomeText.Text = $"Sync complete! Found {RoomsList.Count} chats.";
                    }
                    else
                    {
                        WelcomeText.Text = $"Sync failed! Code: {response.StatusCode}";
                    }
                }
            }
            catch (Exception ex)
            {
                WelcomeText.Text = $"Network Error: {ex.Message}";
            }
        }

        private void RoomsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            RoomItem clickedRoom = e.ClickedItem as RoomItem;

            if (clickedRoom != null)
            {
                string[] chatData = new string[]
                {
                    _accessToken,
                    _serverAddress,
                    clickedRoom.RoomId,
                    clickedRoom.RoomName
                };

                Frame.Navigate(typeof(ChatPage), chatData);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            localSettings.Values.Remove("MetaId");
            localSettings.Values.Remove("ServerAddress");
            localSettings.Values.Remove("Username");

            try
            {
                var vault = new PasswordVault();
                var credentials = vault.FindAllByResource("WindowsPhoneMatrix");
                foreach (var cred in credentials)
                {
                    vault.Remove(cred);
                }
            }
            catch (Exception)
            {

            }

            if (this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
            }
        }
    }
}