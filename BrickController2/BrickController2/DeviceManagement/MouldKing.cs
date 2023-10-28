using BrickController2.PlatformServices.BluetoothLE;
using System;
using System.Text;

namespace BrickController2.DeviceManagement
{
    /// <summary>
    /// </summary>
    internal abstract class MouldKing<_TelegramType> : BluetoothAdvertisingDevice<_TelegramType>
    {
        #region Constants
        /// <summary>
        /// ManufacturerID for BLEAdvertisments
        /// hex: 0xFFF0
        /// dec: 65520
        /// </summary>
        public const ushort ManufacturerID = 0xFFF0;

        private static readonly byte[] Array_C1C2C3C4C5 = HexString_To_ByteArray(Encoding.ASCII.GetBytes("C1C2C3C4C5"));
        #endregion

        #region Constructor
        public MouldKing(string name, string address, byte[] deviceData, IDeviceRepository deviceRepository, IBluetoothLEService bleService)
                : base(name, address, deviceData, deviceRepository, bleService)
        {
        }
        #endregion

        #region Crypt(byte[] rawDataArray)
        protected override byte[] Crypt(byte[] rawDataArray)
        {
            int targetArrayLength = Array_C1C2C3C4C5.Length + rawDataArray.Length + 20;

            byte[] targetArray = new byte[targetArrayLength];
            targetArray[15] = 113; // 0x71
            targetArray[16] = 15;  // 0x0f
            targetArray[17] = 85;  // 0x55

            // copy firstDataArray reverse into targetArray with offset 18
            for (int index = 0; index < Array_C1C2C3C4C5.Length; index++)
            {
                targetArray[index + 18] = Array_C1C2C3C4C5[(Array_C1C2C3C4C5.Length - index) - 1];
            }

            // copy secondDataArray into targetArray with offset 18 + firstDataArray.Length
            //for (int i2 = 0; i2 < rawDataArray.Length; i2++)
            //{
            //    targetArray[Array_C1C2C3C4C5.Length + 18 + i2] = rawDataArray[i2];
            //}
            Buffer.BlockCopy(rawDataArray, 0, targetArray, Array_C1C2C3C4C5.Length + 18, rawDataArray.Length);

            // crypt Bytes from position 15 to 22
            for (int index = 15; index < Array_C1C2C3C4C5.Length + 18; index++)
            {
                targetArray[index] = CryptByte(targetArray[index]);
            }

            // calc checksum und copy to array
            int checksum = CalcChecksumFromArrays(Array_C1C2C3C4C5, rawDataArray);
            targetArray[Array_C1C2C3C4C5.Length + 18 + rawDataArray.Length + 0] = (byte)(checksum & 255);
            targetArray[Array_C1C2C3C4C5.Length + 18 + rawDataArray.Length + 1] = (byte)((checksum >> 8) & 255);

            // crypt bytes from offset 18 to the end with magicNumberArray_63
            int[] magicNumberArray_63 = new int[7];
            FillMagicArray(63, magicNumberArray_63);

            int tempArrayLength = targetArrayLength - 18;
            byte[] tempArray = new byte[tempArrayLength];
            Buffer.BlockCopy(targetArray, 18, tempArray, 0, tempArrayLength);   // copy from offset 18 to end to bArr4

            CryptArray(tempArray, magicNumberArray_63);
            Buffer.BlockCopy(tempArray, 0, targetArray, 18, tempArray.Length);  // copy back to target array

            // crypt complete array with magicNumberArray_37
            int[] magicNumberArray_37 = new int[7];
            FillMagicArray(37, magicNumberArray_37);
            CryptArray(targetArray, magicNumberArray_37);

            // resulting advertisment array has a length of constant 24 bytes
            byte[] telegramArray = new byte[24];

            int lengthResultArray = Array_C1C2C3C4C5.Length + rawDataArray.Length + 5;
            Buffer.BlockCopy(targetArray, 15, telegramArray, 0, lengthResultArray);

            // fill rest of array
            for(int index = lengthResultArray; index < telegramArray.Length; index++)
            {
                telegramArray[index] = (byte)index;
            }

            return telegramArray;
        }
        #endregion

        #region HexString_To_ByteArray(byte[] bArr)
        /// <summary>
        /// Converts the HexString to a ByteArray
        /// </summary>
        /// <param name="bArr"></param>
        /// <returns></returns>
        private static byte[] HexString_To_ByteArray(byte[] bArr)
        {
            // check length is a multiple of 2
            if (bArr.Length % 2 != 0)
            {
                return null;
            }

            // Encoding.ASCII.GetBytes("C1C2C3C4C5") -> byte[] { 0xc1, 0xc2, 0xc3, 0xc4, 0xc5 }
            // Encoding.ASCII.GetBytes("111213141516" -> byte[] { 0x11, 0x12, 0x13, 0x14, 0x15, 0x16 }

            int i;
            int i2 = 0;
            int i3;
            int i4 = 0;
            int length = (bArr.Length + 1) / 2;
            byte[] bArr2 = new byte[length];
            for (int i5 = 0; i5 < length; i5++)
            {
                int i6 = i5 * 2;
                byte b = bArr[i6];
                if (b < 48 || b > 57)
                {
                    if (b >= 97 && b <= 102)
                    {
                        i2 = b - 97;
                    }
                    else if (b < 65 || b > 70)
                    {
                        i = 0;
                    }
                    else
                    {
                        i2 = b - 65;
                    }
                    i = i2 + 10;
                }
                else
                {
                    i = b - 48;
                }
                byte b2 = bArr[i6 + 1];
                if (b2 < 48 || b2 > 57)
                {
                    if (b2 >= 97 && b2 <= 102)
                    {
                        i4 = b2 - 97;
                    }
                    else if (b2 < 65 || b2 > 70)
                    {
                        i3 = 0;
                    }
                    else
                    {
                        i4 = b2 - 65;
                    }
                    i3 = i4 + 10;
                }
                else
                {
                    i3 = b2 - 48;
                }
                bArr2[i5] = (byte)((i << 4) + i3);
            }
            return bArr2;
        }
        #endregion

        #region CFillMagicArray(int magicNumber, int[] iArr)
        /// <summary>
        /// Fills the first 7 bytes of the byte[] with bytes calculated from the magicnumber
        /// </summary>
        /// <param name="magicNumber"></param>
        /// <param name="iArr"></param>
        private static void FillMagicArray(int magicNumber, int[] iArr)
        {
            iArr[0] = 1;
            for (int i2 = 1; i2 < 7; i2++)
            {
                iArr[i2] = (magicNumber >> (6 - i2)) & 1;
            }
        }
        #endregion
        #region CryptByte(byte b)
        private static byte CryptByte(byte b)
        {
            byte b2 = 0;
            for (byte b3 = 0; b3 < 8; b3 = (byte)(b3 + 1))
            {
                if (((1 << b3) & b) != 0)
                {
                    b2 = (byte)(b2 | (1 << (7 - b3)));
                }
            }
            return b2;
        }
        #endregion
        #region CryptInt(int i)
        private static int CryptInt(int i)
        {
            int i2 = 0;
            for (int i3 = 0; i3 < 16; i3++)
            {
                if (((1 << i3) & i) != 0)
                {
                    i2 |= 1 << (15 - i3);
                }
            }
            return 65535 & i2;
        }
        #endregion
        #region CryptArray(byte[] bArr, int[] iArr)
        private static byte[] CryptArray(byte[] bArr, int[] magicNumberArray)
        {
            // foreach byte of array
            for (int indexByte = 0; indexByte < bArr.Length; indexByte++)
            {
                byte currentByte = bArr[indexByte];
                int currentResult = 0;
                // foreach bit in byte
                for (int indexBit = 0; indexBit < 8; indexBit++)
                {
                    currentResult += (((currentByte >> indexBit) & 1) ^ ShiftArray(magicNumberArray)) << indexBit;
                }
                bArr[indexByte] = (byte)(currentResult & 255);
            }
            return bArr;
        }
        #endregion
        #region CalcChecksumFromArrays(byte[] bArr, byte[] bArr2)
        private static int CalcChecksumFromArrays(byte[] bArr, byte[] bArr2)
        {
            int i = 65535;
            for (int i2 = 0; i2 < bArr.Length; i2++)
            {
                i = (i ^ (bArr[(bArr.Length - 1) - i2] << 8)) & 65535;
                for (int i3 = 0; i3 < 8; i3++)
                {
                    int i4 = i & 32768;
                    i <<= 1;
                    if (i4 != 0)
                    {
                        i ^= 4129;
                    }
                }
            }

            // for (byte b : bArr2)
            foreach (byte b in bArr2)
            {
                i = ((CryptByte(b) << 8) ^ i) & 65535;
                for (int i5 = 0; i5 < 8; i5++)
                {
                    int i6 = i & 32768;
                    i <<= 1;
                    if (i6 != 0)
                    {
                        i ^= 4129;
                    }
                }
            }
            return CryptInt(i) ^ 65535;
        }
        #endregion
        #region ShiftArray(int[] iArr)
        private static int ShiftArray(int[] iArr)
        {
            //iArr[3] = iArr[2];
            //iArr[2] = iArr[1];
            //iArr[1] = iArr[0];
            //iArr[0] = iArr[6];
            //iArr[6] = iArr[5];
            //iArr[5] = iArr[4];
            //iArr[4] = iArr[3] ^ iArr[6];
            //return iArr[0];
            int r0 = 3;
            int r1 = iArr[r0];
            int r2 = 6;
            int r3 = iArr[r2];
            r1 = r1 ^ r3;
            r3 = 2;
            int r4 = iArr[r3];
            iArr[r0] = r4;
            r0 = 1;
            r4 = iArr[r0];
            iArr[r3] = r4;
            r3 = 0;
            r4 = iArr[r3];
            iArr[r0] = r4;
            r0 = iArr[r2];
            iArr[r3] = r0;
            r0 = 5;
            r4 = iArr[r0];
            iArr[r2] = r4;
            r2 = 4;
            r4 = iArr[r2];
            iArr[r0] = r4;
            iArr[r2] = r1;
            int r6 = iArr[r3];
            return r6;
        }
        #endregion
    }
}
