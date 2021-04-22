using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NC.Nextion
{
    /// <summary>
    /// Use this class with NextionSession On() function to have more readable session code
    /// </summary>
    public class NextionResponseCode
    {
        public const string Invalid_Instruction_Or_StartUp ="0x00";
        public const string Instruction_Successful = "0x01";
        public const string Invalid_ComponentID = "0x02";
        public const string Invalid_PageID = "0x03";
        public const string Invalid_PictureID = "0x04";
        public const string Invalid_FontID = "0x05";
        public const string Invalid_File_Operation = "0x06";
        public const string Invalid_CRC = "0x09";
        public const string Invalid_Baud_rate_Setting = "0x11";
        public const string Invalid_Waveform_ID_or_Channel = "0x12";
        public const string Invalid_Variable_Name_Or_Attribute = "0x1A";
        public const string Invalid_Variable_Operation = "0x1B";
        public const string Assignment_Failed_to_Assign = "0x1C";
        public const string EEPROM_Operation_Failed = "0x1D";
        public const string Invalid_Quantity_of_Parameters = "0x1E";
        public const string IO_Operation_Failed = "0x1F";
        public const string Escape_Character_Invalid = "0x20";
        public const string Variable_Name_Too_Long = "0x23";

        public const string Serial_Buffer_Overflow = "0x24";
        public const string Touch_Event = "0x65";
        public const string Current_Page_Number = "0x66";
        public const string Touch_Coordinate = "0x67";
        public const string Touch_Coordinate_SleepMode = "0x68";
        public const string String_Data = "0x70";
        public const string Numeric_Data = "0x71";
        public const string Auto_Entered_Sleep_Mode = "0x86";
        public const string Auto_Wake_from_Sleep = "0x87";
        public const string Nextion_Ready = "0x88";
        public const string Start_microSD_Upgrade = "0x89";
        public const string Transparent_Data_Finished = "0xFD";
        public const string Transparent_Data_Ready = "0xFE";

        public string Code { get; private set; }

        private Dictionary<string, string> _Map;

        /// <summary>
        /// Gets description
        /// </summary>
        public string Description
        {
            get
            {
                if (_Map == null)
                {
                    _Map = typeof(NextionResponseCode).GetFields(BindingFlags.Public | BindingFlags.Static |
                          BindingFlags.FlattenHierarchy)
                           .Where(fi => fi.IsLiteral && !fi.IsInitOnly)
                           .ToDictionary(fi => (string)fi.GetValue(this), fi => fi.Name.Replace("_", ""));
                }

                return _Map[this.Code];
            }
        }

        public override string ToString()
        {
            return $"Nextion Response:{this.Description}";
        }

        public static implicit operator string(NextionResponseCode code) => code == null ? null : code.Code;

        public static implicit operator NextionResponseCode(string code) => new NextionResponseCode() { Code = code };
    }
}
