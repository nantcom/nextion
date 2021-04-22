using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NC.Nextion
{

    public sealed partial class NextionDevice : IDisposable
    {
        private static readonly string TERMINATION = System.Text.Encoding.ASCII.GetString(new byte[] { 255, 255, 255 });

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
        /// Create Instance of NextionDevice from find result
        /// </summary>
        /// <param name="result"></param>
        public NextionDevice(NextionDeviceFindResult result)
            : this(result.COMPort, result.COMBaudRate, result.ResponseString)
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

        private void ParseReturnString(string returnString)
        {
            if (string.IsNullOrEmpty(returnString))
            {
                throw new ArgumentNullException(returnString);
            }

            // sample:
            // "comok 1,30601-0,NX3224T024_011R,154,61488,DE6970480F7C2E34,4194304";

            var parts = returnString.Substring(6).Split(',');

            this.IsHasTouch = parts[0] == "1";
            this.Model = parts[2];
            this.MCUCode = parts[4];
            this.Serial = parts[5];
            this.FlashSize = int.Parse(parts[6]);

            this.IsSimulator = this.Model == "Simulator";
        }

        /// <summary>
        /// when set to true, parser will wait for 0x05 byte
        /// and return NextionResponse
        /// </summary>
        private bool _IsUploadingFirmware;
        private IObservable<NextionResponse> _ResponseSubject;
        private CancellationTokenSource _ResponseCanceller;
        private ManualResetEvent _WaitForDisconnect = new(false);

        /// <summary>
        /// Split incoming responses
        /// </summary>
        /// <param name="ms"></param>
        /// <returns></returns>
        private IEnumerable<NextionResponse> SplitResponse(MemoryStream ms)
        {
            var buffer = ms.GetBuffer();

            long lastFoundPos = ms.Position;

            while (ms.Position < ms.Length)
            {
                ms.Position += 1;

                if (ms.Position < 3)
                {
                    continue;
                }

                if (buffer[ms.Position - 1] == 255 &&
                    buffer[ms.Position - 2] == 255 &&
                    buffer[ms.Position - 3] == 255)
                {
                    var response = new NextionResponse();

                    response.Code = "0x" + buffer[lastFoundPos].ToString("X2");

                    var hasData = ms.Position - lastFoundPos > 4;
                    if (hasData)
                    {
                        response.ByteData = new byte[(int)ms.Position - 4 - (int)lastFoundPos];
                        Array.Copy(buffer, (int)lastFoundPos + 1, response.ByteData, 0, response.ByteData.Length);
                    }

                    // found ending, return command since lastFoundPosition to just before ending
                    yield return response;

                    lastFoundPos = ms.Position;
                }
            }

        }

        /// <summary>
        /// Connect to Nextion Device and observe for response.
        /// 
        /// The port will be closed/disposed as soon as last observer is disconnected.
        /// 
        /// Upon registration, observer is guaranteed to receive latest response from Nextion Device, which includes a Port object to send data back
        /// 
        /// </summary>
        /// <returns>IDispoable objects which can be used to disconnect from device by calling dispose</returns>
        public IDisposable Connect(Action<NextionResponse> observer, Action onCompleted = null)
        {
            if (_ResponseSubject != null)
            {
                return _ResponseSubject.Subscribe(observer);
            }

            _WaitForDisconnect.Reset();

            _ResponseCanceller = new CancellationTokenSource();
            _ResponseSubject = Observable.Create<NextionResponse>(observer =>
           {
               var port = new SerialPort(this.COMPort, this.COMBaudRate, Parity.None, 8, StopBits.One);

               Task.Run(() =>
               {
                   ManualResetEvent waitCancel = new(false);
                   _ResponseCanceller.Token.Register(() =>
                   {
                       observer.OnCompleted();
                       waitCancel.Set();
                   });

                   port.ErrorReceived += (s, e) =>
                   {
                       // shutdown on error
                       observer.OnCompleted();
                       waitCancel.Set();
                   };

                   port.Open();

                   if (this.IsSimulator == false)
                   {
                       // disable two byte address mode using broadcast
                       NextionDevice.IssueBroadcastCommand(port, "addr=0");

                       // these commands ensure that nextion device is ready to receive further commands
                       NextionDevice.IssueCommandBatch(port, new string[] {
                        "addr=0", // disable two byte address mode
                        "ussp=0", // never sleep when no serial command
                        "thsp=0", // do not sleep when no touch
                        "thup=1", // wake on touch
                        "bkcmd=2", // only error codes
                        "sleep=0", // exit sleep mode
                    });

                   }

                   port.WriteTimeout = 1000;
                   port.DiscardInBuffer();

                   NextionDevice.IssueBroadcastCommand(port, "addr=0");

                   MemoryStream ms = new(1024);
                   port.DataReceived += (s, e) =>
                   {
                       byte[] buffer = null;
                       while (port.BytesToRead > 0)
                       {
                           var toRead = port.BytesToRead;

                           // we try to read into memory stream buffer directly without intermidaries
                           ms.SetLength(ms.Length + toRead);
                           buffer = ms.GetBuffer();
                           ms.Position += port.Read(buffer, (int)ms.Position, toRead);
                       }

                       if (_IsUploadingFirmware)
                       {
                           buffer = ms.GetBuffer();

                           if (buffer[0] == 0x05)
                           {
                               ms.SetLength(0);
                               observer.OnNext(new NextionResponse()
                               {
                                   Code = "0x05",
                                   Port = port
                               });
                           }
                           return;
                       }

                       if (ms.Position == ms.Length &&
                           ms.Position > 0 &&
                           buffer[ms.Length - 1] == 255 && 
                           buffer[ms.Length - 2] == 255 &&
                           buffer[ms.Length - 3] == 255)
                       {
                           ms.Position = 0;
                           foreach (var r in this.SplitResponse(ms))
                           {
                               r.Port = port;
                               observer.OnNext(r);
                           }

                           // clear buffer once we found termination
                           ms.SetLength(0);
                           ms.Position = 0;
                       }

                   };

                   observer.OnNext(new NextionResponse()
                   {
                       Port = port
                   });

                   waitCancel.WaitOne();

                   port.Close();
                   port.Dispose();

                   _WaitForDisconnect.Set();
               });

               return () =>
               {
                   _ResponseCanceller.Cancel();
                   _ResponseCanceller.Dispose();
                   _ResponseSubject = null;
                   _ResponseCanceller = null;
               };

           }).Replay(1).RefCount(1);

            if (onCompleted != null)
            {
                return _ResponseSubject.Subscribe(observer, onCompleted);
            }

            return _ResponseSubject.Subscribe(observer);
        }

        /// <summary>
        /// Forces active connection to close.
        /// Normally connection will close automatically as soon
        /// as no subscriber is subscribed to Connect function
        /// 
        /// Current subscriber will receive OnCompleted if they provide the handler during subscription
        /// </summary>
        public async Task ForceDisconnect(int timeout = 1000)
        {
            if (_ResponseCanceller == null || _ResponseSubject == null)
            {
                return;
            }

            _ResponseCanceller.Cancel();

            bool success = false;
            await Task.Run(() =>
            {
                success = _WaitForDisconnect.WaitOne(timeout);
            });

            if (!success)
            {
                throw new TimeoutException("Timed out waiting for connection to close");
            }
        }

        /// <summary>
        /// Wait until disconnected from device
        /// </summary>
        /// <returns></returns>
        public Task WaitUntilDisconnect()
        {
            return Task.Run(() =>
            {
                _WaitForDisconnect.WaitOne();
            });
        }

        /// <summary>
        /// Issue command and wait for response, automatically change mode to bkcmd=3
        /// and switched back to bkcmd=0 when response is received.
        /// 
        /// In normal operation, User is expected to use connect to send/recive data with nextion
        /// </summary>
        /// <param name="command"></param>
        public async Task<NextionResponse> IssueCommand(string command, int timeout = 1500)
        {
            int step = 0;
            NextionResponse r = null;
            ManualResetEvent wait = new(false);

            IDisposable observer = null;
            observer = this.Connect(callback =>
            {
                switch (step)
                {
                    case 0:
                        // always get response from nextion
                        NextionDevice.IssueCommand(callback.Port, "bkcmd=3");
                        step = 1;
                        return;

                    case 1:
                        NextionDevice.IssueCommand(callback.Port, command);
                        step = 2;
                        return;

                    case 2:
                        r = callback;
                        observer.Dispose();
                        wait.Set();

                        NextionDevice.IssueCommand(callback.Port, "bkcmd=0");
                        return;
                }

            });

            bool waitSuccess = false;
            await Task.Run(() =>
            {
                waitSuccess = wait.WaitOne(timeout);
            });

            if (waitSuccess == false)
            {
                throw new TimeoutException("Timeout waiting for nextion to respond");
            }

            return r;
        }

        /// <summary>
        /// Upload Firmware to nextion device
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task UploadFirmware(byte[] firmwareData)
        {
            var firmware = new MemoryStream(firmwareData);

            NextionSession session = new();

            session.AtFirst().OnAnyCallback((callback) =>
            {
                NextionDevice.IssueCommandBatch(callback.Port, new string[]
                {
                    "ussp=0",
                    "thsp=0",
                    "sleep=0",
                    "dim=50",
                });

                Task.Delay(1000).Wait();

                callback.Port.DiscardInBuffer();
                callback.Port.DiscardOutBuffer();

                _IsUploadingFirmware = true;
                NextionDevice.IssueCommand(callback.Port, $"whmi-wri {firmware.Length},921600,a");

            }, @goto: "waitupload");

            session.When("waitupload").On("0x05", (callback) =>
            {
                _IsUploadingFirmware = true;

                var buffer = new byte[4096];
                var read = firmware.Read(buffer, 0, buffer.Length);

                callback.Port.Write(buffer, 0, read);

                if (firmware.Position == firmware.Length)
                {
                    return "waitForLastBlock";
                }

                return "waitupload";
            });

            session.When("waitForLastBlock").On("0x05", (callback) =>
            {
                _IsUploadingFirmware = false;

            }, @goto: "waitForPowerOn");

            session.When("waitForPowerOn").OnAnyCallback((callback) =>
            {
                session.End();
            });

            await this.ForceDisconnect();

            session.Begin(this);
            await session.WaitAsync(60000);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            _ResponseCanceller?.Cancel();
            _ResponseCanceller?.Dispose();

            GC.SuppressFinalize(this);
        }

    }
}
