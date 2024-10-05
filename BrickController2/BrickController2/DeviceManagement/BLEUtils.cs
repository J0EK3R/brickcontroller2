﻿

namespace BrickController2.DeviceManagement
{
    public static class BLEUtils
    {
        #region Constants
        private static readonly byte[] switchSheet = new byte[]
        {
      0xf4,
      0xa8,
      0xa0,
      0x8c,
      0x28,
      0xec,
      0x44,
      0x00,
      0x6c,
      0x48,
      0x24,
      0x98,
      0xd4,
      0x9c,
      0x0c,
      0xac,
      0xa4,
      0xbc,
      0xcc,
      0x80,
      0x38,
      0xe8,
      0x5c,
      0x1c,
      0x94,
      0xb0,
      0xc8,
      0x54,
      0x34,
      0x08,
      0x74,
      0xf0,
      0xdc,
      0x14,
      0xc4,
      0xc0,
      0x50,
      0x18,
      0x64,
      0x7c,
      0x70,
      0x78,
      0x88,
      0x90,
      0x58,
      0x2c,
      0xf8,
      0x84,
      0x30,
      0x68,
      0x60,
      0x04,
      0x40,
      0x4c,
      0xe0,
      0xb8,
      0xd8,
      0xfc,
      0x20,
      0x10,
      0xe4,
      0x3c,
      0xd0,
      0xb4,
        };
        #endregion

        #region get_rf_payload(byte[] addr, byte[] data, out byte[] rfPayload)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="addr"></param>
        /// <param name="data"></param>
        /// <param name="rfPayload"></param>
        /// <returns></returns>
        public static int get_rf_payload(byte[] addr, byte[] data, out byte[] rfPayload)
        {
            int addrLength = addr.Length;
            int dataLength = data.Length;
            byte data_offset = 0x12;    // 18
            byte inverse_offset = 0x0f; // 15

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
                byte cVar1 = invert_8(resultbuf[index + inverse_offset]);
                resultbuf[index + inverse_offset] = cVar1;
            }

            ushort crc = check_crc16(addr, data);
            resultbuf[result_data_size - 2] = (byte)(crc);
            resultbuf[result_data_size - 1] = (byte)(crc >> 8);


            byte[] ctx_3F = new byte[7]; // int local_58[8];
            whitening_init(0x3f, ctx_3F); // 1111111

            whitenging_encode(resultbuf, 0x12, addrLength + dataLength + 2, ctx_3F);

            byte[] ctx_26 = new byte[7]; // int local_38[8];
            whitening_init(0x26, ctx_26); // 1101110

            whitenging_encode(resultbuf, 0, addrLength + dataLength + 0x14, ctx_26);

            rfPayload = new byte[addrLength + dataLength + 5]; // (signed*)_JNIEnv::GetByteArrayElements(param_1, param_7, (ubyte[])0x0);
            for (int local_bc = 0; local_bc < addrLength + dataLength + 5; local_bc = local_bc + 1)
            {
                rfPayload[local_bc] = resultbuf[local_bc + 0xf];
            }

            return result_data_size;
        }
        #endregion

        #region static void encry(byte[] data)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        public static void encry(byte[] data)
        {
            Encrypt(data);
        }
        #endregion

        #region static void whitening_init(byte val, byte[] ctx)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="val"></param>
        /// <param name="ctx"></param>
        /// <returns></returns>
        private static void whitening_init(byte val, byte[] ctx)
        {
            ctx[0] = 1;
            ctx[1] = (byte)((val >> 5) & 1);
            ctx[2] = (byte)((val >> 4) & 1);
            ctx[3] = (byte)((val >> 3) & 1);
            ctx[4] = (byte)((val >> 2) & 1);
            ctx[5] = (byte)((val >> 1) & 1);
            ctx[6] = (byte)(val & 1);
        }
        #endregion
        #region static void whitenging_encode(byte[] data, int dataStartIndex, int len, byte[] ctx)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="dataStartIndex"></param>
        /// <param name="len"></param>
        /// <param name="ctx"></param>
        private static void whitenging_encode(byte[] data, int dataStartIndex, int len, byte[] ctx)
        {
            for (int index = 0; index < len; index++)
            {
                byte cVar1 = data[dataStartIndex + index];
                int local_20 = 0;
                for (int bitIndex = 0; bitIndex < 8; bitIndex++)
                {
                    byte uVar2 = whitening_output(ctx);
                    local_20 = (int)((uVar2 ^ (int)cVar1 >> ((byte)bitIndex & 0x1f) & 1U) << ((byte)bitIndex & 0x1f)) + local_20;
                }
                data[dataStartIndex + index] = (byte)local_20;
            }
            return;
        }
        #endregion
        #region static byte whitening_output(byte[] ctx)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        private static byte whitening_output(byte[] ctx)
        {
            byte uVar1 = ctx[3];
            byte uVar2 = ctx[6];
            ctx[3] = ctx[2];
            ctx[2] = ctx[1];
            ctx[1] = ctx[0];
            ctx[0] = ctx[6];
            ctx[6] = ctx[5];
            ctx[5] = ctx[4];
            ctx[4] = (byte)(uVar1 ^ uVar2);
            return ctx[0];
        }
        #endregion

        #region static byte invert_8(byte value)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static byte invert_8(byte value)
        {
            int result = 0;
            for (int index = 0; index < 8; index++)
            {
                if (((int)value & 1 << ((byte)index & 0x1f)) != 0)
                {
                    result |= (byte)(1 << (7 - (byte)index & 0x1f));
                }
            }
            return (byte)result;
        }
        #endregion
        #region static ushort invert_16(ushort value)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static ushort invert_16(ushort value)
        {
            int result = 0;
            for (int index = 0; index < 0x10; index++)
            {
                if (((uint)value & 1 << ((byte)index & 0x1f)) != 0)
                {
                    result |= (ushort)(1 << (0xf - (byte)index & 0x1f));
                }
            }
            return (ushort)result;
        }
        #endregion

        #region static ushort check_crc16(byte[] array1, byte[] array2)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="array1"></param>
        /// <param name="array2"></param>
        /// <returns></returns>
        private static ushort check_crc16(byte[] array1, byte[] array2)
        {
            int array1Length = array1.Length;
            int array2Length = array2.Length;

            int result = 0xffff;
            for (int index = 0; index < array1Length; index++)
            {
                result ^= (ushort)((int)array1[(array1Length + -1) - index] << 8);

                for (int local_24 = 0; local_24 < 8; local_24++)
                {
                    if ((result & 0x8000) == 0)
                    {
                        result = result << 1;
                    }
                    else
                    {
                        result = result << 1 ^ 0x1021;
                    }
                }
            }
            for (int index = 0; index < array2Length; index++)
            {
                byte cVar1 = invert_8(array2[index]);

                result = result ^ (ushort)((int)cVar1 << 8);

                for (int local_2c = 0; local_2c < 8; local_2c++)
                {
                    if ((result & 0x8000) == 0)
                    {
                        result = result << 1;
                    }
                    else
                    {
                        result = result << 1 ^ 0x1021;
                    }
                }
            }
            ushort result_inverse = invert_16((ushort)result);
            return (ushort)(result_inverse ^ 0xffff);
        }
        #endregion

        #region static void Encrypt(byte[] data)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        private static void Encrypt(byte[] data)
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
    }
}