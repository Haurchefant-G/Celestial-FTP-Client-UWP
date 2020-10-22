using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“内容对话框”项模板

namespace Celestial_FTP_Client_UWP.Views
{
    public enum ConnectResult : int
    {
        INVALID_ADDRESS,
        UNSUPPORTED_USERNAME_OR_PASSWORD,
        CONNECT_OK
    }
    public sealed partial class ConnectDialog : ContentDialog
    {
        public ConnectResult Result { get; set; }
        public Client client;
        public MainPage mainpage;


        //public ConnectDialog()
        //{

        //    this.InitializeComponent();
        //}

        public ConnectDialog(MainPage page)
        {

            this.InitializeComponent();
            mainpage = page;
        }

        private async void ConnectDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            mainpage.Progressing = true;
            Task<Client> connect = Client.ConnectServer((int)ip0.Value, (int)ip1.Value, (int)ip2.Value, (int)ip3.Value, (int)port.Value, username.Text, password.Password);;
            List<Task> tlist = new List<Task>();
            tlist.Add(connect);
            tlist.Add(Task.Delay(1000));
            Task tall = Task.WhenAll(tlist);
            await tall;
            client = await connect;
            if (client != null)
            {
                client.passiveMode = mainpage.Passive;
                mainpage.client = client;
                mainpage.Path = " > ";
                mainpage.Canquit = true;
                mainpage.Canconnect = false;
                mainpage.Connected = true;
                mainpage.Progressing = false;
                mainpage.ListCmd_Click(null, null);
            }
            else
            {
                await mainpage.ShowProgressErrorAsync();
            }
        }

        private void ConnectDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            return;
        }
    }
}
