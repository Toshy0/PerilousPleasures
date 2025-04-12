using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buttplug.Client;

namespace DeviceControlExample
{
    class Program
    {
        private static bool usingAutoMode = false;
        private static float vibrationIntensity = 0f;
        private static Task autoModeTask;

        private static async Task WaitForKey()
        {
            Console.WriteLine("Press any key to continue.");
            while (!Console.KeyAvailable)
            {
                await Task.Delay(1);
            }
            Console.ReadKey(true);
        }

        private static async Task PerilousPleasures()
        {
            var client = new ButtplugClient("PerilousPleasures");
            var connector = new ButtplugWebsocketConnector(new Uri("ws://127.0.0.1:12345"));

            try
            {
                await client.ConnectAsync(connector);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Can't connect, exiting!");
                Console.WriteLine($"Message: {ex.InnerException?.Message}");
                await WaitForKey();
                return;
            }

            Console.WriteLine("Connected!");

            await client.StartScanningAsync();
            await client.StopScanningAsync();
            Console.WriteLine("Client knows about these devices:");

            foreach (var device in client.Devices)
            {
                Console.WriteLine($"- {device.Name}");
            }

            await WaitForKey();

            while (true)
            {
                Console.WriteLine("Enter vibration value (0-100) or type 'auto' to revert to RGB control:");

                string input = Console.ReadLine();

                if (input.ToLower() == "auto")
                {
                    usingAutoMode = true;
                    Console.WriteLine("Reverting to RGB control.");

                    if (autoModeTask == null || autoModeTask.Status != TaskStatus.Running)
                    {
                        autoModeTask = RunAutoMode(client);
                    }
                }
                else
                {
                    if (float.TryParse(input, out float value))
                    {
                        if (value >= 0 && value <= 100)
                        {
                            vibrationIntensity = value / 100f;
                            usingAutoMode = false;
                            Console.WriteLine($"Manual vibration intensity set to {value}%.");
                        }
                        else
                        {
                            Console.WriteLine("Invalid value. Please enter a number between 0 and 100.");
                            continue;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid input. Please enter a valid number or 'auto'.");
                        continue;
                    }
                }

                foreach (var device in client.Devices)
                {
                    Console.WriteLine($"{device.Name} supports vibration: {device.VibrateAttributes.Count > 0}");

                    if (device.VibrateAttributes.Count > 0)
                    {
                        var vibratorCount = device.VibrateAttributes.Count;
                        await device.VibrateAsync(Enumerable.Repeat((double)vibrationIntensity, vibratorCount).ToArray());
                        Console.WriteLine($"Sent vibration command to {device.Name} with intensity {vibrationIntensity * 100}%");
                    }
                }

                await Task.Delay(1000);
            }

            await client.DisconnectAsync();
            Console.WriteLine("Disconnected!");

            try
            {
                var testClientDevice = client.Devices.First();
                await testClientDevice.VibrateAsync(1.0);
            }
            catch (ButtplugClientConnectorException e)
            {
                Console.WriteLine("Tried to send after disconnection! Exception: ");
                Console.WriteLine(e);
            }

            await WaitForKey();
        }

        private static async Task RunAutoMode(ButtplugClient client)
        {
            while (usingAutoMode)
            {
                var rgb = GetPixelColor(1147, 878);
                vibrationIntensity = MapRgbToVibration(rgb);
                Console.WriteLine($"Auto mode: Vibration intensity based on RGB: {vibrationIntensity * 100}%");

                foreach (var device in client.Devices)
                {
                    Console.WriteLine($"{device.Name} supports vibration: {device.VibrateAttributes.Count > 0}");

                    if (device.VibrateAttributes.Count > 0)
                    {
                        var vibratorCount = device.VibrateAttributes.Count;
                        await device.VibrateAsync(Enumerable.Repeat((double)vibrationIntensity, vibratorCount).ToArray());
                        Console.WriteLine($"Sent vibration command to {device.Name} with intensity {vibrationIntensity * 100}%");
                    }
                }

                await Task.Delay(50);
            }
        }

        static Color GetPixelColor(int x, int y)
        {
            using (Bitmap screenshot = new Bitmap(1, 1))
            {
                using (Graphics g = Graphics.FromImage(screenshot))
                {
                    g.CopyFromScreen(x, y, 0, 0, new Size(1, 1));
                }
                return screenshot.GetPixel(0, 0);
            }
        }

        static float MapRgbToVibration(Color color)
        {
            if (color.G > 0 || color.B > 0)
            {
                return vibrationIntensity;
            }
            if (color.R / 255f < 0.04)
            {
                return 0;
            }
            else
            {
                return color.R / 255f;
            }
        }

        private static void Main()
        {
            PerilousPleasures().Wait();
        }
    }
}
