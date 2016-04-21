using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Foundation;
using Windows.Media.MediaProperties;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Buffer = Windows.Storage.Streams.Buffer;

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

                //target MAC in decimal, this number corresponds to my bluetoothDeviceInfo (00:07:80:4b:29:ed)

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
                        DisplayLine("Trouvé : " + device.Name + " (type : " + device.Kind.ToString() + ")");

                        deviceInfo = await DeviceInformation.CreateFromIdAsync(device.Id);
                    }
                }
                DisplayLine("Fin du scan");

            }
            catch (Exception ex)
            {
                DisplayLine("Erreur lors du scan : " + ex.Message);
            }
        }

        private async void TenterAppairage(DeviceInformation device)
        {

            try
            {
                DisplayLine("Début de l'appairage...");

                var customPairing = device.Pairing.Custom;

                customPairing.PairingRequested += PairingRequestedHandler;
                var result = await customPairing.PairAsync(DevicePairingKinds.ProvidePin, DevicePairingProtectionLevel.None);
                customPairing.PairingRequested -= PairingRequestedHandler;

                DisplayLine("Résultat de l'appairage : " + result.Status);
            }
            catch (Exception ex)
            {
                DisplayLine(ex.Message);
            }

        }

        private async void TenterCommuniquage(DeviceInformation bluetoothDeviceInfo)
        {
            /* SerialDevice serialDevice = await SerialDevice.FromIdAsync(bluetoothDeviceInfo.Id);

             if (bluetoothDeviceInfo != null)
             {
             }*/

            try
            {
                //success
                DisplayLine("RfComm supportant le série : ");
                var rfcommDevices = await DeviceInformation.FindAllAsync(RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort));
                DeviceInformation rfcommDeviceInfo = null;
                foreach (DeviceInformation rfcommDeviceInfoBoucle in rfcommDevices)
                {
                    if (rfcommDeviceInfoBoucle.Name.Equals("Linky-C"))
                    {
                        DisplayLine("Trouvé : " + rfcommDeviceInfoBoucle.Name + " (type : " +
                                    rfcommDeviceInfoBoucle.Kind.ToString() + ")");
                        rfcommDeviceInfo = rfcommDeviceInfoBoucle;
                    }
                }

                DisplayLine("Série : ");
                var serialDevices = await DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector());
                DeviceInformation serialDeviceInfo = null;
                foreach (DeviceInformation serialDeviceInfoBoucle in serialDevices)
                {
                    if (serialDeviceInfoBoucle.Name.Equals("Linky-C"))
                    {
                        DisplayLine("Trouvé : " + serialDeviceInfoBoucle.Name + " (type : " + serialDeviceInfoBoucle.Kind.ToString() + ")");
                        serialDeviceInfo = serialDeviceInfoBoucle;
                    }
                }

                DisplayLine("bluetoothDeviceInfo : " + bluetoothDeviceInfo.Id);
                DisplayLine("rfcommDeviceInfo : " + rfcommDeviceInfo.Id);
                DisplayLine("serialcommDeviceInfo : " + serialDeviceInfo.Id);

                await TryToCommunicate(bluetoothDeviceInfo, "bluetooth");
                await TryToCommunicate(rfcommDeviceInfo, "rfcomm");
                await TryToCommunicate(serialDeviceInfo, "serial");

            }
            catch (Exception ex)
            {
                DisplayLine(ex.Message);
            }
        }

        private async Task TryToCommunicate(DeviceInformation info, string type)
        {
            try
            {
                if (info != null)
                {
                    DisplayLine("Tentative de communication " + type + "...");
                    SerialDevice device = await SerialDevice.FromIdAsync(info.Id);
                    if (device != null)
                    {
                        DisplayLine("Port  " + type + " ouvert");
                        device.BaudRate = 115200;
                        device.Parity = SerialParity.None;
                        device.StopBits = SerialStopBitCount.One;
                        device.DataBits = 8;
                        IInputStream inputStream = device.InputStream;
                        IOutputStream outputStream = device.OutputStream;

                        using (var streamWriter = new StreamWriter(outputStream.AsStreamForWrite()))
                        {
                            byte[] bytes = { 0x00, 0x03, 0xF8, 0x72, 0x01 };
                            char[] chars = Encoding.ASCII.GetChars(bytes);
                            await streamWriter.WriteAsync(chars);
                        }
                        DisplayLine("Envoyé");
                        await Task.Delay(1000);
                        using (var streamReader = new StreamReader(inputStream.AsStreamForRead()))
                        {
                            string result = await streamReader.ReadToEndAsync();
                            byte[] bytes = Encoding.ASCII.GetBytes(result);
                            DisplayLine(bytes.ToString());
                        }
                    }
                    else
                    {
                        DisplayLine(type + " device is null.");
                    }
                }
            }
            catch (Exception e)
            {
                DisplayLine(type + " communication failed : " + e.Message);
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
                    // on the target bluetoothDeviceInfo. We automatically except here since we can't really "cancel" the operation
                    // from this side.
                    args.Accept();

                    // No need for a deferral since we don't need any decision from the user
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        DisplayLine("Please enter this PIN on the bluetoothDeviceInfo you are pairing with: " + args.Pin);

                    });
                    break;

                case DevicePairingKinds.ProvidePin:
                    // A PIN may be shown on the target bluetoothDeviceInfo and the user needs to enter the matching PIN on 
                    // this Windows bluetoothDeviceInfo. Get a deferral so we can perform the async request to the user.
                    var collectPinDeferral = args.GetDeferral();

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        DisplayLine("ProvidePin");
                        args.Accept(CodePin);
                    });
                    break;

                case DevicePairingKinds.ConfirmPinMatch:
                    // We show the PIN here and the user responds with whether the PIN matches what they see
                    // on the target bluetoothDeviceInfo. Response comes back and we set it on the PinComparePairingRequestedData
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
            if (true)//bluetoothDeviceInfo != null)
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
