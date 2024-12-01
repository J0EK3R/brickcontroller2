
namespace BluetoothEnDeCrypt
{
    public interface ICrypt
    {
        /// <summary>
        /// crypt data-array with addr and ctxvalue
        /// </summary>
        /// <param name="addr">address array</param>
        /// <param name="data">data array to encrypt</param>
        /// <param name="ctxValue">ctx value for encryption</param>
        /// <returns>crypted array</returns>
        byte[] EnCrypt(byte[] addr, byte[] data, byte ctxValue);
    }
}
