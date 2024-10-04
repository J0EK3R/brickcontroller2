using System.Text;

namespace BrickController2.DeviceManagement
{
    public static class ArrayTools
  {
    public static int UnmaskArray(byte[] bArr, int i, int i2)
    {
      return UnmaskArray(bArr, i, i2, true);
    }

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

    public static byte[] CreateMaskArray(int i, int length)
    {
      byte[] bArr = new byte[length];
      for (int index = 0; index < length; index++)
      {
        bArr[index] = (byte)((i >> (((length - 1) - index) * 8)) & 255);
      }
      return bArr;
    }

  }
}
