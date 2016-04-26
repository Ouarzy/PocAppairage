using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
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
        private const string CodePin = "AY85QK-JWICUBIT4";
        private const string Constructeur = "2A";
        private const string AdresseMac = "00078022780B";
        private string _numeroSerie;

        private StreamSocket _socket;
        private DataWriter dataWriterObject;
        private DataReader dataReaderObject;
        private CancellationTokenSource ReadCancellationTokenSource;

        private DeviceInformation sharedDeviceInfo;

        public MainPage()
        {
            InitializeComponent();

            _numeroSerie = CodePin + Constructeur + AdresseMac;
            NoSerieConnecteur.Text = _numeroSerie;
        }

        private async Task<DeviceInformation> TenterListage()
        {
            DeviceInformation deviceInfo = null;
            try
            {
                DisplayLine("Début du scan...");

                var selectorFromDeviceName = BluetoothDevice.GetDeviceSelectorFromDeviceName("Linky-C");
                var devices = await DeviceInformation.FindAllAsync(selectorFromDeviceName);
                if (!devices.Any())
                {
                    DisplayLine("Pas de connecteur trouvé");
                }
                else
                {
                    foreach (var device in devices.Where(d => d.IsCorrectMacAddress(AdresseMac)))
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
            return deviceInfo;
        }

        private async void TenterAppairage(DeviceInformation device)
        {
            try
            {
                DisplayLine("Début de l'appairage...");

                var customPairing = device.Pairing.Custom;

                customPairing.PairingRequested += PairingRequestedHandler;
                var result = await customPairing.PairAsync(DevicePairingKinds.ProvidePin, DevicePairingProtectionLevel.EncryptionAndAuthentication);
                customPairing.PairingRequested -= PairingRequestedHandler;

                DisplayLine("Résultat de l'appairage : " + result.Status);
            }
            catch (Exception ex)
            {
                DisplayLine(ex.Message);
            }
        }

        private async void TenterCommuniquage()
        {
            try
            {
                DisplayLine("Retrouver appairage courant...");
                var selector = RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort);
                foreach (DeviceInformation serialDeviceInfoBoucle in await DeviceInformation.FindAllAsync(selector))
                {
                    if (serialDeviceInfoBoucle.Name.Equals("Linky-C"))
                    {
                        DisplayLine("Trouvé : " + serialDeviceInfoBoucle.Name + " (type : " + serialDeviceInfoBoucle.Kind.ToString() + ")");

                        DisplayLine("serialcommDeviceInfo : " + serialDeviceInfoBoucle.Id);

                        await TryToCommunicate(serialDeviceInfoBoucle, "serial");
                    }
                }
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

                    var rfCommInfo = await RfcommDeviceService.FromIdAsync(info.Id);

                    using (StreamSocket streamSocket = new StreamSocket())
                    {
                        await streamSocket.ConnectAsync(rfCommInfo.ConnectionHostName, rfCommInfo.ConnectionServiceName, SocketProtectionLevel.BluetoothEncryptionAllowNullAuthentication);

                        byte[] bytesOut = { 0x00, 0x03, 0xF3, 0xB5, 0x40 };

                        var writer = new DataWriter(streamSocket.OutputStream);
                        var reader = new DataReader(streamSocket.InputStream);

                        writer.WriteBytes(bytesOut);
                        await writer.StoreAsync();

                        // Get length
                        byte[] bytesLength = new byte[2];
                        await reader.LoadAsync(2);
                        reader.ReadBytes(bytesLength);

                        var length = (uint)BitConverter.ToUInt16(bytesLength.Reverse().ToArray(), 0);
                        byte[] bytesIn = new byte[length];
                        await reader.LoadAsync(length);
                        reader.ReadBytes(bytesIn);

                        DisplayLine(bytesIn.Aggregate("", (val, b) => val + b.ToString("X2")));
                    }

                    //SerialDevice device = await SerialDevice.FromIdAsync(info.Id);
                    //if (device != null)
                    //{
                    //    DisplayLine("Port  " + type + " ouvert");
                    //    device.Parity = SerialParity.None;
                    //    device.BaudRate = 115200;
                    //    device.StopBits = SerialStopBitCount.One;
                    //    device.DataBits = 8;
                    //    device.Handshake = SerialHandshake.None;
                    //    device.IsRequestToSendEnabled = false;
                    //    device.IsDataTerminalReadyEnabled = false;
                    //    device.ReadTimeout = TimeSpan.FromMilliseconds(5000);
                    //    device.WriteTimeout = TimeSpan.FromMilliseconds(1000);
                    //    IInputStream inputStream = device.InputStream;
                    //    IOutputStream outputStream = device.OutputStream;

                    //    byte[] bytesOut = { 0x00, 0x03, 0xF3, 0xB5, 0x40 };

                    //    await outputStream.WriteAsync(bytesOut.AsBuffer());
                    //    // await outputStream.FlushAsync();

                    //    DisplayLine("Envoyé");

                    //    IBuffer inBuffer = new Buffer(1000);
                    //    await inputStream.ReadAsync(inBuffer, 1000, InputStreamOptions.ReadAhead);

                    //    byte[] bytesIn = inBuffer.ToArray();
                    //    string s = "Reçu : ";
                    //    foreach (byte b in bytesIn)
                    //    {
                    //        s += b.ToString("X2") + " ";
                    //    }
                    //    DisplayLine(s);
                    //}
                    //else
                    //{
                    //    DisplayLine(type + " device is null.");
                    //}
                }
            }
            catch (Exception e)
            {
                DisplayLine(type + " communication failed : " + e.Message);
            }
            DisplayLine("Fini");
        }

        private void PairingRequestedHandler(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
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
                    DisplayLine("Please enter this PIN on the bluetoothDeviceInfo you are pairing with: " + args.Pin);
                    break;

                case DevicePairingKinds.ProvidePin:
                    // A PIN may be shown on the target bluetoothDeviceInfo and the user needs to enter the matching PIN on 
                    // this Windows bluetoothDeviceInfo. Get a deferral so we can perform the async request to the user.
                    var deferral = args.GetDeferral();
                    DisplayLine("ProvidePin");
                    args.Accept(CodePin);
                    deferral.Complete();
                    break;

                case DevicePairingKinds.ConfirmPinMatch:
                    // We show the PIN here and the user responds with whether the PIN matches what they see
                    // on the target bluetoothDeviceInfo. Response comes back and we set it on the PinComparePairingRequestedData
                    // then complete the deferral.
                    var displayMessageDeferral = args.GetDeferral();
                    DisplayLine("ConfirmPinMatch");
                    args.Accept();
                    displayMessageDeferral.Complete();
                    break;
            }
        }

        private void AppairerOnClick(object sender, RoutedEventArgs e)
        {
            if (sharedDeviceInfo != null)
            {
                Task.Run(() => TenterAppairage(sharedDeviceInfo));
            }
        }

        private void ListerOnClick(object sender, RoutedEventArgs e)
        {
            Task.Run(() => TenterListage());
        }

        private void CommuniquerOnClick(object sender, RoutedEventArgs e)
        {
            Task.Run(() => TenterCommuniquage());
        }

        private void DisplayLine(string message)
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => Console.Text += message + "\n");
        }
    }

    internal static class Extensions
    {
        internal static bool IsCorrectMacAddress(this DeviceInformation deviceInformation, string macAddress)
        {
            var mac = macAddress.ToUpper().Replace(":", "");
            var id = deviceInformation.Id.ToUpper().Replace(":", "");

            return id.Contains(mac);
        }
    }
}
