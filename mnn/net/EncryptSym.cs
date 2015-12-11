using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.IO;

namespace mnn.net {
    public class EncryptSym {
        private static string key128 = @"$MU#ERu{*90Q3,CR";
        private static string key256 = @"$MU#ERu{*90Q3,CR.'P:q@#l)1XE,w$T";
        private static byte[] iv = { 0x00, 0x0C, 0x10, 0x00, 0xAB, 0x88, 0x06, 0x25, 0xBC, 0x92, 0x10, 0x01, 0xCD, 0x88, 0x11, 0x05 };

        /// <summary>
        /// DES加密
        /// </summary>
        /// <param name="plainStr">明文字符串</param>
        /// <returns>密文</returns>
        public static string DESEncrypt(string plainStr)
        {
            try {
                byte[] key = Encoding.UTF8.GetBytes(key128);
                byte[] plainArray = Encoding.UTF8.GetBytes(plainStr);
                string retval = null;

                using (DESCryptoServiceProvider des = new DESCryptoServiceProvider()) {
                    using (MemoryStream mStream = new MemoryStream()) {
                        using (CryptoStream cStream = new CryptoStream(mStream, des.CreateEncryptor(key, iv), CryptoStreamMode.Write)) {
                            cStream.Write(plainArray, 0, plainArray.Length);
                            cStream.FlushFinalBlock();
                            retval = Convert.ToBase64String(mStream.ToArray());
                        }
                    }
                }
                return retval;
            } catch {
                return null;
            }
        }

        /// <summary>
        /// DES解密
        /// </summary>
        /// <param name="encryptStr">密文字符串</param>
        /// <returns>明文</returns>
        public static string DESDecrypt(string encryptStr)
        {
            try {
                byte[] key = Encoding.UTF8.GetBytes(key128);
                byte[] encryptArray = Convert.FromBase64String(encryptStr);
                string retval = null;

                using (DESCryptoServiceProvider des = new DESCryptoServiceProvider()) {
                    using (MemoryStream mStream = new MemoryStream()) {
                        using (CryptoStream cStream = new CryptoStream(mStream, des.CreateDecryptor(key, iv), CryptoStreamMode.Write)) {
                            cStream.Write(encryptArray, 0, encryptArray.Length);
                            cStream.FlushFinalBlock();
                            retval = Encoding.UTF8.GetString(mStream.ToArray());
                        }
                    }
                }
                return retval;
            } catch {
                return null;
            }
        }

        /// <summary>
        /// AES加密
        /// </summary>
        /// <param name="plainStr">明文字符串</param>
        /// <returns>密文</returns>
        public static string AESEncrypt(string plainStr)
        {
            try {
                byte[] key = Encoding.UTF8.GetBytes(key128);
                byte[] plainArray = Encoding.UTF8.GetBytes(plainStr);
                string retval = null;

                using (Rijndael aes = Rijndael.Create()) {
                    using (MemoryStream mStream = new MemoryStream()) {
                        using (CryptoStream cStream = new CryptoStream(mStream, aes.CreateEncryptor(key, iv), CryptoStreamMode.Write)) {
                            cStream.Write(plainArray, 0, plainArray.Length);
                            cStream.FlushFinalBlock();
                            retval = Convert.ToBase64String(mStream.ToArray());
                        }
                    }
                }
                return retval;
            } catch {
                return null;
            }
        }

        /// <summary>
        /// AES加密
        /// </summary>
        /// <param name="plainStr">明文字符串</param>
        /// <returns>密文</returns>
        public static byte[] AESEncrypt(byte[] plainArray)
        {
            try {
                byte[] key = Encoding.UTF8.GetBytes(key128);
                byte[] retval = null;

                using (Rijndael aes = Rijndael.Create()) {
                    using (MemoryStream mStream = new MemoryStream()) {
                        using (CryptoStream cStream = new CryptoStream(mStream, aes.CreateEncryptor(key, iv), CryptoStreamMode.Write)) {
                            cStream.Write(plainArray, 0, plainArray.Length);
                            cStream.FlushFinalBlock();
                            retval = mStream.ToArray();
                        }
                    }
                }
                return retval;
            } catch {
                return null;
            }
        }

        /// <summary>
        /// AES解密
        /// </summary>
        /// <param name="encryptStr">密文字符串</param>
        /// <returns>明文</returns>
        public static string AESDecrypt(string encryptStr)
        {
            try {
                byte[] key = Encoding.UTF8.GetBytes(key128);
                byte[] encryptArray = Convert.FromBase64String(encryptStr);
                string retval = null;

                using (Rijndael aes = Rijndael.Create()) {
                    using (MemoryStream mStream = new MemoryStream()) {
                        using (CryptoStream cStream = new CryptoStream(mStream, aes.CreateDecryptor(key, iv), CryptoStreamMode.Write)) {
                            cStream.Write(encryptArray, 0, encryptArray.Length);
                            cStream.FlushFinalBlock();
                            retval = Encoding.UTF8.GetString(mStream.ToArray());
                        }
                    }
                }
                return retval;
            } catch {
                return null;
            }
        }

        /// <summary>
        /// AES解密
        /// </summary>
        /// <param name="encryptStr">密文字符串</param>
        /// <returns>明文</returns>
        public static byte[] AESDecrypt(byte[] encryptArray)
        {
            try {
                byte[] key = Encoding.UTF8.GetBytes(key128);
                byte[] retval = null;

                using (Rijndael aes = Rijndael.Create()) {
                    using (MemoryStream mStream = new MemoryStream()) {
                        using (CryptoStream cStream = new CryptoStream(mStream, aes.CreateDecryptor(key, iv), CryptoStreamMode.Write)) {
                            cStream.Write(encryptArray, 0, encryptArray.Length);
                            cStream.FlushFinalBlock();
                            retval = mStream.ToArray();
                        }
                    }
                }
                return retval;
            } catch {
                return null;
            }
        }
    }
}
