using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using System.Net.Http;
using System.Text;
using Windows.Data.Json;

using Windows.Storage;


using Windows.Foundation.Metadata;

using Windows.Security.Credentials;

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

            // if (localSettings.Values.ContainsKey("AccessToken") &&
            //     localSettings.Values.ContainsKey("ServerAddress") &&
            //    localSettings.Values.ContainsKey("Username"))
            //{
            //  string savedToken = localSettings.Values["AccessToken"].ToString();
            //string savedServer = localSettings.Values["ServerAddress"].ToString();

            // string[] sessionData = new string[] { savedToken, savedServer };
            // Frame.Navigate(typeof(RoomsPage), sessionData);
            //}

            if (localSettings.Values.ContainsKey("ServerAddress"))
            {
                try
                {
                    var vault = new PasswordVault();
                    var credentialList = vault.FindAllByResource("WindowsPhoneMatrix");

                    if (credentialList.Count > 0)
                    {
                        var cred = credentialList[0];
                        cred.RetrievePassword();

                        string savedToken = cred.Password;
                        string savedServer = localSettings.Values["ServerAddress"].ToString();

                        string[] sessionData = new string[] { savedToken, savedServer };
                        Frame.Navigate(typeof(RoomsPage), sessionData);
                    }
                }
                catch (Exception)
                {

                }
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
                    string cleanServer = server.Trim();
                    if (cleanServer.Contains("/") || cleanServer.Contains(" ") || cleanServer.Contains("?"))
                    {
                        StatusText.Text = "Invalid server address. Please enter a domain or IP only.";
                        LoginButton.IsEnabled = true;
                        return;
                    }

                    JsonObject loginJson = new JsonObject();
                    loginJson.SetNamedValue("type", JsonValue.CreateStringValue("m.login.password"));
                    loginJson.SetNamedValue("user", JsonValue.CreateStringValue(user));
                    loginJson.SetNamedValue("password", JsonValue.CreateStringValue(pass));

                    string jsonPayload = loginJson.Stringify();
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    string loginUrl = $"https://{cleanServer}/_matrix/client/v3/login";


                    HttpResponseMessage response = await client.PostAsync(loginUrl, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResponse = await response.Content.ReadAsStringAsync();

                        JsonObject rootObject = JsonObject.Parse(jsonResponse);
                        string accessToken = rootObject.GetNamedString("access_token");
                        // string deviceId = rootObject.GetNamedString("device_id");

                        var vault = new PasswordVault();
                        vault.Add(new PasswordCredential("WindowsPhoneMatrix", user, accessToken));

                        var localSettings = ApplicationData.Current.LocalSettings;
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
                        StatusText.Text = $"Login failed! Code: {response.StatusCode}";
                        StatusText.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Red);
                    }
                }
            }
            catch (Exception ex)
            {
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
