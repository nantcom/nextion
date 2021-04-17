using System;
using System.IO.Ports;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NC.Nextion
{
    public partial class NextionDevice
    {
        private static void IssueCommand(SerialPort port, string command)
        {
            var ascii = System.Text.Encoding.ASCII.GetBytes(command + "AAA");
            ascii[ascii.Length - 1] = 255;
            ascii[ascii.Length - 2] = 255;
            ascii[ascii.Length - 3] = 255;

            port.Write(ascii, 0, ascii.Length);
        }

        private static void IssueBroadcastCommand(SerialPort port, string command)
        {
            var ascii = System.Text.Encoding.ASCII.GetBytes("AA" + command + "AAA");
            ascii[0] = 255;
            ascii[1] = 255;
            ascii[ascii.Length - 1] = 255;
            ascii[ascii.Length - 2] = 255;
            ascii[ascii.Length - 3] = 255;

            port.Write(ascii, 0, ascii.Length);
        }

        private static bool _IsFinding = false;

        /// <summary>
        /// Find nextion device that is not already connected
        /// </summary>
        /// <param name="portNames">Port names to search</param>
        /// <param name="exceptPortNames">Port names to skip search</param>
        public static async Task<NextionDeviceFindResult> Find(string[] portNames = null, string[] exceptPortNames = null)
        {
            if (_IsFinding)
            {
                throw new InvalidOperationException("Another Find is in progress");
            }
            _IsFinding = true;

            if (portNames == null)
            {
                portNames = SerialPort.GetPortNames();
            }

            if (exceptPortNames != null)
            {
                portNames = portNames.Except(exceptPortNames).ToArray();
            }

            var bauds = new int[] { 921600, 9600, 115200, 2400, 4800, 19200, 31250, 38400, 57600, 230400, 250000, 256000, 512000 };

            Func<string, int, (bool ok, string returnString)> testPort = (port, baud) =>
            {
                bool shouldRetry = true;
            Retry:

                (bool ok, string returnString) testResult = (false, null);

                var ev = new ManualResetEvent(false);

                SerialPort comport = new SerialPort(port, baud, Parity.None, 8, StopBits.One);
                try
                {
                    comport.Encoding = System.Text.Encoding.ASCII;
                    comport.Open();
                    comport.DiscardOutBuffer();
                    comport.DiscardInBuffer();

                    comport.DataReceived += (s, e) =>
                    {
                        try
                        {
                            while (comport.BytesToRead > 0)
                            {
                                var result = comport.ReadTo(TERMINATION);
                                if (result.Contains("comok"))
                                {
                                    testResult.returnString = result.Substring(result.IndexOf("comok"));
                                    ev.Set();
                                }
                            }
                        }
                        catch (Exception)
                        {
                            ev.Set();
                            if (testResult.returnString == null)
                            {
                                shouldRetry = true;
                            }
                        }

                    };

                    NextionDevice.IssueCommand(comport, "DRAKJHSUYDGBNCJHGJKSHBDN");
                    NextionDevice.IssueCommand(comport, "connect");
                    NextionDevice.IssueBroadcastCommand(comport, "connect");

                    if (ev.WaitOne((int)((1000000d / baud) + 300)))
                    {
                        testResult.ok = true;
                    }
                    else
                    {
                        testResult.ok = false;
                    }

                }
                catch (UnauthorizedAccessException ex)
                {
                    shouldRetry = true;
                }
                catch (Exception ex)
                {
                }
                finally
                {
                    // needs to spawn thread because sometimes 
                    // the program stuck when trying to close port
                    Task.Run(() =>
                    {
                        if (comport != null && comport.IsOpen)
                        {
                            comport.DiscardInBuffer();
                            comport.DiscardOutBuffer();
                            comport.Close();
                            comport.Dispose();
                        }

                    });
                }

                if (testResult.ok == false && shouldRetry == true)
                {
                    shouldRetry = false;

                    Task.Delay(1000).Wait();

                    goto Retry;
                }

                return testResult;
            };

            NextionDeviceFindResult findResult = null;
            foreach (var portName in portNames.OrderBy(s => s))
            {
                foreach (var b in bauds)
                {
                    (bool ok, string response) result = (false, null);

                    await Task.Run(() =>
                    {
                        try
                        {
                            result = testPort(portName, b);
                        }
                        catch (Exception)
                        {
                        }
                    });

                    if (result.ok)
                    {
                        findResult = new NextionDeviceFindResult()
                        {
                            ResponseString = result.response,
                            COMPort = portName,
                            COMBaudRate = b
                        };
                        goto EndFind;
                    }
                }
            }

        EndFind:
            _IsFinding = false;
            return findResult;
        }

    }
}
