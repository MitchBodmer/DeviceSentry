using System.Text;
using DeviceSentry;
using Windows.Devices.Enumeration;

bool enumerationCompleted = false;
List<DeviceInformation> deviceInformationList = [];

Console.OutputEncoding = Encoding.UTF8;

TaskCompletionSource watcherAbortedTaskCompletionSource = new();

DeviceWatcher primaryDeviceWatcher = DeviceInformation.CreateWatcher();

primaryDeviceWatcher.EnumerationCompleted += EnumerationCompeted;
primaryDeviceWatcher.Added += DeviceAdded;
primaryDeviceWatcher.Updated += DeviceUpdated;
primaryDeviceWatcher.Removed += DeviceRemoved;
primaryDeviceWatcher.Stopped += Stopped;

Console.CancelKeyPress += Cancelled;

const string usageMessage = "Press the space bar to pause or resume the sentry, 'C' to clear the history, or 'Q' to exit.";

Console.WriteLine($"Starting the sentry. {usageMessage}");

primaryDeviceWatcher.Start();

while (true)
{
    switch (await Task.WhenAny(ReadKeyAsync(), watcherAbortedTaskCompletionSource.Task))
    {
        case Task<ConsoleKey> consoleKeyTask:
            switch (await consoleKeyTask)
            {
                case ConsoleKey.Spacebar:

                    switch (primaryDeviceWatcher.Status)
                    {
                        case DeviceWatcherStatus.Stopped:
                            Console.WriteLine("Resuming the sentry.");
                            primaryDeviceWatcher.Start();
                            break;
                        case DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted:
                            Console.WriteLine("Pausing the sentry.");
                            enumerationCompleted = false;
                            primaryDeviceWatcher.Stop();
                            deviceInformationList.Clear();
                            break;
                        case DeviceWatcherStatus.Stopping:
                            Console.WriteLine("The sentry is pausing.");
                            break;
                    }

                    break;

                case ConsoleKey.Q:
                    Console.WriteLine();
                    Console.WriteLine("Stopping the sentry.");
                    primaryDeviceWatcher.Stop();
                    return;

                case ConsoleKey.C:
                    Console.Clear();
                    Console.Write("\x1b[3J");
                    Console.WriteLine(usageMessage);
                    break;
            }

            break;

        default:
            Console.WriteLine();
            Console.WriteLine("The sentry stopped unexpectedly.");
            return;
    }
}

async Task<ConsoleKey> ReadKeyAsync() => 
    await Task.Run(() => Console.ReadKey(true).Key).ConfigureAwait(false);

void Cancelled(object? sender, ConsoleCancelEventArgs consoleCancelEventArgs)
{
    consoleCancelEventArgs.Cancel = true;
    Console.WriteLine();
    Console.WriteLine("User cancelled. Stopping the sentry.");
    primaryDeviceWatcher.Stop();
}

void EnumerationCompeted(DeviceWatcher deviceWatcher, object _) => enumerationCompleted = true;

void DeviceAdded(DeviceWatcher deviceWatcher, DeviceInformation deviceInformation)
{
    if (enumerationCompleted)
    {
        PrintDeviceEventHeader("Device Added", deviceInformation.Name, Colors.DeviceAdded);

        PrintDeviceInformation(deviceInformation);

        deviceInformationList.Add(deviceInformation);

        Console.WriteLine();
    }
    else
    {
        deviceInformationList.Add(deviceInformation);
    }
}

void DeviceUpdated(DeviceWatcher deviceWatcher, DeviceInformationUpdate deviceInformationUpdate)
{
    DeviceInformation deviceInformation =
        deviceInformationList.Single(deviceInformation => deviceInformation.Id == deviceInformationUpdate.Id);

    PrintDeviceEventHeader("Device Updated", deviceInformation.Name, Colors.DeviceUpdated);

    PrintDeviceUpdate(deviceInformation, deviceInformationUpdate);

    deviceInformation.Update(deviceInformationUpdate);

    Console.WriteLine();
}

void DeviceRemoved(DeviceWatcher deviceWatcher, DeviceInformationUpdate deviceInformationUpdate)
{
    DeviceInformation deviceInformation = deviceInformationList.Single(
        deviceInformation => deviceInformation.Id == deviceInformationUpdate.Id);

    PrintDeviceEventHeader("Device Removed", deviceInformation.Name, Colors.DeviceRemoved);

    PrintDeviceUpdate(deviceInformation, deviceInformationUpdate);

    deviceInformationList.Remove(deviceInformation);

    Console.WriteLine();
}

void Stopped(DeviceWatcher deviceWatcher, object _)
{
    if (deviceWatcher.Status == DeviceWatcherStatus.Aborted)
    {
        watcherAbortedTaskCompletionSource.SetResult();
    }
}

void PrintDeviceInformation(DeviceInformation deviceInformation)
{
    PrintPropertyRow(nameof(deviceInformation.Id), deviceInformation.Id);
    PrintPropertyRow(nameof(deviceInformation.Kind), deviceInformation.Kind.ToString());

    foreach (KeyValuePair<string, object> keyValuePair in deviceInformation.Properties.OrderBy(keyValuePair=>keyValuePair.Key))
    {
        PrintPropertyRow(keyValuePair.Key, new DevicePropertyValue(keyValuePair.Value));
    }
}

void PrintDeviceUpdate(DeviceInformation deviceInformation, DeviceInformationUpdate deviceInformationUpdate)
{
    PrintPropertyDiffRow(
        nameof(deviceInformation.Id),
        deviceInformation.Id,
        deviceInformationUpdate.Id);

    PrintPropertyDiffRow(
        nameof(deviceInformation.Kind), 
        deviceInformation.Kind.ToString(), 
        deviceInformationUpdate.Kind.ToString());

    foreach (KeyValuePair<string, object> keyValuePair in deviceInformation.Properties.OrderBy(keyValuePair=>keyValuePair.Key))
    {
        DevicePropertyValue oldPropertyValue = new(keyValuePair.Value);

        if (deviceInformationUpdate.Properties.TryGetValue(keyValuePair.Key, out object? newPropertyValueObject))
        {
            DevicePropertyValue newPropertyValue = new(newPropertyValueObject);
            PrintPropertyDiffRow(keyValuePair.Key, oldPropertyValue, newPropertyValue);
        }
        else
        {
            PrintPropertyRow(keyValuePair.Key, oldPropertyValue);
        }
    }
}

void PrintDeviceEventHeader(string headerText, string deviceName, ConsoleColor consoleColor)
{
    Console.Write("\u2588 (");

    PrintWithColor($"{DateTime.Now:M/d/yy h:m:s tt}", Colors.DateTime);

    Console.Write(") ");

    PrintWithColor(headerText, consoleColor);

    Console.WriteLine($" - {deviceName}");
}

void PrintPropertyRow(string name, string? value)
{
    PrintPropertyName(name);
    PrintPropertyValue(value);
}

void PrintPropertyDiffRow(string name, string? oldValue, string? newValue)
{
    PrintPropertyName(name);
    PrintPropertyValueDiff(oldValue, newValue);
}

void PrintPropertyName(string rowLabel)
{
    Console.Write("\u2588 ");

    PrintWithColor(rowLabel, Colors.PropertyLabel);

    Console.Write(": ");
}

void PrintPropertyValue(string? s) => Console.WriteLine(s);

void PrintPropertyValueDiff(string? oldPropertyValue, string? newPropertyValue)
{
    if (newPropertyValue == oldPropertyValue)
    {
        PrintPropertyValue(newPropertyValue);
    }
    else
    {
        PrintWithColor(oldPropertyValue, Colors.OldValue);

        Console.Write(" \u2192 ");

        PrintWithColor(newPropertyValue, Colors.NewValue);

        Console.WriteLine();
    }
}

void PrintWithColor(string? value, ConsoleColor color)
{
    ConsoleColor oldConsoleColor = Console.ForegroundColor;

    Console.ForegroundColor = color;
    Console.Write(value);
    Console.ForegroundColor = oldConsoleColor;
}