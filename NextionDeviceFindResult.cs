using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NC.Nextion
{
    /// <summary>
    /// Search Result
    /// </summary>
    public class NextionDeviceFindResult
    {
        /// <summary>
        /// Response string from Device
        /// </summary>
        public string ResponseString { get; set; }

        /// <summary>
        /// COM Port
        /// </summary>
        public string COMPort { get; set; }

        /// <summary>
        /// Baud Rate of com port to use
        /// </summary>
        public int COMBaudRate { get; set; }
    }
}
