using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.IO;

namespace mnn.util {
    public static class EncryptSym {
        private static readonly string KEY128 = @"$MU#ERu{*90Q3,CR";
        //private static readonly string KEY256 = @"$MU#ERu{*90Q3,CR.'P:q@#l)1XE,w$T";
        private static readonly byte[] IV = { 0x00, 0x0C, 0x10, 0x00, 0xAB, 0x88, 0x06, 0x25,
            0xBC, 0x92, 0x10, 0x01, 0xCD, 0x88, 0x11, 0x05 };

        /// <summary>
        /// DES加密
        /// </summary>
        /// <param name="plainStr">明文字符串</param>
        /// <returns>密文</returns>
        public static string DESEncrypt(string plainStr)
        {
            byte[] key = Encoding.UTF8.GetBytes(KEY128);
            byte[] plainArray = Encoding.UTF8.GetBytes(plainStr);

            try {
                using (DESCryptoServiceProvider des = new DESCryptoServiceProvider()) {
                    using (MemoryStream mStream = new MemoryStream()) {
                        using (CryptoStream cStream = new CryptoStream(mStream, des.CreateEncryptor(key, IV), CryptoStreamMode.Write)) {
                            cStream.Write(plainArray, 0, plainArray.Length);
                            cStream.FlushFinalBlock();
                            return Convert.ToBase64String(mStream.ToArray());
                        }
                    }
                }
            } catch {
                return plainStr;
            }
        }

        /// <summary>
        /// DES解密
        /// </summary>
        /// <param name="encryptStr">密文字符串</param>
        /// <returns>明文</returns>
        public static string DESDecrypt(string encryptStr)
        {
            byte[] key = Encoding.UTF8.GetBytes(KEY128);
            byte[] encryptArray = Convert.FromBase64String(encryptStr);

            try {
                using (DESCryptoServiceProvider des = new DESCryptoServiceProvider()) {
                    using (MemoryStream mStream = new MemoryStream()) {
                        using (CryptoStream cStream = new CryptoStream(mStream, des.CreateDecryptor(key, IV), CryptoStreamMode.Write)) {
                            cStream.Write(encryptArray, 0, encryptArray.Length);
                            cStream.FlushFinalBlock();
                            return Encoding.UTF8.GetString(mStream.ToArray());
                        }
                    }
                }
            } catch {
                return encryptStr;
            }
        }

        /// <summary>
        /// AES加密
        /// </summary>
        /// <param name="plainStr">明文字符串</param>
        /// <returns>密文</returns>
        public static string AESEncrypt(string plainStr)
        {
            byte[] key = Encoding.UTF8.GetBytes(KEY128);
            byte[] plainArray = Encoding.UTF8.GetBytes(plainStr);

            try {
                using (Rijndael aes = Rijndael.Create()) {
                    using (MemoryStream mStream = new MemoryStream()) {
                        using (CryptoStream cStream = new CryptoStream(mStream, aes.CreateEncryptor(key, IV), CryptoStreamMode.Write)) {
                            cStream.Write(plainArray, 0, plainArray.Length);
                            cStream.FlushFinalBlock();
                            return Convert.ToBase64String(mStream.ToArray());
                        }
                    }
                }
            } catch {
                return plainStr;
            }
        }

        /// <summary>
        /// AES加密
        /// </summary>
        /// <param name="plainArray">明文字符串</param>
        /// <returns>密文</returns>
        public static byte[] AESEncrypt(byte[] plainArray)
        {
            byte[] key = Encoding.UTF8.GetBytes(KEY128);

            try {
                using (Rijndael aes = Rijndael.Create()) {
                    using (MemoryStream mStream = new MemoryStream()) {
                        using (CryptoStream cStream = new CryptoStream(mStream, aes.CreateEncryptor(key, IV), CryptoStreamMode.Write)) {
                            cStream.Write(plainArray, 0, plainArray.Length);
                            cStream.FlushFinalBlock();
                            return mStream.ToArray();
                        }
                    }
                }
            } catch {
                return plainArray;
            }
        }

        /// <summary>
        /// AES解密
        /// </summary>
        /// <param name="encryptStr">密文字符串</param>
        /// <returns>明文</returns>
        public static string AESDecrypt(string encryptStr)
        {
            byte[] key = Encoding.UTF8.GetBytes(KEY128);
            byte[] encryptArray = Convert.FromBase64String(encryptStr);

            try {
                using (Rijndael aes = Rijndael.Create()) {
                    using (MemoryStream mStream = new MemoryStream()) {
                        using (CryptoStream cStream = new CryptoStream(mStream, aes.CreateDecryptor(key, IV), CryptoStreamMode.Write)) {
                            cStream.Write(encryptArray, 0, encryptArray.Length);
                            cStream.FlushFinalBlock();
                            return Encoding.UTF8.GetString(mStream.ToArray());
                        }
                    }
                }
            } catch {
                return encryptStr;
            }
        }

        /// <summary>
        /// AES解密
        /// </summary>
        /// <param name="encryptArray">密文字符串</param>
        /// <returns>明文</returns>
        public static byte[] AESDecrypt(byte[] encryptArray)
        {
            byte[] key = Encoding.UTF8.GetBytes(KEY128);

            try {
                using (Rijndael aes = Rijndael.Create()) {
                    using (MemoryStream mStream = new MemoryStream()) {
                        using (CryptoStream cStream = new CryptoStream(mStream, aes.CreateDecryptor(key, IV), CryptoStreamMode.Write)) {
                            cStream.Write(encryptArray, 0, encryptArray.Length);
                            cStream.FlushFinalBlock();
                            return mStream.ToArray();
                        }
                    }
                }
            } catch {
                return encryptArray;
            }
        }
    }
}
