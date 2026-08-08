using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
//For networking
using System.Net.Http;
using System.Text;
using Windows.Data.Json;

//For existing login check
using Windows.Storage;


using Windows.Foundation.Metadata;

namespace WindowsPhoneMatrix
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

            this.Loaded += MainPage_Loaded;
        }


        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Optional: Make the title bar dark if running on a Desktop PC
            var titleBar = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().TitleBar;
            titleBar.BackgroundColor = Windows.UI.Color.FromArgb(255, 30, 30, 30);
            titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(255, 30, 30, 30);
            titleBar.ForegroundColor = Windows.UI.Colors.White;
            titleBar.ButtonForegroundColor = Windows.UI.Colors.White;

            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();
                statusBar.BackgroundColor = Windows.UI.Color.FromArgb(255, 30, 30, 30);
                statusBar.BackgroundOpacity = 1;
                statusBar.ForegroundColor = Windows.UI.Colors.White;
            }

            var localSettings = ApplicationData.Current.LocalSettings;

            // Check if we've logged in before
            if (localSettings.Values.ContainsKey("AccessToken") &&
                localSettings.Values.ContainsKey("ServerAddress") &&
                localSettings.Values.ContainsKey("Username"))
            {
                string savedToken = localSettings.Values["AccessToken"].ToString();
                string savedServer = localSettings.Values["ServerAddress"].ToString();

                string[] sessionData = new string[] { savedToken, savedServer };
                Frame.Navigate(typeof(RoomsPage), sessionData);
            }
        }

        private void Input_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if(e.Key == Windows.System.VirtualKey.Enter)
            {
                LoginButton_Click(this, null);
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string user = UsernameBox.Text;
            string pass = PasswordBox.Password;
            string server = AddressBox.Text;
            string metaID = MetaIdBox.Text;

            StatusText.Text = "Connecting to server...";
            LoginButton.IsEnabled = false;

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // JSON payload
                    string jsonPayload = $"{{\"type\":\"m.login.password\", \"user\":\"{user}\", \"password\":\"{pass}\"}}";
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    // Send POST 
                    // (Using v3 of the Matrix Client-Server API)
                    string loginUrl = "https://" + server + "/_matrix/client/v3/login";
                    HttpResponseMessage response = await client.PostAsync(loginUrl, content);

                    // Process response
                    if (response.IsSuccessStatusCode)
                    {
                        // Success
                        string jsonResponse = await response.Content.ReadAsStringAsync();

                        // Parse for token
                        JsonObject rootObject = JsonObject.Parse(jsonResponse);
                        string accessToken = rootObject.GetNamedString("access_token");
                       // string deviceId = rootObject.GetNamedString("device_id");

                        var localSettings = ApplicationData.Current.LocalSettings;
                        localSettings.Values["AccessToken"] = accessToken;
                        localSettings.Values["ServerAddress"] = server;
                        localSettings.Values["Username"] = user;

                        if (!string.IsNullOrWhiteSpace(metaID))
                        {
                            localSettings.Values["MetaId"] = metaID.Trim();
                        }

                        string[] sessionData = new string[] { accessToken, server };
                        Frame.Navigate(typeof(RoomsPage), sessionData);

                    }
                    else
                    {
                        // Fail
                        StatusText.Text = $"Login failed! Code: {response.StatusCode}";
                        StatusText.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Red);
                    }
                }
            }
            catch (Exception ex)
            {
                // Network error
                StatusText.Text = $"Network Error: {ex.Message}";
                StatusText.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Red);
            }
            finally
            {
                LoginButton.IsEnabled = true;
            }
        }
    }
}
