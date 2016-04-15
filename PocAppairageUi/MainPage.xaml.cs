using System;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace PocAppairageUi
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();

            NoSerieConnecteur.Text = "+SWW387NA6QVFOJ82" + "A0007802277FF";

            InitWithSelector();
        }

        private void InitWithBleWatcher()
        {
            var advertisementWatcher = new BluetoothLEAdvertisementWatcher
            {
                //SignalStrengthFilter = new BluetoothSignalStrengthFilter
                //{
                //    InRangeThresholdInDBm = -100,
                //    OutOfRangeThresholdInDBm = -102,                    
                //}
            };
            advertisementWatcher.ScanningMode = BluetoothLEScanningMode.Active;

            advertisementWatcher.Stopped += AdvertisementWatcherStopped;
            advertisementWatcher.Received += AdvertisementWatcherReceived;

            advertisementWatcher.Start();
        }

        private void AdvertisementWatcherStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
        {
            DisplayLine("stopped");
        }

        private void AdvertisementWatcherReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            DisplayLine("received:");
            DisplayLine(sender.ToString() + args.ToString());
        }

        private void AppairageConnecteurOnClick(object sender, RoutedEventArgs e)
        {
        }

        private async void InitWithSelector()
        {
            string selector = BluetoothDevice.GetDeviceSelector();
            var devices = await DeviceInformation.FindAllAsync(selector);
            foreach (var device in devices)
            {
                var bluetoothDevice = await BluetoothDevice.FromIdAsync(device.Id);
                if (bluetoothDevice != null)
                {
                    Console.Text += (bluetoothDevice.BluetoothAddress).ToString() + "\n"; 
                }

                Console.Text += device.Id + "\n";

                foreach (var property in device.Properties)
                {
                    Console.Text += ("   " + property.Key + " " + property.Value) + "\n";
                }
            }
        }

        private void DisplayLine(string message)
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => Console.Text += message + "\n");
        }
    }
}
