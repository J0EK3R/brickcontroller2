using System;
using System.Collections.Generic;
using System.Linq;

namespace BrickController2.DeviceManagement
{
  /// <summary>
  /// static class wich implements the encryption algorithm for the advertising data
  /// </summary>
  internal static class MouldKingCrypt
  {
    #region Constants
    private static readonly byte[] Array_C1C2C3C4C5 = new byte[] { 0xC1, 0xC2, 0xC3, 0xC4, 0xC5 };
    private static readonly int[] MagicArray_37 = CreateMagicArray(37, 7);
    private static readonly int[] MagicArray_63 = CreateMagicArray(63, 7);
    #endregion

    #region static TryDecryptBruteForce(byte[] cryptDataArray, out byte[] result, Action<byte[]> output)
    /// <summary>
    /// Tries to decrypt the given MK-BT-Data
    /// (Between CompanyId and CRC)
    /// </summary>
    /// <param name="cryptDataArray">Array of the crypted MK-BT-Data</param>
    /// <param name="resultArray">resulting Array or null</param>
    /// <returns>True, if success. Else False.</returns>
    public static bool TryDecryptBruteForce(byte[] cryptDataArray, out byte[] resultArray)
    {
      List<byte> resultList = new List<byte>();

      // cryptDataArray.Length - 8 - 2
      //                         L header
      //                             L 2 bytes for checksum
      for (int byteOffset = 0; byteOffset < cryptDataArray.Length - 8 - 2; byteOffset++)
      {
        // add new byte in result list
        resultList.Add(0x00);

        // brute force from 0x00 to 0xFF
        for (int currentVariant = 0x00; currentVariant <= 0xFF; currentVariant++)
        {
          resultList[byteOffset] = (byte)currentVariant;

          // calculate intermediate array to compare
          byte[] cryptIntermediateResultArray = MouldKingCrypt.Crypt(resultList.ToArray());

          byte currentSetByte = cryptDataArray[byteOffset + 8];
          byte currentByte = cryptIntermediateResultArray[byteOffset + 8];

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

      resultArray = null;
      return false;
    }
    #endregion
    #region static Crypt(byte[] rawDataArray)
    /// <summary>
    /// encrypts the given rawDataArray with the MK specific encryption
    /// </summary>
    /// <param name="rawDataArray">rawDataArray to be encrypted</param>
    /// <returns>encrypted Data</returns>
    public static byte[] Crypt(byte[] rawDataArray)
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

      Buffer.BlockCopy(rawDataArray, 0, targetArray, Array_C1C2C3C4C5.Length + 18, rawDataArray.Length);

      // crypt Bytes from position 15 to 22
      for (int index = 15; index < Array_C1C2C3C4C5.Length + 18; index++)
      {
        targetArray[index] = RevertBits_Byte(targetArray[index]);
      }

      // calc checksum und copy to array
      int checksum = CalcChecksumFromArrays(Array_C1C2C3C4C5, rawDataArray);
      targetArray[Array_C1C2C3C4C5.Length + 18 + rawDataArray.Length + 0] = (byte)(checksum & 255);
      targetArray[Array_C1C2C3C4C5.Length + 18 + rawDataArray.Length + 1] = (byte)((checksum >> 8) & 255);

      // crypt bytes from offset 18 to the end with magicNumberArray_63
      int[] magicNumberArray_63 = MagicArray_63.ToArray(); // Copy MagicArray_63
      int tempArrayLength = targetArrayLength - 18;
      byte[] tempArray = new byte[tempArrayLength];
      Buffer.BlockCopy(targetArray, 18, tempArray, 0, tempArrayLength);   // copy from offset 18 to end to bArr4

      CryptArray(tempArray, magicNumberArray_63);
      Buffer.BlockCopy(tempArray, 0, targetArray, 18, tempArray.Length);  // copy back to target array

      // crypt complete array with magicNumberArray_37
      int[] magicNumberArray_37 = MagicArray_37.ToArray(); // copy MagicArray_37
      CryptArray(targetArray, magicNumberArray_37);

      // resulting advertisment array has a length of constant 24 bytes
      byte[] telegramArray = new byte[24];

      int lengthResultArray = Array_C1C2C3C4C5.Length + rawDataArray.Length + 5;
      Buffer.BlockCopy(targetArray, 15, telegramArray, 0, lengthResultArray);

      // fill rest of array
      for (int index = lengthResultArray; index < telegramArray.Length; index++)
      {
        telegramArray[index] = (byte)(index + 1);
      }

      return telegramArray;
    }
    #endregion

    #region CreateMagicArray(int magicNumber, int size)
    /// <summary>
    /// Creates an array with the given size filled with values calculated from the given magicnumber
    /// </summary>
    /// <param name="magicNumber">MagicNumber</param>
    /// <param name="size">Size of MagicArray</param>
    private static int[] CreateMagicArray(int magicNumber, int size)
    {
      int[] magicArray = new int[size];
      magicArray[0] = 1;

      for (int index = 1; index < 7; index++)
      {
        magicArray[index] = (magicNumber >> (6 - index)) & 1;
      }

      return magicArray;
    }
    #endregion
    #region RevertBits_Byte(byte value)
    /// <summary>
    /// Reverts the bits of a byte
    /// </summary>
    /// <param name="value"></param>
    /// <returns>reverted byte</returns>
    private static byte RevertBits_Byte(byte value)
    {
      byte result = 0;
      for (byte indexBit = 0; indexBit < 8; indexBit = (byte)(indexBit + 1))
      {
        if (((1 << indexBit) & value) != 0)
        {
          result = (byte)(result | (1 << (7 - indexBit)));
        }
      }
      return result;
    }
    #endregion
    #region RevertBits_Int(int value)
    /// <summary>
    /// Reverts the bits of an integer
    /// </summary>
    /// <param name="value"></param>
    /// <returns>reverted integer</returns>
    private static int RevertBits_Int(int value)
    {
      int result = 0;
      for (int indexBit = 0; indexBit < 16; indexBit++)
      {
        if (((1 << indexBit) & value) != 0)
        {
          result |= 1 << (15 - indexBit);
        }
      }
      return 65535 & result;
    }
    #endregion
    #region CryptArray(byte[] bArr, int[] iArr)
    /// <summary>
    /// 
    /// </summary>
    /// <param name="bArr"></param>
    /// <param name="magicNumberArray"></param>
    /// <returns></returns>
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
          currentResult += (((currentByte >> indexBit) & 1) ^ ShiftMagicArray(magicNumberArray)) << indexBit;
        }
        bArr[indexByte] = (byte)(currentResult & 255);
      }
      return bArr;
    }
    #endregion
    #region CalcChecksumFromArrays(byte[] bArr, byte[] bArr2)
    /// <summary>
    /// Calculates the checksum of the given two arrays
    /// </summary>
    /// <param name="firstArray">first array</param>
    /// <param name="secondArray">second array</param>
    /// <returns>calculated checksum</returns>
    private static int CalcChecksumFromArrays(byte[] firstArray, byte[] secondArray)
    {
      int result = 65535;
      for (int firstArrayIndex = 0; firstArrayIndex < firstArray.Length; firstArrayIndex++)
      {
        result = (result ^ (firstArray[(firstArray.Length - 1) - firstArrayIndex] << 8)) & 65535;
        for (int indexBit = 0; indexBit < 8; indexBit++)
        {
          int currentResult = result & 32768;
          result <<= 1;
          if (currentResult != 0)
          {
            result ^= 4129;
          }
        }
      }

      foreach (byte currentByte in secondArray)
      {
        result = ((RevertBits_Byte(currentByte) << 8) ^ result) & 65535;
        for (int indexBit = 0; indexBit < 8; indexBit++)
        {
          int currentResult = result & 32768;
          result <<= 1;
          if (currentResult != 0)
          {
            result ^= 4129;
          }
        }
      }
      return RevertBits_Int(result) ^ 65535;
    }
    #endregion
    #region ShiftArray(int[] iArr)
    private static int ShiftMagicArray(int[] iArr)
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
