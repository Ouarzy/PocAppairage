using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;
using Windows.UI.Xaml;

namespace PocAppairageUi
{
    class Class1
    {
        async void OnRegister(object sender, RoutedEventArgs e)
        {
            if (BackgroundTaskRegistration.AllTasks.Count == 0)
            {
                var trigger = this.MakeTrigger();

                // this is needed for Phone, not so for Windows in this case.
                var allowed = await BackgroundExecutionManager.RequestAccessAsync();

                if ((allowed != BackgroundAccessStatus.Denied) &&
                  (allowed != BackgroundAccessStatus.Unspecified))
                {
                    BackgroundTaskBuilder builder = new BackgroundTaskBuilder();
                    builder.Name = "My Task Name";
                    //builder.TaskEntryPoint = typeof(TheTask).FullName;
                    builder.SetTrigger(trigger);
                    builder.Register();
                }
            }
        }
        BluetoothLEAdvertisementPublisherTrigger MakeTrigger()
        {
            var trigger = new BluetoothLEAdvertisementPublisherTrigger();

            var dataToPublish = "Hello";
            var writer = new DataWriter();
            writer.WriteInt32(dataToPublish.Length);
            writer.WriteString(dataToPublish);

            trigger.Advertisement.ManufacturerData.Add(
              new BluetoothLEManufacturerData()
              {
                  CompanyId = 0x0006, // Microsoft, I think, didn't check again.
                  Data = writer.DetachBuffer()
              }
            );
            return (trigger);
        }

    }
}
