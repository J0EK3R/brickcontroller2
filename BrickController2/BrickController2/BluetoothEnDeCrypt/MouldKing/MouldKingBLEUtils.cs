using System;

namespace BluetoothEnDeCrypt.MouldKing
{
    /// <summary>
    /// static class wich implements the encryption algorithm for the advertising data
    /// </summary>
    public class MouldKingBLEUtils
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
                return this.EnCrypt(addr, data, MouldKingBLEUtils.CTXValue);
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
                MouldKingBLEUtils.Get_rf_payload(addr, data, ctxValue, out rfPayload);
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

        /// <summary>
        /// Address array
        /// </summary>
        public static readonly byte[] AddressArray = new byte[] { 0xC1, 0xC2, 0xC3, 0xC4, 0xC5 };
        #endregion

        #region static byte Get_rf_payload(byte[] addr, byte[] data, out byte[] rfPayload)
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

            byte data_offset = 0x12; // 0x12 (18)
            byte inverse_offset = 0x0f; // 0x0f (15)

            int result_data_size = data_offset + addrLength + dataLength + 2;
            byte[] resultbuf = new byte[result_data_size];

            resultbuf[15] = 113; // 0x71
            resultbuf[16] = 15;  // 0x0f
            resultbuf[17] = 85;  // 0x55

            // copy firstDataArray reverse into targetArray with offset 18
            for (int index = 0; index < addrLength; index++)
            {
                //resultbuf[data_offset + addrLength - index - 1] = addr[index];
                resultbuf[index + data_offset] = addr[(addrLength - index) - 1];
            }

            //Buffer.BlockCopy(data, 0, resultbuf, addrLength + data_offset, dataLength);
            // copy dataArray into resultbuf with offset 18 + addrLength
            for (int index = 0; index < dataLength; index++)
            {
                resultbuf[data_offset + addrLength + index] = data[index];
            }

            // crypt Bytes from position 15 to 22
            for (int index = inverse_offset; index < addrLength + data_offset; index++)
            {
                resultbuf[index] = DeCryptTools.Invert_8(resultbuf[index]);
            }

            // calc checksum und copy to array
            int checksum = DeCryptTools.Check_crc16(addr, data);
            resultbuf[result_data_size - 2] = (byte)(checksum & 255);
            resultbuf[result_data_size - 1] = (byte)((checksum >> 8) & 255);

            byte[] ctx_0x3F = new byte[7]; // int local_58[8];
            DeCryptTools.Whitening_init(0x3f, ctx_0x3F); // 0x3f (63) -> ctx_0x3F = [1111111]
            DeCryptTools.Whitening_encode(resultbuf, 0x12, addrLength + dataLength + 2, ctx_0x3F);

            byte[] ctx = new byte[7];
            DeCryptTools.Whitening_init(ctxValue, ctx); // ctxValue= 0x25 (37) -> ctx = [1101110]
            DeCryptTools.Whitening_encode(resultbuf, 0, result_data_size, ctx);

            // resulting advertisment array has a length of constant 24 bytes
            rfPayload = new byte[24];

            int lengthResultArray = addrLength + dataLength + 5;

            if (lengthResultArray > rfPayload.Length)
            {
                return 0;
            }

            Buffer.BlockCopy(resultbuf, 15, rfPayload, 0, lengthResultArray);

            // fill rest of array
            for (int index = lengthResultArray; index < rfPayload.Length; index++)
            {
                rfPayload[index] = (byte)(index + 1);
            }

            return rfPayload.Length;
        }
        #endregion
    }
}
