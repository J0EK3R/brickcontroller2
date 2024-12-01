
namespace BluetoothEnDeCrypt.PowerBox
{
    public static class PowerBoxBLEUtils
    {
        #region class Crypt
        public class Crypt :
          ICrypt
        {
            #region Crypt()
            /// <summary>
            /// Constructor
            /// </summary>
            public Crypt()
            {
            }
            #endregion

            #region byte[] EnCrypt(byte[] addr, byte[] data)
            /// <summary>
            /// crypt data-array with addr and ctxvalue
            /// </summary>
            /// <param name="addr">address array</param>
            /// <param name="data">data array to encrypt</param>
            /// <returns>crypted array</returns>
            public byte[] EnCrypt(byte[] addr, byte[] data)
            {
                return EnCrypt(addr, data, PowerBoxBLEUtils.CTXValue);
            }
            #endregion
            #region byte[] EnCrypt(byte[] addr, byte[] data, byte ctxValue)
            /// <summary>
            /// crypt data-array with addr and ctxvalue
            /// </summary>
            /// <param name="addr">address array</param>
            /// <param name="data">data array to encrypt</param>
            /// <param name="ctxValue">ctx value for encryption</param>
            /// <returns>crypted array</returns>
            public byte[] EnCrypt(byte[] addr, byte[] data, byte ctxValue)
            {
                byte[] rfPayload;
                PowerBoxBLEUtils.Get_rf_payload(addr, data, CTXValue, out rfPayload);
                return rfPayload;
            }
            #endregion
        }
        #endregion

        #region Constants
        /// <summary>
        /// CTXValue for Encryption
        /// </summary>
        public const byte CTXValue = 0x25;

        public static byte[] AddressArray = { 0xC1, 0xC2, 0xC3, 0xC4, 0xC5 };
        #endregion

        #region static int Get_rf_payload(byte[] addr, byte[] data, out byte[] rfPayload)
        /// <summary>
        /// crypt data-array with addr and ctxvalue
        /// </summary>
        /// <param name="addr">address array</param>
        /// <param name="data">data array to encrypt</param>
        /// <param name="ctxValue">ctx value for encryption</param>
        /// <param name="rfPayload">crypted array</param>
        /// <returns>size of crypted array</returns>
        public static int Get_rf_payload(byte[] addr, byte[] data, byte ctxValue, out byte[] rfPayload)
        {
            int addrLength = addr.Length;
            int dataLength = data.Length;
            byte data_offset = 0x12;    // 0x12 (18)
            byte inverse_offset = 0x0f; // 0x0f (15)

            int length_24 = addrLength + dataLength + data_offset;
            int result_data_size = length_24 + 2;

            byte[] resultbuf = new byte[result_data_size];

            resultbuf[0x0f] = 0x71; 
            resultbuf[0x10] = 0x0f; 
            resultbuf[0x11] = 0x55; 

            // reverse copy addr-array to resultbuf at data_offset (0x12)
            for (int index = 0; index < addrLength; index++)
            {
                resultbuf[index + data_offset] = addr[addrLength - index - 1];
            }

            // copy data-array to resultbuf at data_offset + addrLength
            for (int index = 0; index < dataLength; index++)
            {
                resultbuf[addrLength + data_offset + index] = data[index];
            }

            // invert bytes in resultbuf fromn inverse_offset to addrLength + 3
            for (int index = 0; index < addrLength + 3; index++)
            {
                byte cVar1 = DeCryptTools.Invert_8(resultbuf[index + inverse_offset]);
                resultbuf[index + inverse_offset] = cVar1;
            }

            // calc crc16 from addr-array and data-array
            ushort crc = DeCryptTools.Check_crc16(addr, data);

            // copy crc16 to last 2 bytes of resultArray
            resultbuf[result_data_size - 2] = (byte)(crc);
            resultbuf[result_data_size - 1] = (byte)(crc >> 8);


            byte[] ctx_0x3F = new byte[7];
            DeCryptTools.Whitening_init(0x3f, ctx_0x3F); // 0x3f (63): 1111111
            DeCryptTools.Whitening_encode(resultbuf, 0x12, addrLength + dataLength + 2, ctx_0x3F);


            byte[] ctx_0x25 = new byte[7];
            DeCryptTools.Whitening_init(ctxValue, ctx_0x25); // 1101110
            DeCryptTools.Whitening_encode(resultbuf, 0, length_24 + 2, ctx_0x25);

            rfPayload = new byte[addrLength + dataLength + 5];

            // copy resultbuf from 0xf to rfPayload
            for (int local_bc = 0; local_bc < addrLength + dataLength + 5; local_bc = local_bc + 1)
            {
                rfPayload[local_bc] = resultbuf[local_bc + 0xf];
            }

            return result_data_size;
        }
        #endregion
    }
}
