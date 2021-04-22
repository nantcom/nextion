using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace NC.Nextion
{
    public partial class NextionDevice
    {
        private static void SyncWrite(SerialPort port, byte[] buffer, int length)
        {
            lock (port)
            {
                port.Write(buffer, 0, length);
            }
        }

        /// <summary>
        /// Issue a Nextion command to serial port
        /// </summary>
        /// <param name="port"></param>
        /// <param name="command"></param>
        public static void IssueCommand(SerialPort port, string command)
        {
            // allocate buffer and pre-filled with 255
            var buffer = new byte[command.Length + 3];
            Unsafe.InitBlock(ref buffer[0], 255, (uint)buffer.Length);

            // overwrite it with ASCII
            // format is: command??? where ? = 255
            var span = new Span<byte>(buffer, 0, command.Length);
            System.Text.Encoding.ASCII.GetBytes(command, span);

            NextionDevice.SyncWrite(port, buffer, buffer.Length);
        }

        /// <summary>
        /// Issue command batch to nextion device
        /// </summary>
        /// <param name="port"></param>
        /// <param name="commands"></param>
        public static void IssueCommandBatch(SerialPort port, IEnumerable<string> commands)
        {
            // allocate buffer and pre-filled with 255
            var buffer = new byte[1024];
            Unsafe.InitBlock(ref buffer[0], 255, (uint)buffer.Length);

            var position = 0;
            Action<string> appendCommand = (command) =>
            {
                if (command.Length + 3 + position > 1024)
                {
                    throw new InvalidOperationException("Buffer is now too large");
                }

                var span = new Span<byte>(buffer, position, command.Length + 3);
                System.Text.Encoding.ASCII.GetBytes(command, span);

                position += (command.Length + 3);
            };

            appendCommand("com_stop");

            foreach (var item in commands)
            {
                appendCommand(item);
            }

            appendCommand("com_star");

            NextionDevice.SyncWrite(port, buffer, position);
        }



        public static Task IssueFadeCommand( SerialPort port, int from, int to, int step = 10, int delay = 75 )
        {
            var cmd = new List<string>();
            var inc = to > from ? 1 : -1;

            Func<int, bool> willStop = (i) => to > from ? (i <= to) : (i >= to);

            var y = 0;
            cmd.Add("sleep=0");
            for (int i = from; willStop(i); i += inc * step)
            {
                cmd.Add($"dim={i}");
                cmd.Add($"delay={delay}");
            }

            NextionDevice.IssueCommandBatch(port, cmd);

            return Task.Delay(step * (delay + 10));
        }

        /// <summary>
        /// Issue a Nextion Broadcast command to serial port
        /// </summary>
        /// <param name="port"></param>
        /// <param name="command"></param>
        public static void IssueBroadcastCommand(SerialPort port, string command)
        {
            // allocate buffer and pre-filled with 0
            var buffer = new byte[command.Length + 5];
            Unsafe.InitBlock(ref buffer[0], 255, (uint)buffer.Length);

            // overwrite it with ASCII from command
            // format is: ??command??? where ? = 255
            var span = new Span<byte>(buffer, 2, command.Length);
            System.Text.Encoding.ASCII.GetBytes(command, span);

            NextionDevice.SyncWrite(port, buffer, buffer.Length);
        }

        private static bool _IsFinding = false;

        /// <summary>
        /// Find nextion device that is not already connected
        /// </summary>
        /// <param name="portNames">Port names to search</param>
        /// <param name="exceptPortNames">Port names to skip search</param>
        public static async Task<NextionDeviceFindResult> Find(IEnumerable<string> portNames = null, IEnumerable<string> exceptPortNames = null)
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

            var bauds = new int[] { 921600, 115200, 9600, 2400, 4800, 19200, 31250, 38400, 57600, 230400, 250000, 256000, 512000 };

#if DEBUG

            bauds = new int[] { 921600, 115200};
#endif

            Func<string, int, (bool ok, string returnString)> testPort = (port, baud) =>
            {
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
                                    testResult.returnString = result;
                                    comport.DiscardInBuffer();
                                    ev.Set();

                                    return;
                                }
                                else
                                {
                                    // maybe simulator
                                    if ( System.Text.ASCIIEncoding.ASCII.GetBytes(result)[0] == 26 )
                                    {
                                        // simulator return "invalid command"
                                        
                                        testResult.returnString = "comok 1,38024-2556,Simulator,99,999,I-AM-SIMULATOR--,16777216";
                                        comport.DiscardInBuffer();
                                        ev.Set();

                                        return;
                                    }
                                }

                            }
                        }
                        catch (Exception)
                        {
                            ev.Set();
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

                return testResult;
            };

            NextionDeviceFindResult findResult = null;
            foreach (var portName in portNames.Where( p => p.StartsWith("COM") ).OrderBy(s => s))
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
