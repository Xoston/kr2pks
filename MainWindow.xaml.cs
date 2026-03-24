using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using kr2pks.Properties;

namespace kr2pks
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<NetworkInterfaceInfo>? networkInterfaces;
        private ObservableCollection<string>? urlHistory;

        public MainWindow()
        {
            InitializeComponent();
            InitializeNetworkInterfaces();
            LoadUrlHistory();
        }

        private void InitializeNetworkInterfaces()
        {
            try
            {
                networkInterfaces = new ObservableCollection<NetworkInterfaceInfo>();
                
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    var interfaceInfo = new NetworkInterfaceInfo
                    {
                        Name = ni.Name,
                        Description = ni.Description,
                        Interface = ni
                    };

                    var ipProperties = ni.GetIPProperties();
                    foreach (var ip in ipProperties.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            interfaceInfo.IPAddress = ip.Address.ToString();
                            interfaceInfo.SubnetMask = ip.IPv4Mask?.ToString() ?? "Нет данных";
                            break;
                        }
                    }

                    interfaceInfo.MacAddress = ni.GetPhysicalAddress().ToString();
                    if (string.IsNullOrEmpty(interfaceInfo.MacAddress))
                        interfaceInfo.MacAddress = "Нет данных";
                    
                    interfaceInfo.Status = ni.OperationalStatus.ToString();
                    
                    interfaceInfo.Speed = FormatSpeed(ni.Speed);
                    interfaceInfo.InterfaceType = ni.NetworkInterfaceType.ToString();
                    
                    networkInterfaces.Add(interfaceInfo);
                }
                
                InterfacesListBox.ItemsSource = networkInterfaces;
            }
            catch (Exception ex)
            {
                ShowStatus($"Ошибка при получении сетевых интерфейсов: {ex.Message}", true);
            }
        }

        private string FormatSpeed(long speed)
        {
            if (speed <= 0) return "Неизвестно";
            
            string[] sizes = { "bps", "Kbps", "Mbps", "Gbps" };
            double len = speed;
            int order = 0;
            
            while (len >= 1000 && order < sizes.Length - 1)
            {
                len /= 1000;
                order++;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }

        private void InterfacesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InterfacesListBox.SelectedItem is NetworkInterfaceInfo selectedInterface)
            {
                DisplayInterfaceInfo(selectedInterface);
            }
        }

        private void DisplayInterfaceInfo(NetworkInterfaceInfo interfaceInfo)
        {
            IpAddressText.Text = interfaceInfo.IPAddress ?? "Нет IPv4 адреса";
            SubnetMaskText.Text = interfaceInfo.SubnetMask ?? "Нет данных";
            MacAddressText.Text = interfaceInfo.MacAddress ?? "Нет данных";
            StatusText.Text = interfaceInfo.Status ?? "Неизвестно";
            SpeedText.Text = interfaceInfo.Speed ?? "Неизвестно";
            TypeText.Text = interfaceInfo.InterfaceType ?? "Неизвестно";
        }

        private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string url = UrlTextBox.Text.Trim();
                if (string.IsNullOrEmpty(url))
                {
                    ShowStatus("Пожалуйста, введите URL", true);
                    return;
                }
                
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    url = "https://" + url;
                }
                
                Uri uri = new Uri(url);
                
                SchemeText.Text = uri.Scheme;
                HostText.Text = uri.Host;
                PortText.Text = uri.Port.ToString();
                PathText.Text = string.IsNullOrEmpty(uri.AbsolutePath) ? "/" : uri.AbsolutePath;
                QueryText.Text = string.IsNullOrEmpty(uri.Query) ? "Нет параметров" : uri.Query.TrimStart('?');
                FragmentText.Text = string.IsNullOrEmpty(uri.Fragment) ? "Нет фрагмента" : uri.Fragment;
                
                AddToHistory(url);
                
                ShowStatus($"URL успешно проанализирован: {url}");
                
                DetermineAddressType(uri.Host);
            }
            catch (UriFormatException ex)
            {
                ShowStatus($"Ошибка формата URL: {ex.Message}", true);
            }
            catch (Exception ex)
            {
                ShowStatus($"Ошибка при анализе URL: {ex.Message}", true);
            }
        }

        private void DetermineAddressType(string host)
        {
            try
            {
                IPAddress[] addresses = Dns.GetHostAddresses(host);
                foreach (var ip in addresses)
                {
                    string type = "";
                    
                    if (IPAddress.IsLoopback(ip))
                        type = "Loopback (локальный)";
                    else if (IsPrivateIP(ip))
                        type = "Локальный (частный)";
                    else
                        type = "Публичный";
                    
                    ShowStatus($"IP: {ip} - Тип: {type}");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Не удалось определить тип адреса: {ex.Message}", true);
            }
        }

        private bool IsPrivateIP(IPAddress ip)
        {
            byte[] bytes = ip.GetAddressBytes();
            
            if (bytes[0] == 10)
                return true;
            
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                return true;
            
            if (bytes[0] == 192 && bytes[1] == 168)
                return true;
            
            return false;
        }

        private async void PingButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string host = UrlTextBox.Text.Trim();
                if (string.IsNullOrEmpty(host))
                {
                    ShowStatus("Пожалуйста, введите URL или хост для проверки", true);
                    return;
                }
                
                if (host.StartsWith("http://") || host.StartsWith("https://"))
                {
                    Uri uri = new Uri(host);
                    host = uri.Host;
                }
                
                ShowStatus($"Выполняется ping для {host}...");
                PingButton.IsEnabled = false;
                
                using (Ping ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(host, 3000);
                    
                    if (reply.Status == IPStatus.Success)
                    {
                        PingResultText.Text = $"✓ Ping успешен: {reply.Address} - время: {reply.RoundtripTime} мс";
                        ShowStatus($"Ping успешен: {reply.RoundtripTime} мс");
                    }
                    else
                    {
                        PingResultText.Text = $"✗ Ping не удался: {reply.Status}";
                        ShowStatus($"Ping не удался: {reply.Status}", true);
                    }
                }
            }
            catch (Exception ex)
            {
                PingResultText.Text = $"✗ Ошибка при выполнении ping: {ex.Message}";
                ShowStatus($"Ошибка при выполнении ping: {ex.Message}", true);
            }
            finally
            {
                PingButton.IsEnabled = true;
            }
        }

        private async void DnsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string host = UrlTextBox.Text.Trim();
                if (string.IsNullOrEmpty(host))
                {
                    ShowStatus("Пожалуйста, введите URL или хост для DNS запроса", true);
                    return;
                }
                if (host.StartsWith("http://") || host.StartsWith("https://"))
                {
                    Uri uri = new Uri(host);
                    host = uri.Host;
                }
                
                ShowStatus($"Получение DNS информации для {host}...");
                DnsButton.IsEnabled = false;
                
                var hostEntry = await Dns.GetHostEntryAsync(host);
                
                string dnsInfo = $"✓ DNS информация для {host}:\n";
                dnsInfo += $"  Каноническое имя: {hostEntry.HostName}\n";
                dnsInfo += $"  IP адреса:\n";
                
                foreach (var ip in hostEntry.AddressList)
                {
                    string type = ip.AddressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6";
                    dnsInfo += $"    - {ip} ({type})\n";
                }
                
                PingResultText.Text = dnsInfo;
                ShowStatus($"DNS информация успешно получена");
            }
            catch (Exception ex)
            {
                PingResultText.Text = $"✗ Ошибка при получении DNS информации: {ex.Message}";
                ShowStatus($"Ошибка при получении DNS информации: {ex.Message}", true);
            }
            finally
            {
                DnsButton.IsEnabled = true;
            }
        }

        private void AddToHistory(string url)
        {
            if (urlHistory == null)
            {
                urlHistory = new ObservableCollection<string>();
                HistoryListBox.ItemsSource = urlHistory;
            }
            
            if (!urlHistory.Contains(url))
            {
                urlHistory.Insert(0, url);
                while (urlHistory.Count > 20)
                {
                    urlHistory.RemoveAt(urlHistory.Count - 1);
                }
                
                SaveUrlHistory();
            }
        }

        private void LoadUrlHistory()
        {
            try
            {
                urlHistory = new ObservableCollection<string>();
                
                if (Settings.Default.UrlHistory != null)
                {
                    foreach (string url in Settings.Default.UrlHistory)
                    {
                        urlHistory.Add(url);
                    }
                }
                
                HistoryListBox.ItemsSource = urlHistory;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки истории: {ex.Message}");
            }
        }

        private void SaveUrlHistory()
        {
            try
            {
                Settings.Default.UrlHistory = new System.Collections.Specialized.StringCollection();
                
                if (urlHistory != null)
                {
                    foreach (string url in urlHistory)
                    {
                        Settings.Default.UrlHistory.Add(url);
                    }
                }
                
                Settings.Default.Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения истории: {ex.Message}");
            }
        }

        private void HistoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HistoryListBox.SelectedItem is string selectedUrl)
            {
                UrlTextBox.Text = selectedUrl;
                AnalyzeButton_Click(sender, e);
            }
        }

        private void ShowStatus(string message, bool isError = false)
        {
            StatusTextBlock.Text = message;
            StatusTextBlock.Foreground = isError ? 
                System.Windows.Media.Brushes.Red : 
                System.Windows.Media.Brushes.Black;
        }
    }

    public class NetworkInterfaceInfo
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? IPAddress { get; set; }
        public string? SubnetMask { get; set; }
        public string? MacAddress { get; set; }
        public string? Status { get; set; }
        public string? Speed { get; set; }
        public string? InterfaceType { get; set; }
        public NetworkInterface? Interface { get; set; }
        
        public override string ToString()
        {
            return $"{Name} - {IPAddress ?? "Нет IP"}";
        }
    }
}