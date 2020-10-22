
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Celestial_FTP_Client_UWP.Views
{
    public class ftpFileTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Dir { get; set; }
        public DataTemplate File { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            switch (((ftpfile)item).Type)
            {
                case 'd':
                    return Dir;
                default:
                    return File;
            }
        }
    }

    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        private bool panelOpen = false;

        private bool canconnect = true;

        private bool canquit = false;

        private bool connected = false;

        private bool progressing = false;

        private bool progressError = false;

        private bool passive = true;

        private Client _client = null;

        List<ftpfile> ftpfiles = new List<ftpfile>();
        private string focusFile;

        List<string> pathlist = new List<string>();

        public bool PanelOpen
        {
            set { Set(ref panelOpen, value); }
            get { return panelOpen; }
        }

        public bool Canconnect
        {
            set { Set(ref canconnect, value); }
            get { return canconnect; }
        }

        public bool Canquit
        {
            set { Set(ref canquit, value); }
            get { return canquit; }
        }

        public bool Connected
        {
            set { Set(ref connected, value); }
            get { return connected; }
        }

        public bool Progressing
        {
            set { Set(ref progressing, value); }
            get { return progressing; }
        }

        public bool ProgressError
        {
            set { Set(ref progressError, value); }
            get { return progressError; }
        }

        public bool Passive
        {
            set
            {
                Set(ref passive, value);
                if (client != null)
                {
                    client.passiveMode = value;
                }
            }
            get { return passive; }
        }

        public Client client
        {
            get { return _client; }
            set { Set(ref _client, value); }
        }

        private string _path = "Please connect.";

        public string Path
        {
            get { return _path; }
            set
            {
                Set(ref _path, value);
            }
        }

        public List<ftpfile> Ftpfiles { get => ftpfiles; set { Set(ref ftpfiles, value); } }

        public MainPage()
        {
            InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void ChangeCmdEnable()
        {
            Connected = !Connected;
            Progressing = !Progressing;
        }

        private void Set<T>(ref T storage, T value, [CallerMemberName]string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return;
            }

            storage = value;
            OnPropertyChanged(propertyName);
        }

        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public async Task ShowProgressErrorAsync()
        {
            ProgressError = true;
            await Task.Delay(3000);
            Progressing = false;
            ProgressError = false;
        }

        private void RetrieveList_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            PanelOpen = !PanelOpen;
        }

        private async void ConnectServer_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            ConnectDialog connectDialog = new ConnectDialog(this);
            ContentDialogResult a = await connectDialog.ShowAsync();
        }

        public async void ListCmd_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            ChangeCmdEnable();
            Ftpfiles = await client.LIST();
            if(Ftpfiles != null)
            {
                ChangeCmdEnable();
            }
            else
            {
                await ShowProgressErrorAsync();
                connected = true;
            }
        }

        private void FileButton_Click(object sender, RoutedEventArgs e)
        {
            focusFile = (string)(sender as Button).Tag;
            ShowFileMenu(sender, false);
        }

        private void ShowFileMenu(object sender, bool isTransient)
        {
            FlyoutShowOptions myOption = new FlyoutShowOptions();
            myOption.ShowMode = isTransient ? FlyoutShowMode.Transient : FlyoutShowMode.Standard;
            FileFlyout.ShowAt((sender as Button), myOption);
        }

        private async void DirButton_Click(object sender, RoutedEventArgs e)
        {
            ChangeCmdEnable();
            focusFile = (string)(sender as Button).Tag;
            if (await client.CWD(focusFile))
            {
                pathlist.Add(focusFile);
                Ftpfiles = await client.LIST();
                if (Ftpfiles != null)
                {
                    ChangeCmdEnable();
                    Path = " > " + String.Join(" > ", pathlist);
                    return;
                }
            }
            await ShowProgressErrorAsync();
            Connected = true;
        }

        private void DirButton_RightClick(object sender, RoutedEventArgs e)
        {
            focusFile = (string)(sender as Button).Tag;
            ShowDirMenu(sender, false);
        }

        private void ShowDirMenu(object sender, bool isTransient)
        {
            FlyoutShowOptions myOption = new FlyoutShowOptions();
            myOption.ShowMode = isTransient ? FlyoutShowMode.Transient : FlyoutShowMode.Standard;
            DirFlyout.ShowAt((sender as Button), myOption);
        }

        private void OnFlyoutClicked(object sender, RoutedEventArgs e)
        {
            string l = (sender as AppBarButton).Label;
            switch (l)
            {
                case "Download":
                    Download(focusFile);
                    break;
                case "Remove":
                    Remove(focusFile);
                    break;
                case "Delete":
                    Delete(focusFile);
                    break;
                case "Rename":
                    Rename(focusFile);
                    break;
            }
            System.Diagnostics.Debug.WriteLine(focusFile);
            System.Diagnostics.Debug.WriteLine(l);
        }

        private async void Download(string filename)
        {
            string name = filename;
            string suffix = ".txt";
            int index = filename.LastIndexOf(".");
            if (index >= 0)
            {
                name = filename.Substring(0, index);
                suffix = filename.Substring(index, filename.Length - index);
            }
            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            savePicker.SuggestedStartLocation =
                Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;

            savePicker.FileTypeChoices.Add("File", new List<string>() { suffix });
            savePicker.SuggestedFileName = name;

            Windows.Storage.StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                ChangeCmdEnable();
                if (await client.RETR(file, filename))
                {
                    ChangeCmdEnable();
                    //ListCmd_Click(null, null);
                    return;
                }
                ChangeCmdEnable();
                DisplayMessageDialog("Failed to download.");
            }
                
        }

        private async void StoreFile_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");
            Windows.Storage.StorageFile file = await picker.PickSingleFileAsync();
            
            if (file != null)
            {
                ChangeCmdEnable();
                if (await client.STOR(file))
                {
                    ChangeCmdEnable();
                    ListCmd_Click(null, null);
                    return;
                }
                ChangeCmdEnable();
                DisplayMessageDialog("Failed to download.");
            }
        }

        private async void Remove(string name)
        {
            ChangeCmdEnable();
            if(await client.RMD(name))
            {
                ChangeCmdEnable();
                ListCmd_Click(null, null);
                return;
            }
            ChangeCmdEnable();
            DisplayMessageDialog("Failed to delete.");
        }

        private async void Delete(string name)
        {
            ChangeCmdEnable();
            if (await client.DELE(name))
            {
                ChangeCmdEnable();
                ListCmd_Click(null, null);
                return;
            }
            ChangeCmdEnable();
            DisplayMessageDialog("Failed to delete.");
        }

        private async void Rename(string oldname)
        {
            NameDialog nameDialog = new NameDialog(oldname);
            ContentDialogResult a = await nameDialog.ShowAsync();
            if (a.ToString() == "Primary")
            {
                var s = nameDialog.newname;
                if (validName(s))
                {
                    ChangeCmdEnable();
                    if (await client.RNFR(oldname))
                    {
                        if (await client.RNTO(s))
                        {
                            ChangeCmdEnable();
                            ListCmd_Click(null, null);
                            return;
                        }
                    }
                    ChangeCmdEnable();
                    DisplayMessageDialog("Failed to rename.");
                }
            }
        }

        private async void UpperButton_Click(object sender, RoutedEventArgs e)
        {
            if (pathlist.Count != 0)
            {
                ChangeCmdEnable();
                var s = pathlist.Last();
                pathlist.RemoveAt(pathlist.Count - 1);
                var arg = "/" + String.Join("/", pathlist);
                if (await client.CWD(arg))
                {
                    Ftpfiles = await client.LIST();
                    Path = " > " + String.Join(" > ", pathlist);
                    if (Ftpfiles != null)
                    {
                        ChangeCmdEnable();
                        return;
                    }
                }
                else
                {
                    pathlist.Add(s);
                }
                await ShowProgressErrorAsync();
                Connected = true;
            }
        }

        private async void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            NameDialog nameDialog = new NameDialog("NewFolder");
            ContentDialogResult a = await nameDialog.ShowAsync();
            if(a.ToString() == "Primary")
            {
                var s = nameDialog.newname;
                if (validName(s))
                {
                    ChangeCmdEnable();
                    if (await client.MKD(s))
                    {
                        ChangeCmdEnable();
                        ListCmd_Click(null, null);
                        return;
                    }
                    else
                    {
                        ChangeCmdEnable();
                        DisplayMessageDialog("Failed to create new folder");
                    }
                }
            }
        }

        private bool validName(string newname)
        {
            if (newname == "")
            {
                DisplayMessageDialog("Name can't be empty.");
                return false;
            }

            if (newname.Contains("/") || newname.Contains("\\"))
            {
                DisplayMessageDialog("Invalid Name", "Name can't include / and \\");
                return false;
            }

            foreach (var file in ftpfiles)
            {
                if (file.Name == newname)
                {
                    DisplayMessageDialog("Invalid Name", $"\"{newname}\" already exists.");
                    return false;
                }
            }
            return true;
        }

        private async void DisplayMessageDialog(string title, string content = "")
        {
            ContentDialog noWifiDialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "Ok"
            };
            ContentDialogResult result = await noWifiDialog.ShowAsync();
        }

        private async void Quit_Click(object sender, RoutedEventArgs e)
        {
            client.disconnectFile();
            var t = client.QUIT();
            Path = "Please connect.";
            Ftpfiles = null;
            client = null;
            Connected = false;
            Canconnect = true;
            Canquit = false;
            await t;
        }
    }
}
