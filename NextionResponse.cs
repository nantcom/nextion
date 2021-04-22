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
        /// <summary>
        /// Response code, in string format for readability
        /// </summary>
        public NextionResponseCode Code { get; set; }

        /// <summary>
        /// String Data from Nextion
        /// </summary>
        public string Data
        {
            get
            {
                if (this.Code == NextionResponseCode.String_Data)
                {
                    return Encoding.ASCII.GetString(this.ByteData);
                }
                return null;
            }
        }

        /// <summary>
        /// Integer data from nextion
        /// if not Integer data response, null will be returned
        /// </summary>
        public int? IntegerData
        {
            get
            {
                if (this.Code == NextionResponseCode.Numeric_Data)
                {
                    /*
                     0x71 0x01 0x02 0x03 0x04 0xFF 0xFF 0xFF
                     * Returned when get command to return a number
                        4 byte 32-bit value in little endian order.
                        (0x01+0x02*256+0x03*65536+0x04*16777216)
                        data: 67305985*/
                    return  this.ByteData[0] +
                           (this.ByteData[1] * 256) +
                           (this.ByteData[2] * 65536) +
                           (this.ByteData[3] * 16777216);
                }

                return null;
            }
        }

        /// <summary>
        /// Touch Coordinate data        /// 
        /// if response is not touch coordinate, pair of -1 will be returned
        /// </summary>
        public (int x, int y, bool pressed) TouchCoords
        {
            get
            {
                if (this.Code == NextionResponseCode.Touch_Coordinate ||
                    this.Code == NextionResponseCode.Touch_Coordinate_SleepMode)
                {
                    return (
                        (this.ByteData[0] * 256) + this.ByteData[1],
                        (this.ByteData[2] * 256) + this.ByteData[3],
                        this.ByteData[4] == 1
                    );
                }

                return (-1, -1, false);
            }
        }


        /// <summary>
        /// Touch Event information Coordinate data
        /// if response is not touch event, -1 will be returned
        /// </summary>
        public (int pageNumber, int componentId, bool pressed) TouchEventInfo
        {
            get
            {
                if (this.Code == NextionResponseCode.Touch_Event)
                {
                    return (this.ByteData[0], this.ByteData[1], this.ByteData[2] == 1);
                }

                return (-1, -1, false);
            }
        }

        /// <summary>
        /// Page number data
        /// </summary>
        public int PageNumber
        {
            get
            {
                if (this.Code == NextionResponseCode.Current_Page_Number)
                {
                    return this.ByteData[0];
                }

                return -1;
            }
        }

        /// <summary>
        /// Raw Byte Data from Nextion
        /// </summary>
        public byte[] ByteData { get; set; }

        /// <summary>
        /// Serial Port which could be used to send data back to Nextion Device
        /// </summary>
        public SerialPort Port { get; set; }
        
    }
}
