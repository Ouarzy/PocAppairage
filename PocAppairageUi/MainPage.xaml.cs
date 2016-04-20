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
        private const string AdresseMac = "0007802B217E";
        private string _numeroSerie;

        private Windows.Devices.Bluetooth.Rfcomm.RfcommDeviceService _service;
        private StreamSocket _socket;
        private DataWriter dataWriterObject;
        private DataReader dataReaderObject;
        private CancellationTokenSource ReadCancellationTokenSource;

        private DeviceInformation deviceInfo;

        public MainPage()
        {
            InitializeComponent();

            _numeroSerie = CodePin + Constructeur + AdresseMac;
            NoSerieConnecteur.Text = _numeroSerie;

        }

        private async void TenterListage()
        {
            try
            {
                DisplayLine("Début du scan...");

                var rfCommAqs = "System.Devices.DevObjectType:=5 AND System.Devices.AepService.ProtocolId:= \"{00030000-0000-1000-8000-00805F9B34FB}\"";
                var BLEothAqs = "System.Devices.DevObjectType:=5 AND System.Devices.Aep.ProtocolId:=\"{BB7BB05E-5972-42B5-94FC-76EAA7084D49}\"";
                var BToothAqs1 = "System.Devices.DevObjectType:=5";

                //var deviceswatcher = DeviceInformation.CreateWatcher(BToothAqs1);
                //deviceswatcher.Added += DeviceswatcherOnAdded;
                //deviceswatcher.Updated += DeviceswatcherOnUpdated;
                //deviceswatcher.Stopped += DeviceswatcherOnStopped;
                //deviceswatcher.EnumerationCompleted += DeviceswatcherOnEnumerationCompleted;
                //deviceswatcher.Start();

                //target MAC in decimal, this number corresponds to my device (00:07:80:4b:29:ed)

                //AND System.DeviceInterface.Bluetooth.DeviceAddress:=\"6002927E3645\"
                //AND System.Devices.Aep.ProtocolId:=\"{E0CBF06C-CD8B-4647-BB8A-263B43F0F974}\"

                var targetMac = ulong.Parse(AdresseMac, System.Globalization.NumberStyles.HexNumber);

                var serialPort = "System.DeviceInterface.Bluetooth.ServiceGuid:=\"{00001101-0000-1000-8000-00805F9B34FB}\" AND System.DeviceInterface.Bluetooth.DeviceAddress:=\"0007802277FF\"";
                var connecteurFilter = "System.DeviceInterface.Bluetooth.DeviceAddress:=\"6002927E3645\"";



                var selector1 = BluetoothDevice.GetDeviceSelectorFromDeviceName("Linky-C");
                var devices1 = await DeviceInformation.FindAllAsync(selector1);
                if (!devices1.Any())
                {
                    DisplayLine("Pas de connecteur trouvé");
                }
                else
                {
                    foreach (var device in devices1)
                    {
                        DisplayLine("Trouvé : " + device.Name + "(type : " + device.Kind.ToString() + ")");

                        deviceInfo = await DeviceInformation.CreateFromIdAsync(device.Id);
                    }
                }


            }
            catch (Exception ex)
            {
                DisplayLine(ex.Message);
            }
        }

        private bool _pairing = false;

        private async void TenterAppairage(DeviceInformation device)
        {

            try
            {
                if (!_pairing)
                {
                    _pairing = true;

                    var customPairing = device.Pairing.Custom;

                    customPairing.PairingRequested += PairingRequestedHandler;
                    var result = await customPairing.PairAsync(DevicePairingKinds.ProvidePin, DevicePairingProtectionLevel.None);
                    customPairing.PairingRequested -= PairingRequestedHandler;

                    DisplayLine("Résultat de l'appairage : " + result.Status);
                }
            }
            catch (Exception ex)
            {
                DisplayLine(ex.Message);
                _pairing = false;
            }

        }

        private async void TenterCommuniquage(DeviceInformation device)
        {
            try
            {
                //success
                _service = await RfcommDeviceService.FromIdAsync(device.Id);
                _socket = new StreamSocket();
                await _socket.ConnectAsync(_service.ConnectionHostName, _service.ConnectionServiceName);
            }
            catch (Exception ex)
            {
                DisplayLine(ex.Message);
                _pairing = false;
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

        private void AppairerOnClick(object sender, RoutedEventArgs e)
        {
            if (deviceInfo != null)
            {
                Task.Run(() => TenterAppairage(deviceInfo));
            }
        }

        private void ListerOnClick(object sender, RoutedEventArgs e)
        {
            Task.Run(() => TenterListage());
        }
        private void CommuniquerOnClick(object sender, RoutedEventArgs e)
        {
            if (deviceInfo != null)
            {
                Task.Run(() => TenterCommuniquage(deviceInfo));
            }
        }
        private void DisplayLine(string message)
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => Console.Text += message + "\n");
        }
    }
}
