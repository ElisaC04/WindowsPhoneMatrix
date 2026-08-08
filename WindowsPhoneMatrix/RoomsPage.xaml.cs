using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace WindowsPhoneMatrix
{
    // This simple class holds the data for a single chat room
    public class RoomItem
    {
        public string RoomId { get; set; }
        public string RoomName { get; set; }
    }

    public sealed partial class RoomsPage : Page
    {
        private string _accessToken;
        private string _serverAddress;

        // ObservableCollection automatically updates the ListView when we add items!
        public ObservableCollection<RoomItem> RoomsList { get; set; }

        public RoomsPage()
        {
            this.InitializeComponent();
            RoomsList = new ObservableCollection<RoomItem>();

            // Connect our code list to the XAML ListView
            RoomsListView.ItemsSource = RoomsList;
        }

        // Notice we added 'async' here so we can call our sync method
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            string[] sessionData = e.Parameter as string[];

            if (sessionData != null)
            {
                _accessToken = sessionData[0];
                _serverAddress = sessionData[1];

                // Kick off the network request
                await SyncRoomsAsync();
            }
        }

        private async Task SyncRoomsAsync()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // 1. Ask the Matrix server for ALL our data
                    string syncUrl = $"https://{_serverAddress}/_matrix/client/v3/sync?access_token={_accessToken}";
                    HttpResponseMessage response = await client.GetAsync(syncUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        // 2. Read the massive JSON response
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        JsonObject rootObject = JsonObject.Parse(jsonResponse);

                        // 3. Dig into the JSON: root -> rooms -> join
                        if (rootObject.ContainsKey("rooms"))
                        {
                            JsonObject roomsObj = rootObject.GetNamedObject("rooms");
                            if (roomsObj.ContainsKey("join"))
                            {
                                JsonObject joinedRooms = roomsObj.GetNamedObject("join");

                                // 4. Loop through every room we are joined to
                                foreach (string roomId in joinedRooms.Keys)
                                {
                                    string roomName = "Unnamed Room";

                                    // 5. Dig deeper into the state events to find the room's name
                                    JsonObject roomData = joinedRooms.GetNamedObject(roomId);
                                    if (roomData.ContainsKey("state"))
                                    {
                                        JsonObject stateObj = roomData.GetNamedObject("state");
                                        if (stateObj.ContainsKey("events"))
                                        {
                                            JsonArray eventsArray = stateObj.GetNamedArray("events");

                                            // Look through all events for the "m.room.name" type
                                            foreach (IJsonValue eventValue in eventsArray)
                                            {
                                                JsonObject evt = eventValue.GetObject();
                                                if (evt.GetNamedString("type") == "m.room.name")
                                                {
                                                    roomName = evt.GetNamedObject("content").GetNamedString("name");
                                                    break; // We found the name, stop searching events
                                                }
                                            }
                                        }
                                    }

                                    // 6. Add it to our UI list!
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
            // Wipe the saved data
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            localSettings.Values.Remove("AccessToken");
            localSettings.Values.Remove("ServerAddress");
            localSettings.Values.Remove("Username");

            // Go back to login screen
            if (this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
            }
        }
    }
}