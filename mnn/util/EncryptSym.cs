using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.IO;

namespace mnn.util {
    public class EncryptSym {
        /// <summary>
        /// 获取密钥
        /// </summary>
        private static string Key
        {
            get { return @"$MU#ERu{"; }
        }

        /// <summary>
        /// 获取向量
        /// </summary>
        private static string IV
        {
            get { return @"e2C*(%q=S5,v$AS3"; }
        }

        /// <summary>
        /// DES加密
        /// </summary>
        /// <param name="plainStr">明文字符串</param>
        /// <returns>密文</returns>
        public static string DESEncrypt(string plainStr)
        {
            try {
                byte[] bKey = Encoding.UTF8.GetBytes(Key);
                byte[] bIV = Encoding.UTF8.GetBytes(IV);
                byte[] byteArray = Encoding.UTF8.GetBytes(plainStr);
                string retval = null;

                DESCryptoServiceProvider des = new DESCryptoServiceProvider();
                using (MemoryStream mStream = new MemoryStream()) {
                    using (CryptoStream cStream = new CryptoStream(mStream, des.CreateEncryptor(bKey, bIV), CryptoStreamMode.Write)) {
                        cStream.Write(byteArray, 0, byteArray.Length);
                        cStream.FlushFinalBlock();
                        retval = Convert.ToBase64String(mStream.ToArray());
                    }
                }
                des.Clear();
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
                byte[] bKey = Encoding.UTF8.GetBytes(Key);
                byte[] bIV = Encoding.UTF8.GetBytes(IV);
                byte[] byteArray = Convert.FromBase64String(encryptStr);
                string retval = null;

                DESCryptoServiceProvider des = new DESCryptoServiceProvider();
                using (MemoryStream mStream = new MemoryStream()) {
                    using (CryptoStream cStream = new CryptoStream(mStream, des.CreateDecryptor(bKey, bIV), CryptoStreamMode.Write)) {
                        cStream.Write(byteArray, 0, byteArray.Length);
                        cStream.FlushFinalBlock();
                        retval = Encoding.UTF8.GetString(mStream.ToArray());
                    }
                }
                des.Clear();
                return retval;
            } catch {
                return null;
            }
        }


        /// <summary>
        /// 获取密钥
        /// </summary>
        private static string AESKey
        {
            get { return @"$MU#ERu{*90Q3,CR.'P:q@#l)1XE,w$T"; }
        }

        /// <summary>
        /// 获取向量
        /// </summary>
        private static string AESIV
        {
            get { return @"e2C*(%q=S5,v$AS3"; }
        }

        /// <summary>
        /// AES加密
        /// </summary>
        /// <param name="plainStr">明文字符串</param>
        /// <returns>密文</returns>
        public static string AESEncrypt(string plainStr)
        {
            try {
                byte[] bKey = Encoding.UTF8.GetBytes(AESKey);
                byte[] bIV = Encoding.UTF8.GetBytes(AESIV);
                byte[] byteArray = Encoding.UTF8.GetBytes(plainStr);
                string retval = null;

                Rijndael aes = Rijndael.Create();
                using (MemoryStream mStream = new MemoryStream()) {
                    using (CryptoStream cStream = new CryptoStream(mStream, aes.CreateEncryptor(bKey, bIV), CryptoStreamMode.Write)) {
                        cStream.Write(byteArray, 0, byteArray.Length);
                        cStream.FlushFinalBlock();
                        retval = Convert.ToBase64String(mStream.ToArray());
                    }
                }
                aes.Clear();
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
                byte[] bKey = Encoding.UTF8.GetBytes(AESKey);
                byte[] bIV = Encoding.UTF8.GetBytes(AESIV);
                byte[] byteArray = Convert.FromBase64String(encryptStr);
                string retval = null;

                Rijndael aes = Rijndael.Create();
                using (MemoryStream mStream = new MemoryStream()) {
                    using (CryptoStream cStream = new CryptoStream(mStream, aes.CreateDecryptor(bKey, bIV), CryptoStreamMode.Write)) {
                        cStream.Write(byteArray, 0, byteArray.Length);
                        cStream.FlushFinalBlock();
                        retval = Encoding.UTF8.GetString(mStream.ToArray());
                    }
                }
                aes.Clear();
                return retval;
            } catch {
                return null;
            }
        }
    }
}
