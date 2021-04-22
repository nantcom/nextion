using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NC.Nextion
{

    public class NextionResponse
    {
        public string Code { get; set; }

        public string Data { get; set; }

        /// <summary>
        /// Serial Port which could be used to send data back to Nextion Device
        /// </summary>
        public SerialPort Port { get; set; }
    }
}
