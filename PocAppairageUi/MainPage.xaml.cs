using System;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace PocAppairageUi
{
    /// <summary>
    /// Chercher des infos ici:
    /// https://github.com/Microsoft/Windows-universal-samples/tree/master/Samples/DeviceEnumerationAndPairing
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private string _numeroSerie;

        public MainPage()
        {
            InitializeComponent();

            _numeroSerie = "+SWW387NA6QVFOJ82" + "A0007802277FF";
            NoSerieConnecteur.Text = _numeroSerie;

            TryPairingWithWatcher();
        }

        private void TryPairingWithWatcher()
        {
            var watcher = DeviceInformation.CreateWatcher();

            var handlerAdded = new TypedEventHandler<DeviceWatcher, DeviceInformation>(async (watch, deviceInfo) =>
            {
                // Since we have the collection databound to a UI element, we need to update the collection on the UI thread.
                await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    DisplayLine("Devices found");
                    DisplayLine(deviceInfo.Id);
                    DisplayLine(deviceInfo.Name);
                });
            });
            watcher.Added += handlerAdded;
            watcher.Stopped +=WatcherOnStopped;
            watcher.Updated += WatcherOnUpdated;
            watcher.Start();
        }



        private void WatcherOnStopped(DeviceWatcher sender, object args)
        {
            DisplayLine("stopped");
        }

        private void WatcherOnUpdated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            DisplayLine("updated:");
            DisplayLine(sender.ToString() + args.ToString());
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
            try
            {
                var connecteur = await BluetoothDevice.FromBluetoothAddressAsync(4567890);

                DisplayLine(connecteur.DeviceId);
                DisplayLine(connecteur.ConnectionStatus.ToString());
            }
            catch (Exception ex)
            {
                DisplayLine(ex.Message);
            }


            //string selector = BluetoothDevice.GetDeviceSelector();
            //var devices = BluetoothDevice.GetDeviceSelector(selector);
            //foreach (var device in devices)
            //{
            //    var bluetoothDevice = await BluetoothDevice.FromIdAsync(device.Id);
            //    if (bluetoothDevice != null)
            //    {
            //        DisplayLine(bluetoothDevice.BluetoothAddress.ToString());
            //    }

            //    DisplayLine(device.Id);

            //    foreach (var property in device.Properties)
            //    {
            //        DisplayLine("   " + property.Key + " " + property.Value);
            //    }
            //}
        }

        private void DisplayLine(string message)
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => Console.Text += message + "\n");
        }
    }
}
