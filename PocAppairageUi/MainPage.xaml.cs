using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
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
        private const string CodePin = "+SWW387NA6QVFOJ8";
        private const string Constructeur = "2A";
        private const string AdresseMac = "0007802277FF";
        private string _numeroSerie;

        private Windows.Devices.Bluetooth.Rfcomm.RfcommDeviceService _service;
        private StreamSocket _socket;
        private DataWriter dataWriterObject;
        private DataReader dataReaderObject;
        private CancellationTokenSource ReadCancellationTokenSource;


        public MainPage()
        {
            InitializeComponent();

            _numeroSerie = CodePin + Constructeur + AdresseMac;
            NoSerieConnecteur.Text = _numeroSerie;

            //Advertisement();
            //InitializeRfcommDeviceService();
            Task.Run(() => TryCustomPairing());
            //RunWatcher();
        }

        private void Advertisement()
        {
            Task.Run(() =>
            {
                var advertisement = new BluetoothLEAdvertisementWatcher();
                advertisement.Stopped += AdvertisementWatcherStopped;
                advertisement.Received += AdvertisementWatcherReceived;

                advertisement.Start();
            });
        }

        private async void InitializeRfcommDeviceService()
        {
            try
            {
                var deviceSelector = RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort);
                var deviceInfoCollection = await DeviceInformation.FindAllAsync(deviceSelector);
                var numDevices = deviceInfoCollection.Count();

                // By clearing the backing data, we are effectively clearing the ListBox
                var _pairedDevices = new List<DeviceInformation>();

                if (numDevices == 0)
                {
                    //MessageDialog md = new MessageDialog("No paired devices found", "Title");
                    //await md.ShowAsync();
                    DisplayLine("InitializeRfcommDeviceService: No paired devices found.");
                }
                else
                {
                    // Found paired devices.
                    foreach (var deviceInfo in deviceInfoCollection)
                    {
                        if (!deviceInfo.Name.Contains("Ouarzy"))
                        {
                            DisplayLine(deviceInfo.Name);
                            DisplayLine(deviceInfo.Id);
                            _pairedDevices.Add(deviceInfo);

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DisplayLine(ex.Message);
            }
        }


        private async void TryCustomPairing()
        {
            try
            {
                var deviceName = "Ouarzy Phone";
                var selector = BluetoothDevice.GetDeviceSelectorFromDeviceName(deviceName);
                var devices = await DeviceInformation.FindAllAsync(selector);
                if (!devices.Any())
                {
                        DisplayLine("No BluetoothDevice found");
                }
                else
                {
                    foreach (var device in devices)
                    {
                        DisplayLine(device.Id);
                        DisplayLine(device.Name);
                    }
                }

                var selector2 = BluetoothLEDevice.GetDeviceSelectorFromDeviceName("Charge HR");
                var devices2 = await DeviceInformation.FindAllAsync(selector2);
                if (!devices2.Any())
                {
                    DisplayLine("No BluetoothLEDevice found");
                }
                else
                {
                    foreach (var device in devices2)
                    {
                        DisplayLine(device.Id);
                        DisplayLine(device.Name);
                    }
                }

                var selector3 = RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort);
                var devices3 = await DeviceInformation.FindAllAsync(selector3);
                if (!devices3.Any())
                {
                    DisplayLine("No RfcommDeviceService found");
                }
                else
                {
                    foreach (var device in devices3)
                    {
                        DisplayLine(device.Id);
                        DisplayLine(device.Name);
                    }
                }

            }
            catch (Exception ex)
            {
                DisplayLine(ex.Message);
            }
        }

        private async void PairingRequestedHandler(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
        {
            switch (args.PairingKind)
            {
                case DevicePairingKinds.ConfirmOnly:
                    // Windows itself will pop the confirmation dialog as part of "consent" if this is running on Desktop or Mobile
                    // If this is an App for 'Windows IoT Core' where there is no Windows Consent UX, you may want to provide your own confirmation.
                    DisplayLine("ConfirmOnly");
                    args.Accept();
                    break;

                case DevicePairingKinds.DisplayPin:
                    // We just show the PIN on this side. The ceremony is actually completed when the user enters the PIN
                    // on the target device. We automatically except here since we can't really "cancel" the operation
                    // from this side.
                    args.Accept();

                    // No need for a deferral since we don't need any decision from the user
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        DisplayLine("Please enter this PIN on the device you are pairing with: " + args.Pin);

                    });
                    break;

                case DevicePairingKinds.ProvidePin:
                    // A PIN may be shown on the target device and the user needs to enter the matching PIN on 
                    // this Windows device. Get a deferral so we can perform the async request to the user.
                    var collectPinDeferral = args.GetDeferral();

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        DisplayLine("ProvidePin");
                        args.Accept(CodePin);
                    });
                    break;

                case DevicePairingKinds.ConfirmPinMatch:
                    // We show the PIN here and the user responds with whether the PIN matches what they see
                    // on the target device. Response comes back and we set it on the PinComparePairingRequestedData
                    // then complete the deferral.
                    var displayMessageDeferral = args.GetDeferral();

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        DisplayLine("ConfirmPinMatch");
                        args.Accept();
                        displayMessageDeferral.Complete();
                    });
                    break;
            }
        }

        private void RunWatcher()
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

            var handlerUpdated = new TypedEventHandler<DeviceWatcher, DeviceInformationUpdate>(async (watch, deviceInfo) =>
            {
                // Since we have the collection databound to a UI element, we need to update the collection on the UI thread.
                await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    DisplayLine("Devices update");
                    DisplayLine(deviceInfo.Id);
                    DisplayLine(deviceInfo.Kind.ToString());
                });
            });


            watcher.Added += handlerAdded;
            watcher.Updated += handlerUpdated;
            watcher.Stopped += WatcherOnStopped;
            watcher.Start();
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

        private bool _pairing = true;
        private async void AdvertisementWatcherReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            DisplayLine("received:");

            try
            {
                DisplayLine(args.BluetoothAddress.ToString());
                var device = await BluetoothDevice.FromBluetoothAddressAsync(args.BluetoothAddress);
                DisplayLine("nor: " + device.Name);

                if (!_pairing)
                {
                    _pairing = true;

                    var device2 = await BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress);
                    DisplayLine("ble: " + device2.Name);

                    var customPairing = device2.DeviceInformation.Pairing.Custom;

                    customPairing.PairingRequested += PairingRequestedHandler;
                    var result = await customPairing.PairAsync(DevicePairingKinds.ProvidePin, DevicePairingProtectionLevel.None);
                    customPairing.PairingRequested -= PairingRequestedHandler;

                    DisplayLine("pairing: " + result.Status);

                    var bloodPressureService = await GattDeviceService.FromIdAsync(device2.DeviceId);
                    GattCharacteristic bloodPressureCharacteristic =
                        bloodPressureService.GetCharacteristics(GattCharacteristicUuids.BloodPressureFeature)[0];

                    bloodPressureCharacteristic.ValueChanged += bloodPressureChanged;
                }
            }
            catch (Exception ex)
            {
                DisplayLine(ex.Message);
                _pairing = false;
            }
        }

        private void bloodPressureChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            DisplayLine("Something");
        }

        private void WatcherOnStopped(DeviceWatcher sender, object args)
        {
            DisplayLine("stopped");
        }


        private IBackgroundTrigger TryFindDevices()
        {
            var rfcommConnectionTrigger = new RfcommConnectionTrigger();
            return rfcommConnectionTrigger;
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
