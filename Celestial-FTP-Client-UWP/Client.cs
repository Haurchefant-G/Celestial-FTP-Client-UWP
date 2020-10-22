using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Media.Audio;
using Windows.Storage;
using Windows.Storage.Provider;
using Windows.UI.Xaml.Controls;

namespace Celestial_FTP_Client_UWP
{

    public enum ClientStatus : int
    {
        START_STATUS,
        VISITOR_STATUS,
        WAIT_PASSWORD_STATUS,
        LOGIN_STATUS,
        PORT_STATUS,
        PASV_STATUS,
        TRANS_STATUS
    };

    public class ftpfile
    {
        public ftpfile(char t, string n)
        {
            Type = t;
            Name = n;
        }

        private char type;
        private string name;

        public char Type { get => type; set => type = value; }
        public string Name { get => name; set => name = value; }
    }

    public class Client
    {

        public class RecvArgs : EventArgs
        {
            public string Data { get; set; }
            public RecvArgs(string data)
            {
                Data = data;
            }
        }

        static int CLIENT_STATUS = 0x0f;
        static int RENAME_STATUS = 0x10;

        public string IP { get; set; }
        public int Port { get; set; }
        public string username { get; set; }
        public string password { get; set; }
        public ClientStatus status { get; set; }
        public bool passiveMode { get; set; } = false;
        public TcpListener listener { get; private set; } = null;
        public string responseStr { get; private set; }
        public string serverip { get; private set; }
        public int serverport { get; private set; }
        public TcpClient fileClient { get; private set; }
        public NetworkStream filens { get; private set; }

        TcpClient cmdClient;
        private NetworkStream ns;

        static int fileport = 20358;

        public event EventHandler<string> Received;

        public event EventHandler<string> Send;


        //public delegate int HandleRecv(string i);

        EventHandler<string> onReceived = (sender, eventArgs) =>
        {
            Console.WriteLine(eventArgs);
            System.Diagnostics.Debug.WriteLine(eventArgs);
        };

        EventHandler<string> onSend = (sender, eventArgs) =>
        {
            Console.ReadLine();
            System.Diagnostics.Debug.WriteLine(eventArgs);
        };

        public Client()
        {
            cmdClient = new TcpClient();
        }

        public Client(int ip0 = 127, int ip1 = 0, int ip2 = 0, int ip3 = 1, int port = 21)
        {
            IP = $"{ip0}.{ip1}.{ip2}.{ip3}";
            Port = port;
            cmdClient = new TcpClient();
        }

        public Client(string ip = "127.0.0.1", int port = 21)
        {
            cmdClient = new TcpClient(ip, port);
            ns = cmdClient.GetStream();
            Received += onReceived;
            Send += onSend;
        }

        private async Task<bool> connectAsync()
        {
            try
            {
                await cmdClient.ConnectAsync(IP, Port);
                ns = cmdClient.GetStream();
                Received += onReceived;
                Send += onSend;
                if (await recv_responseAsync() == 220)
                {
                    status = ClientStatus.VISITOR_STATUS;
                    return true;
                }
            }
            catch (Exception e)
            {

            }
            return false;
        }

        public static async Task<Client> ConnectServer(int ip0, int ip1, int ip2, int ip3, int port, string username, string password)
        {
            Client ftpclient = new Client(ip0, ip1, ip2, ip3, port);

            if (await ftpclient.connectAsync())
            {
                if (await ftpclient.login(username, password))
                {
                    return ftpclient;
                }
                else
                {
                    return null;
                }
            }
            return null;
        }

        async Task<int> recv_responseAsync()
        {
            if (ns.CanRead)
            {
                // Reads NetworkStream into a byte buffer.
                byte[] bytes = new byte[cmdClient.ReceiveBufferSize];

                // Read can return anything from 0 to numBytesToRead.
                // This method blocks until at least one byte is read.
                await ns.ReadAsync(bytes, 0, (int)cmdClient.ReceiveBufferSize);

                // Returns the data received from the host to the console.
                responseStr = Encoding.UTF8.GetString(bytes);
                int code;
                int.TryParse(responseStr.Substring(0, 3), out code);
                Received?.Invoke(this, responseStr);
                Received?.Invoke(this, code.ToString());
                return code;
            }
            return 0;
        }

        async Task send_commandAsync(string cmd)
        {
            if (ns.CanWrite)
            {
                Byte[] sendBytes = Encoding.UTF8.GetBytes(cmd);
                await ns.WriteAsync(sendBytes, 0, sendBytes.Length);
                Send?.Invoke(this, cmd);
            }
        }

        public async Task<bool> login(string user, string password)
        {
            await USER(user);
            await recv_responseAsync();
            await PASS(password);
            await recv_responseAsync();
            await SYST();
            await TYPE("I");
            await recv_responseAsync();
            return true;
        }

        async Task USER(string user)
        {
            await send_commandAsync($"USER {user}\r\n");
        }

        async Task PASS(string password)
        {
            await send_commandAsync($"PASS {password}\r\n");
        }

        async Task<string> SYST()
        {
            await send_commandAsync($"SYST\r\n");
            await recv_responseAsync();
            return responseStr.Substring(4, responseStr.Length - 4);
        }

        async Task TYPE(string type)
        {
            await send_commandAsync($"TYPE {type}\r\n");
        }

        public async Task<int> QUIT()
        {
            await send_commandAsync($"QUIT\r\n");
            int code = await recv_responseAsync();
            cmdClient.Close();
            cmdClient.Dispose();
            return code;
        }

        async Task ABOR()
        {
            await send_commandAsync($"ABOR\r\n");
        }

        public void disconnectFile()
        {
            serverip = null;
            if (listener != null)
            {
                listener.Stop();
                listener = null;
            }
            if (fileClient != null)
            {
                fileClient.Close();
                fileClient.Dispose();
                fileClient = null;
                filens = null;
            }
            serverport = 0;
        }

        async Task<bool> PORTorPASV()
        {
            disconnectFile();
            Task<bool> i = passiveMode ? PASV(): PORT();
            return await i;
        }

        async Task<bool> PORT()
        {
            IPEndPoint i = (IPEndPoint)cmdClient.Client.LocalEndPoint;
            string ip = i.Address.ToString().Replace('.', ',');
            int port = ++fileport;
            bool success = false;
            while(!success)
            {
                try
                {
                    listener = new TcpListener(IPAddress.Any, port);
                    success = true;
                }
                catch (Exception e)
                {
                    ++port;
                }
            }

            listener.Start();
            string s = $"{ip},{port / 256},{port % 256}";
            await send_commandAsync($"PORT {s}\r\n");
            if (await recv_responseAsync() == 200)
            {
                return true;
            }
            return false;
        }

        async Task<bool> PASV()
        {
            await send_commandAsync($"PASV\r\n");
            if (await recv_responseAsync() == 227)
            {
                string pattern = @"\d+";
                MatchCollection nums = Regex.Matches(responseStr, pattern);
                if (nums.Count == 7)
                {
                    serverip = $"{nums[1].Value}.{nums[2].Value}.{nums[3].Value}.{nums[4].Value}";
                    serverport = int.Parse(nums[5].Value) * 256 + int.Parse(nums[6].Value);
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> RETR(Windows.Storage.StorageFile file, string filename)
        {

            Windows.Storage.CachedFileManager.DeferUpdates(file);


            if (await PORTorPASV())
            {
                var i = recv_responseAsync();
                Task<TcpClient> acceptTask;
                if (passiveMode)
                {
                    fileClient = new TcpClient(serverip, serverport);
                    await send_commandAsync($"RETR {filename}\r\n");
                    if (await i != 150)
                    {
                        await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(file);
                        return false;
                    }
                }
                else
                {
                    acceptTask = listener.AcceptTcpClientAsync();
                    await send_commandAsync($"RETR {filename}\r\n");
                    if (await i != 150)
                    {
                        await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(file);
                        return false;
                    }
                    fileClient = await acceptTask;
                }

                filens = fileClient.GetStream();

                if (filens.CanRead)
                {

                    byte[] bytes = new byte[fileClient.ReceiveBufferSize];

                    int n = 0;

                    using (var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
                    {
                        using (var outputStream = stream.GetOutputStreamAt(0))
                        {
                            using (var dataWriter = new Windows.Storage.Streams.DataWriter(outputStream))
                            {
                                do
                                {
                                    try
                                    {
                                        n = await filens.ReadAsync(bytes, 0, (int)fileClient.ReceiveBufferSize);
                                        dataWriter.WriteBytes(bytes.Take(n).ToArray());
                                    }
                                    catch (Exception e)
                                    {
                                        disconnectFile();
                                        await dataWriter.StoreAsync();
                                        await outputStream.FlushAsync();
                                        await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(file);
                                        return false;
                                    }
                                } while (n > 0);
                                await dataWriter.StoreAsync();
                                await outputStream.FlushAsync();
                                disconnectFile();
                                int code = await recv_responseAsync();
                            }
                        }
                    }
                }
                await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(file);
                disconnectFile();
                return true;
            }

            Windows.Storage.Provider.FileUpdateStatus status =
                    await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(file);
            disconnectFile();
            return false;
        }

        public async Task<bool> STOR(Windows.Storage.StorageFile file)
        {

            if (await PORTorPASV())
            {
                var i = recv_responseAsync();
                Task<TcpClient> acceptTask;
                if (passiveMode)
                {
                    fileClient = new TcpClient(serverip, serverport);
                    await send_commandAsync($"STOR {file.Name}\r\n");
                    if (await i != 150)
                    {
                        return false;
                    }
                }
                else
                {
                    acceptTask = listener.AcceptTcpClientAsync();
                    await send_commandAsync($"STOR {file.Name}\r\n");
                    if (await i != 150)
                    {
                        return false;
                    }
                    fileClient = await acceptTask;
                }

                filens = fileClient.GetStream();

                if (filens.CanRead)
                {

                    byte[] bytes = new byte[fileClient.ReceiveBufferSize];

                    uint n = 0;

                    using (var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read))
                    {
                        using (var inputStream = stream.GetInputStreamAt(0))
                        {
                            using (var dataReader = new Windows.Storage.Streams.DataReader(inputStream))
                            {
                                do
                                {
                                    try
                                    {
                                        n = await dataReader.LoadAsync((uint)fileClient.ReceiveBufferSize);
                                        if (n != 0)
                                        {
                                            var buf = dataReader.ReadBuffer(n);
                                            await filens.WriteAsync(buf.ToArray(), 0, (int)n);
                                        }
                                        else
                                        {
                                            await filens.WriteAsync(bytes, 0, (int)n);
                                        }
                                    }
                                    catch(Exception e)
                                    {
                                        disconnectFile();
                                        return false;
                                    }
                                } while (n > 0);
                                disconnectFile();
                                int code = await recv_responseAsync();
                            }
                        }
                    }
                    disconnectFile();
                    return true;
                }
            }
            disconnectFile();
            return false;
        }


        public async Task<bool> MKD(string dir)
        {
            await send_commandAsync($"MKD {dir}\r\n");
            if(await recv_responseAsync() == 257)
            {
                return true;
            }
            return false;
        }

        public async Task<bool> CWD(string dir)
        {
            await send_commandAsync($"CWD {dir}\r\n");
            if(await recv_responseAsync() == 250)
            {
                return true;
            }
            return false;
        }

        public async Task<string> PWD()
        {
            await send_commandAsync($"PWD\r\n");
            if (await recv_responseAsync() == 250)
            {
                return responseStr;
            }
            return "Error";
        }

        public async Task<List<ftpfile>> LIST(string path = "./")
        {
            var filelist = new List<ftpfile>();
            if (await PORTorPASV())
            {
                var i = recv_responseAsync();
                Task<TcpClient> acceptTask;
                if (passiveMode)
                {
                    fileClient = new TcpClient(serverip, serverport);
                    await send_commandAsync($"LIST {path}\r\n");
                    if (await i != 150)
                    {
                        return null;
                    }
                }
                else
                {
                    acceptTask = listener.AcceptTcpClientAsync();
                    await send_commandAsync($"LIST {path}\r\n");
                    if (await i != 150)
                    {
                        return null;
                    }
                    fileClient = await acceptTask;
                }

                filens = fileClient.GetStream();
                // Create sample file; replace if exists.
                //Windows.Storage.StorageFolder
                if (filens.CanRead)
                {

                    byte[] bytes = new byte[fileClient.ReceiveBufferSize];

                    int n = 0;

                    StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
                    StorageFile listResult = await storageFolder.CreateFileAsync("listresult.txt", CreationCollisionOption.ReplaceExisting);

                    using (var stream = await listResult.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
                    {
                        using (var outputStream = stream.GetOutputStreamAt(0))
                        {
                            using (var dataWriter = new Windows.Storage.Streams.DataWriter(outputStream))
                            {
                                do
                                {
                                    try
                                    {
                                        n = await filens.ReadAsync(bytes, 0, (int)fileClient.ReceiveBufferSize);
                                        dataWriter.WriteBytes(bytes.Take(n).ToArray());
                                    }
                                    catch (Exception e)
                                    {
                                        disconnectFile();
                                        await dataWriter.StoreAsync();
                                        await outputStream.FlushAsync();
                                        return filelist;
                                    }
                                } while (n > 0);
                                await dataWriter.StoreAsync();
                                await outputStream.FlushAsync();
                                int code = await recv_responseAsync();
                            }
                        }
                    }

                    string s;
                    using (Stream file = await listResult.OpenStreamForReadAsync())
                    {
                        using (StreamReader read = new StreamReader(file))
                        {
                            while(!read.EndOfStream)
                            {
                                s = read.ReadLine();
                                System.Diagnostics.Debug.WriteLine(s);
                                var infos = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                var f = new ftpfile(infos[0][0], infos.Last());
                                filelist.Add(f);
                            }
                        }
                    }
                    disconnectFile();
                    return filelist;
                }
                
            }
            disconnectFile();
            return null;

        }

        public async Task<bool> DELE(string file)
        {
            await send_commandAsync($"DELE {file}\r\n");
            if (await recv_responseAsync() == 250)
            {
                return true;
            }
            return false;
        }

        public async Task<bool> RMD(string dir)
        {
            await send_commandAsync($"RMD {dir}\r\n");
            if (await recv_responseAsync() == 250)
            {
                return true;
            }
            return false;
        }

        public async Task<bool> RNFR(string path)
        {
            await send_commandAsync($"RNFR {path}\r\n");
            if (await recv_responseAsync() == 350)
            {
                return true;
            }
            return false;
        }

        public async Task<bool> RNTO(string path)
        {
            await send_commandAsync($"RNTO {path}\r\n");
            if (await recv_responseAsync() == 250)
            {
                return true;
            }
            return false;
        }
    }
}
