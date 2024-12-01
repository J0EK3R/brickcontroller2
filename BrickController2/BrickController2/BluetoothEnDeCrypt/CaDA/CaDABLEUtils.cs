
using System.Text;

namespace BluetoothEnDeCrypt.CaDA
{
    public static class CaDABLEUtils
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
                return EnCrypt(addr, data, CaDABLEUtils.CTXValue);
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
                CaDABLEUtils.Get_rf_payload(addr, data, 0x26, out rfPayload);
                return rfPayload;
            }
            #endregion
        }
        #endregion

        #region Constants
        /// <summary>
        /// CTXValue for Encryption
        /// </summary>
        public const byte CTXValue = 0x26;

        /// <summary>
        /// 
        /// </summary>
        public static readonly byte[] AddressArray =
        {
            67, // 0x43
            65, // 0x41
            82, // 0x52
        };

        /// <summary>
        /// LookupTable
        /// </summary>
        private static readonly byte[] switchSheet = new byte[]
        {
            0xf4, 0xa8, 0xa0, 0x8c, 0x28, 0xec, 0x44, 0x00, 0x6c, 0x48, 0x24, 0x98, 0xd4, 0x9c, 0x0c, 0xac,
            0xa4, 0xbc, 0xcc, 0x80, 0x38, 0xe8, 0x5c, 0x1c, 0x94, 0xb0, 0xc8, 0x54, 0x34, 0x08, 0x74, 0xf0,
            0xdc, 0x14, 0xc4, 0xc0, 0x50, 0x18, 0x64, 0x7c, 0x70, 0x78, 0x88, 0x90, 0x58, 0x2c, 0xf8, 0x84,
            0x30, 0x68, 0x60, 0x04, 0x40, 0x4c, 0xe0, 0xb8, 0xd8, 0xfc, 0x20, 0x10, 0xe4, 0x3c, 0xd0, 0xb4,
        };
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

            resultbuf[0x0f] = 0x71; //  'q';
            resultbuf[0x10] = 0x0f; // '\x0f';
            resultbuf[0x11] = 0x55; // 'U';

            for (int index = 0; index < addrLength; index++)
            {
                resultbuf[index + data_offset] = addr[addrLength - index - 1];
            }

            for (int index = 0; index < dataLength; index++)
            {
                resultbuf[addrLength + data_offset + index] = data[index];
            }

            for (int index = 0; index < addrLength + 3; index++)
            {
                byte cVar1 = DeCryptTools.Invert_8(resultbuf[index + inverse_offset]);
                resultbuf[index + inverse_offset] = cVar1;
            }

            ushort crc = DeCryptTools.Check_crc16(addr, data);
            resultbuf[result_data_size - 2] = (byte)(crc);
            resultbuf[result_data_size - 1] = (byte)(crc >> 8);

            byte[] ctx_0x3F = new byte[7];
            DeCryptTools.Whitening_init(0x3f, ctx_0x3F); // 0x3f (63): 1111111
            DeCryptTools.Whitening_encode(resultbuf, 0x12, addrLength + dataLength + 2, ctx_0x3F);

            byte[] ctx_0x26 = new byte[7];
            DeCryptTools.Whitening_init(ctxValue, ctx_0x26); // 0x26 (38): 1101110
            DeCryptTools.Whitening_encode(resultbuf, 0, addrLength + dataLength + 0x14, ctx_0x26);

            rfPayload = new byte[addrLength + dataLength + 5];
            for (int local_bc = 0; local_bc < addrLength + dataLength + 5; local_bc = local_bc + 1)
            {
                rfPayload[local_bc] = resultbuf[local_bc + 0xf];
            }

            return result_data_size;
        }
        #endregion

        #region static void Encrypt(byte[] data)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        public static void Encrypt(byte[] data)
        {
            byte bVar1;
            byte uVar2;

            if ((data[0] & 1) != 0)
            {
                bVar1 = data[2];
                data[2] = (byte)(data[2] & 0xf0);
                data[2] = (byte)(data[2] | data[6] & 0xf);
                data[6] = (byte)(data[6] & 0xf0);
                data[6] = (byte)(data[6] | bVar1 & 0xf);
            }
            if ((data[0] & 2) != 0)
            {
                bVar1 = data[2];
                data[2] = (byte)(data[2] & 0xf);
                data[2] = (byte)(data[2] | (byte)((data[5] & 0xf) << 4));
                data[5] = (byte)(data[5] & 0xf0);
                data[5] = (byte)(data[5] | (byte)((int)(uint)(bVar1 & 0xf0) >> 4));
            }
            if ((data[0] & 4) != 0)
            {
                uVar2 = data[3];
                data[3] = (byte)(data[3] & 0xf0);
                data[3] = (byte)(data[3] | (byte)((int)(data[4] & 0xf0) >> 4));
                data[4] = (byte)(data[4] & 0xf);
                data[4] = (byte)(data[4] | uVar2 << 4);
            }
            if ((data[0] & 8) != 0)
            {
                bVar1 = data[3];
                data[3] = (byte)(data[3] & 0xf);
                data[3] = (byte)(data[3] | (byte)((data[4] & 0xf) << 4));
                data[4] = (byte)(data[4] & 0xf0);
                data[4] = (byte)(data[4] | (byte)((int)(uint)(bVar1 & 0xf0) >> 4));
            }
            if ((data[0] & 0x10) != 0)
            {
                bVar1 = data[5];
                data[5] = (byte)(data[5] & 0xf);
                data[5] = (byte)(data[5] | data[7] & 0xf0);
                data[7] = (byte)(data[7] & 0xf);
                data[7] = (byte)(data[7] | bVar1 & 0xf0);
            }
            if ((data[0] & 0x20) != 0)
            {
                bVar1 = data[6];
                data[6] = (byte)(data[6] & 0xf);
                data[6] = (byte)(data[6] | (byte)((data[7] & 0xf) << 4));
                data[7] = (byte)(data[7] & 0xf0);
                data[7] = (byte)(data[7] | (byte)((int)(uint)(bVar1 & 0xf0) >> 4));
            }
            if ((data[0] & 0x40) != 0)
            {
                uVar2 = data[2];
                data[2] = (byte)(data[2] & 0xf0);
                data[2] = (byte)(data[2] | (byte)((int)(data[3] & 0xf0) >> 4));
                data[3] = (byte)(data[3] & 0xf);
                data[3] = (byte)(data[3] | uVar2 << 4);
            }
            if ((data[0] & 0x80) != 0)
            {
                bVar1 = data[2];
                data[2] = (byte)(data[2] & 0xf);
                data[2] = (byte)(data[2] | (byte)((data[3] & 0xf) << 4));
                data[3] = (byte)(data[3] & 0xf0);
                data[3] = (byte)(data[3] | (byte)((int)(uint)(bVar1 & 0xf0) >> 4));
            }
            data[2] = (byte)(data[2] ^ data[1] ^ 0x69);
            data[3] = (byte)(data[3] ^ data[1] ^ 0x69);
            data[4] = (byte)(data[4] ^ data[1] ^ 0x69);
            data[5] = (byte)(data[5] ^ data[1] ^ 0x69);
            data[6] = (byte)(data[6] ^ data[1] ^ 0x69);
            data[7] = (byte)(data[7] ^ data[1] ^ 0x69);
            for (int index = 0; index < 8; index++)
            {
                data[index] = (byte)(switchSheet[(int)(data[index] / 4)] + data[index] % 4);
            }
        }
        #endregion

        #region static int UnmaskArray(byte[] bArr, int i, int i2)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="bArr"></param>
        /// <param name="i"></param>
        /// <param name="i2"></param>
        /// <returns></returns>
        public static int UnmaskArray(byte[] bArr, int i, int i2)
        {
            return UnmaskArray(bArr, i, i2, true);
        }
        #endregion
        #region static int UnmaskArray(byte[] bArr, int offset, int count, bool z)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="bArr"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="z"></param>
        /// <returns></returns>
        public static int UnmaskArray(byte[] bArr, int offset, int count, bool z)
        {
            int result;
            int index = 0;
            if (z)
            {
                result = 0;
                while (index < count)
                {
                    result |= (bArr[offset + index] & 255) << (((count - 1) - index) * 8);
                    index++;
                }
            }
            else
            {
                result = 0;
                while (index < count)
                {
                    result |= (bArr[offset + index] & 255) << (index * 8);
                    index++;
                }
            }
            return result;
        }
        #endregion
        #region static byte[] CreateMaskArray(int i, int length)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="i"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static byte[] CreateMaskArray(int i, int length)
        {
            byte[] bArr = new byte[length];
            for (int index = 0; index < length; index++)
            {
                bArr[index] = (byte)((i >> (((length - 1) - index) * 8)) & 255);
            }
            return bArr;
        }
        #endregion

        #region static string ArrayToHexString(byte[] bArr)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="bArr"></param>
        /// <returns></returns>
        public static string ArrayToHexString(byte[] bArr)
        {
            StringBuilder stringBuffer = new StringBuilder();
            stringBuffer.Append($"Length= {bArr.Length:D4}: ");


            foreach (byte b2 in bArr)
            {
                //stringBuffer.Append("|");
                stringBuffer.Append(" ");
                stringBuffer.Append($"0x{(b2 & 255):X2}");
            }
            return stringBuffer.ToString();
        }
        #endregion
    }
}
