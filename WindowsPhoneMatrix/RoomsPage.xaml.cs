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

namespace WindowsPhoneMatrix
{
    public sealed partial class RoomsPage : Page
    {
        private string _accessToken;
        private string _serverAddress;

        public RoomsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            string[] sessionData = e.Parameter as string[]; //cast parameter to array

            if (sessionData != null){
                _accessToken = sessionData[0];
                _accessToken = sessionData[1];
                WelcomeText.Text = $"Connected to {_serverAddress}!\nToken: {_accessToken.Substring(0, 8)}...";
            }
        }
    }
}
