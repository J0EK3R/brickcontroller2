using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BluetoothEnDeCrypt
{
    /// <summary>
    /// static class wich implements the encryption algorithm for the advertising data
    /// </summary>
    public static class DeCryptTools
    {
        #region static byte[] FromWiresharkString(string stringArray)
        /// <summary>
        /// Converts a wireshark export string to byte[]
        /// </summary>
        /// <param name="stringArray">string from wireshark export</param>
        /// <param name="offset">offset</param>
        /// <returns>byte[]</returns>
        public static byte[] FromWiresharkString(string stringArray, int offset = 0)
        {
            string[] bytes = stringArray.Split(' ');

            byte[] result = new byte[bytes.Length - offset];

            for (int index = 0; index < result.Length; index++)
            {
                result[index] = byte.Parse(bytes[index + offset], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return result;
        }
        #endregion

        #region static string ArrayToHexString(byte[] bArr)
        /// <summary>
        /// converts a byte[] to string for output
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

        #region static byte[] EnCrypt(ICrypt crypt, byte[] rawDataArray)
        /// <summary>
        /// do encryption
        /// </summary>
        /// <param name="crypt">crypt-class</param>
        /// <param name="addr">byte[] with addr</param>
        /// <param name="data">byte[] with data</param>
        /// <param name="ctxValue">ctxValue for encryption</param>
        /// <returns></returns>
        public static byte[] EnCrypt(ICrypt crypt, byte[] addr, byte[] data, byte ctxValue)
        {
            return crypt.EnCrypt(addr, data, ctxValue);
        }
        #endregion
        #region static TryDecryptBruteForce(byte[] cryptDataArray, out byte[] result, Action<byte[]> output)
        /// <summary>
        /// Tries to decrypt the given byte[]
        /// </summary>
        /// <param name="crypt">crypt-class</param>
        /// <param name="addr">byte[] with addr</param>
        /// <param name="cryptDataArray">Array of with crypted data</param>
        /// <param name="ctxValue">ctxValue for encryption</param>
        /// <param name="resultArray">resulting Array or null</param>
        /// <param name="output">output callback or null</param>
        /// <returns>True, if success. Else False.</returns>
        public static bool TryDecryptBruteForce(ICrypt crypt, byte[] addr, byte[] cryptDataArray, byte ctxValue, out byte[] resultArray, Action<string> output = null)
        {
            for (int startOffset = 0; startOffset < cryptDataArray.Length; startOffset++)
            {
                List<byte> resultList = new List<byte>();

                // cryptDataArray.Length - 8 - 2
                //                         L header
                //                             L 2 bytes for checksum
                for (int byteOffset = 0; byteOffset < cryptDataArray.Length - startOffset - 2; byteOffset++)
                {
                    // add new byte in result list
                    resultList.Add(0x00);

                    // brute force from 0x00 to 0xFF
                    for (int currentVariant = 0x00; currentVariant <= 0xFF; currentVariant++)
                    {
                        //Console.WriteLine($"startOffset: {startOffset}, byteOffset: {byteOffset}, variant: {currentVariant}");

                        resultList[byteOffset] = (byte)currentVariant;

                        // calculate intermediate array to compare
                        byte[] cryptIntermediateResultArray = crypt.EnCrypt(addr, resultList.ToArray(), ctxValue);

                        if (cryptIntermediateResultArray.Length > cryptDataArray.Length)
                        {
                            byteOffset = cryptDataArray.Length - startOffset - 2; // also break for-loop "byteOffset"
                            break;
                        }
                        else if (cryptIntermediateResultArray.Length <= byteOffset + startOffset)
                        {
                            break;
                        }

                        output?.Invoke($"startOffset: {startOffset,2} byteOffset: {byteOffset,2} variant: 0x{currentVariant:X2} - {DeCryptTools.ArrayToHexString(cryptIntermediateResultArray)}");

                        byte currentSetByte = cryptDataArray[byteOffset + startOffset];
                        byte currentByte = cryptIntermediateResultArray[byteOffset + startOffset];

                        // check if current byte matches the byte in the given array
                        if (currentByte == currentSetByte)
                        {
                            // check if the given array equals the resulting array
                            if (cryptDataArray.SequenceEqual(cryptIntermediateResultArray))
                            {
                                // here we are...
                                resultArray = resultList.ToArray();
                                return true;
                            }

                            break;
                        }
                    }
                }
            }

            resultArray = null;
            return false;
        }
        #endregion

        #region static byte Invert_8(byte value)
        /// <summary>
        /// inverts the bits of a given byte
        /// </summary>
        /// <param name="value">byte to invert</param>
        /// <returns>inverted byte</returns>
        public static byte Invert_8(byte value)
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
        #region static ushort Invert_16(ushort value)
        /// <summary>
        /// inverts the bits of a given short
        /// </summary>
        /// <param name="value">short to invert</param>
        /// <returns>inverted short</returns>
        public static ushort Invert_16(ushort value)
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
        #region static ushort Check_crc16(byte[] array1, byte[] array2)
        /// <summary>
        /// calculate crc16
        /// </summary>
        /// <param name="array1">first array</param>
        /// <param name="array2">second array</param>
        /// <returns></returns>
        public static ushort Check_crc16(byte[] array1, byte[] array2)
        {
            int array1Length = array1.Length;

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

            int array2Length = array2.Length;
            for (int index = 0; index < array2Length; index++)
            {
                byte cVar1 = DeCryptTools.Invert_8(array2[index]);

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
            ushort result_inverse = DeCryptTools.Invert_16((ushort)result);
            return (ushort)(result_inverse ^ 0xffff);
        }
        #endregion

        #region static void Whitening_init(byte val, byte[] ctx)
        /// <summary>
        /// initialize ctx array
        /// </summary>
        /// <param name="val">value to init</param>
        /// <param name="ctx">byte[7] to be initialized</param>
        public static void Whitening_init(byte val, byte[] ctx)
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
        #region static void Whitening_encode(byte[] data, int dataStartIndex, int len, byte[] ctx)
        /// <summary>
        /// encode byte[]
        /// </summary>
        /// <param name="data">byte[]</param>
        /// <param name="dataStartIndex">startindex of bytes to encode</param>
        /// <param name="len">length of bytearray</param>
        /// <param name="ctx">ctx array</param>
        public static void Whitening_encode(byte[] data, int dataStartIndex, int len, byte[] ctx)
        {
            for (int index = 0; index < len; index++)
            {
                byte currentByte = data[dataStartIndex + index];
                int currentResult = 0;
                for (int bitIndex = 0; bitIndex < 8; bitIndex++)
                {
                    byte uVar2 = whitening_output(ctx);
                    currentResult = (int)((uVar2 ^ (int)currentByte >> ((byte)bitIndex & 0x1f) & 1U) << ((byte)bitIndex & 0x1f)) + currentResult;
                }
                data[dataStartIndex + index] = (byte)currentResult;
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
            byte value_3 = ctx[3];
            byte value_6 = ctx[6];
            ctx[3] = ctx[2];
            ctx[2] = ctx[1];
            ctx[1] = ctx[0];
            ctx[0] = ctx[6];
            ctx[6] = ctx[5];
            ctx[5] = ctx[4];
            ctx[4] = (byte)(value_3 ^ value_6);
            return ctx[0];
        }
        #endregion
    }
}
