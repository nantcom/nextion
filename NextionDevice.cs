using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NC.Nextion
{

    public sealed partial class NextionDevice : IDisposable
    {
        private static readonly string TERMINATION = System.Text.Encoding.ASCII.GetString(new byte[] { 255, 255, 255 });

        private SerialPort _Port;

        /// <summary>
        /// COM Port connected to device
        /// </summary>
        public string COMPort { get; private set; }

        /// <summary>
        /// Baud Rate used
        /// </summary>
        public int COMBaudRate { get; private set; }

        /// <summary>
        /// Whether device support touch
        /// </summary>
        public bool IsHasTouch { get; private set; }

        /// <summary>
        /// Model
        /// </summary>
        public string Model { get; private set; }

        /// <summary>
        /// Size of Flash Memory
        /// </summary>
        public int FlashSize { get; private set; }

        /// <summary>
        /// Serial Number
        /// </summary>
        public string Serial { get; private set; }

        /// <summary>
        /// MCU Serial Number
        /// </summary>
        public string MCUCode { get; private set; }

        /// <summary>
        /// Whether the device is simulator
        /// </summary>
        public bool IsSimulator { get; private set; }

        /// <summary>
        /// Whether the serial port was removed
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Whether the device is connected
        /// </summary>
        public bool IsConnected
        {
            get
            {
                if (_Port != null)
                {
                    return _Port.IsOpen;
                }

                return false;
            }
        }

        /// <summary>
        /// Create Instance of NextionDevice for use with Nextion Simulator
        /// </summary>
        /// <param name="portName"></param>
        public NextionDevice(string portName, int baudRate)
        {
            this.Model = "Simulator";
            this.MCUCode = "Virtual";
            this.Serial = "Virtual 01";
            this.IsHasTouch = true;
            this.FlashSize = 40000;
            this.COMPort = portName;
            this.COMBaudRate = baudRate;
            this.IsSimulator = true;

            this.Initialize(_Port_DataReceived);
        }

        /// <summary>
        /// Create Instance of NextionDevice from find result
        /// </summary>
        /// <param name="result"></param>
        public NextionDevice( NextionDeviceFindResult result )
            : this( result.COMPort, result.COMBaudRate, result.ResponseString)
        {

        }

        /// <summary>
        /// Create Instance of NextionDevice
        /// </summary>
        /// <param name="portName">COM Port Name</param>
        /// <param name="baudRate">Baud rate</param>
        /// <param name="returnString">return string from Nextion Device</param>
        public NextionDevice(string portName, int baudRate, string returnString)
        {
            this.ParseReturnString(returnString);
            this.COMPort = portName;
            this.COMBaudRate = baudRate;
        }

        private void Initialize(SerialDataReceivedEventHandler portHandler, int receiveThreshold = 4)
        {
            if (this.IsDisposed)
            {
                this.IsDisposed = false;
                GC.ReRegisterForFinalize(this);
            }

            if (_Port != null)
            {
                if (_Port.IsOpen)
                {
                    _Port.DiscardInBuffer();
                    _Port.DiscardOutBuffer();
                    _Port.Close();
                }
                _Port.Dispose();
            }

            _Port = new SerialPort(this.COMPort, this.COMBaudRate, Parity.None, 8, StopBits.One);
            _Port.Open();

            _Port.WriteTimeout = 1000;

            _Port.ReceivedBytesThreshold = receiveThreshold;
            _Port.DataReceived += portHandler;
            _Port.DiscardInBuffer();
        }

        private void ParseReturnString(string returnString)
        {
            if (string.IsNullOrEmpty(returnString))
            {
                throw new ArgumentNullException(returnString);
            }

            var temp = returnString.Substring(6, returnString.Length - 9);

            var parts = temp.Split(',');

            this.IsHasTouch = parts[0] == "1";
            this.Model = parts[2];
            this.MCUCode = parts[4];
            this.Serial = parts[5];
            this.FlashSize = int.Parse(parts[6]);
        }

        /// <summary>
        /// Connect to Nextion Device, If COM Port is already open - it will be closed and reconnected
        /// Upon connection
        /// </summary>
        public void Connect()
        {
            this.Initialize(_Port_DataReceived);

            if (this.IsSimulator == false)
            {
                NextionDevice.IssueBroadcastCommand(_Port, "addr=0");
                this.BatchCommands(new string[] {
                    "addr=0",
                    "ussp=0",
                    "thsp=0",
                    "thup=1",
                    "bkcmd=2",
                    "dim=0",
                    "sleep=0",
                    "dim=0",
                });
            }
        }

        /// <summary>
        /// If this flag is set, _Port_DataReceived will not process incoming data
        /// </summary>
        private bool _StopResponseParsing = false;

        private List<int> _InBuffer = new List<int>(1024);
        private ManualResetEvent _WaitResponse = new ManualResetEvent(false);

        private void _Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_StopResponseParsing)
            {
                return;
            }

            while (_Port.BytesToRead > 0)
            {
                _InBuffer.Add(_Port.ReadByte());

                if (_InBuffer.Count >= 4 &&
                    _InBuffer[_InBuffer.Count - 1] == 255 &&
                    _InBuffer[_InBuffer.Count - 2] == 255 &&
                    _InBuffer[_InBuffer.Count - 3] == 255)
                {
                    if (_InBuffer.Count == 4)
                    {
                        // short command with only response code
                        this.NewResponse(new NextionResponse()
                        {
                            Code = "0x" + _InBuffer[0].ToString("X2")
                        });
                    }
                    else
                    {
                        var response = _InBuffer
                                            .Take(_InBuffer.Count - 3)
                                            .Select(b => (byte)b)
                                            .ToArray();

                        this.NewResponse(new NextionResponse()
                        {
                            Code = "0x" + _InBuffer[0].ToString("X2"),
                            Data = System.Text.Encoding.ASCII.GetString(response, 1, response.Length - 1)
                        });
                    }

                    _WaitResponse.Set();
                    _InBuffer.Clear();

                }
            }
        }

        /// <summary>
        /// Occured when tnew response from nextion is received
        /// first string is the response code (HEX), string contains data (if any)
        /// </summary>
        public event Action<NextionResponse> NewResponse = delegate { };

        /// <summary>
        /// Send command to nextion and wait for response code
        /// </summary>
        /// <param name="command"></param>
        /// <returns>Response code (First Byte of nextion response)</returns>
        public async Task<List<NextionResponse>> IssueCommand(string command)
        {
            if (this.IsDisposed)
            {
                return null;
            }

            _Port.DiscardInBuffer();
            _Port.DiscardOutBuffer();

            List<NextionResponse> responses = new List<NextionResponse>();

            await Task.Run(() =>
            {
                var lastResponse = DateTime.Now;
                Action<NextionResponse> captureResponse = (r) =>
                {
                    responses.Add(r);
                };

                try
                {
                    NextionDevice.IssueCommand(_Port, "bkcmd=3");
                    _WaitResponse.WaitOne();
                    _WaitResponse.Reset();

                    _Port.DiscardInBuffer();

                    this.NewResponse += captureResponse;
                    NextionDevice.IssueCommand(_Port, command);
                    _WaitResponse.WaitOne();
                    _WaitResponse.Reset();

                    NextionDevice.IssueCommand(_Port, "bkcmd=2");

                    Task.Delay(1000).Wait();
                }
                catch (Exception)
                {
                    this.Dispose();
                }

                this.NewResponse -= captureResponse;
            });

            if (this.IsDisposed == false)
            {
                _Port.DiscardInBuffer();
                _Port.DiscardOutBuffer();
            }

            return responses;
        }

        /// <summary>
        /// Issue command without waiting for response
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public void IssueCommandAndForget(string command)
        {
            if (this.IsDisposed)
            {
                return;
            }

            try
            {
                NextionDevice.IssueCommand(_Port, command);
            }
            catch (Exception)
            {
                this.Dispose();
            }
        }

        private MemoryStream _CommandBuffer = new MemoryStream(1024);

        /// <summary>
        /// Buffer a command to nextion device
        /// </summary>
        /// <param name="command"></param>
        public void BufferCommand(string command)
        {
            if (this.IsDisposed)
            {
                return;
            }

            if (_CommandBuffer == null)
            {
                _CommandBuffer = new MemoryStream(1024);
                return;
            }

            var ascii = System.Text.Encoding.ASCII.GetBytes(command + "AAA");
            ascii[ascii.Length - 1] = 255;
            ascii[ascii.Length - 2] = 255;
            ascii[ascii.Length - 3] = 255;
            _CommandBuffer.Write(ascii, 0, ascii.Length);
        }

        /// <summary>
        /// Flush the command buffer
        /// </summary>
        public void FlushCommandBuffer()
        {
            if (_Port == null)
            {
                return; // other thread have closed the connection
            }

            if (_Port.IsOpen == false)
            {
                return;
            }

            if (_CommandBuffer == null || _CommandBuffer.Length == 0)
            {
                return;
            }

            lock (this)
            {
                var bytes = _CommandBuffer.GetBuffer();
                var wait = new ManualResetEventSlim(false);

                Task.Run(() =>
                {
                    try
                    {
                        _Port.Write(bytes, 0, (int)_CommandBuffer.Position);
                        wait.Set();
                    }
                    catch (Exception)
                    {
                        this.Dispose();
                    }
                });

                var isset = wait.Wait(2000);
                if (isset == false)
                {
                    this.Dispose();
                }

                _CommandBuffer.Dispose();
                _CommandBuffer = new MemoryStream();
            }
        }

        /// <summary>
        /// Buffer a command to nextion device and flush afterwards
        /// </summary>
        /// <param name="command"></param>
        public void BatchCommands(IEnumerable<string> command)
        {
            if (_Port == null)
            {
                return; // other thread have closed the connection
            }

            this.BufferCommand("com_stop");

            foreach (var cmd in command)
            {
                this.BufferCommand(cmd);
            }

            this.BufferCommand("com_star");
            this.FlushCommandBuffer();
        }


        /// <summary>
        /// Upload new firmware, after firmware is updated the device will reconnect
        /// However, the connection may not be successful if Baud Rate is changed by firmware
        /// </summary>
        /// <param name="path"></param>
        public async Task UploadFirmware(string path)
        {
            var nextionReady = new ManualResetEventSlim(false);
            var updateComplete = new ManualResetEvent(false);

            this.Initialize((s, e) =>
            {
                try
                {
                    if (_Port.IsOpen == false)
                    {
                        updateComplete.Set();
                        return;
                    }

                    if (_Port.BytesToRead == 1)
                    {
                        var ready = _Port.ReadByte();
                        if (ready == 5)
                        {
                            nextionReady.Set();
                        }

                        return;
                    }

                    // probably nextion power on status
                    if (_Port.BytesToRead > 1)
                    {
                        updateComplete.Set();
                    }
                }
                catch (Exception)
                {
                    updateComplete.Set();
                }

            }, 1);

            this.BatchCommands(new string[]
            {
                "ussp=0",
                "thsp=0",
                "sleep=0",
                "dim=50",
            });

            await Task.Delay(1000);

            var fi = new FileInfo(path);
            NextionDevice.IssueCommand(_Port, string.Format("whmi-wri {0},{1},a", fi.Length, 921600));

            await Task.Delay(2000);

            _Port.BaudRate = 921600;
            _Port.DiscardInBuffer();

            await Task.Delay(2000);

            // Read Firmware and write to device in 4K blocks
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(() =>
            {
                using (var f = File.OpenRead(path))
                {
                    var buffer = new byte[4096];
                    while (true)
                    {
                        nextionReady.Reset();

                        var read = f.Read(buffer, 0, buffer.Length);
                        _Port.Write(buffer, 0, buffer.Length);

                        nextionReady.Wait();

                        if (read < buffer.Length) // last block
                        {
                            break;
                        }
                    }

                }
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            var result = true;
            await Task.Run(() =>
            {
                result = updateComplete.WaitOne(60000);
            });

            if (result == false)
            {
                this.Dispose();
                throw new Exception("Timeout waiting for Nextion to Restart after Update");
            }

            this.Initialize(_Port_DataReceived);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            this.IsDisposed = true;

            if (_Port != null)
            {
                var port = _Port;
                Task.Run(() =>
                {
                    port.DiscardInBuffer();
                    port.DiscardOutBuffer();
                    port.Close();
                    port.Dispose();
                });
            }

            _Port = null;

            GC.SuppressFinalize(this);
        }

    }
}
