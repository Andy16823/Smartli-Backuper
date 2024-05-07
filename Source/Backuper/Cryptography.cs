using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Backuper
{
    public class Cryptography
    {
        private static byte[] GenerateSalt()
        {
            byte[] salt = new byte[16];
            using (RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider())
            {
                rngCsp.GetBytes(salt);
            }
            return salt;
        }

        private static byte[] GenerateKey(string password, byte[] salt, int keySize)
        {
            using (Rfc2898DeriveBytes keyDerivationFunction = new Rfc2898DeriveBytes(password, salt))
            {
                return keyDerivationFunction.GetBytes(keySize);
            }
        }

        private static byte[] GenerateIV(string password, byte[] salt, int ivSize)
        {
            using (Rfc2898DeriveBytes ivDerivationFunction = new Rfc2898DeriveBytes(password, salt))
            {
                return ivDerivationFunction.GetBytes(ivSize);
            }
        }

        public static void EncryptFile(string inputFile, string outputFile, string password)
        {
            byte[] salt = GenerateSalt();

            using (AesManaged aes = new AesManaged())
            {
                byte[] key = GenerateKey(password, salt, aes.KeySize / 8);
                byte[] iv = GenerateIV(password, salt, aes.BlockSize / 8);

                using (FileStream inputFileStream = new FileStream(inputFile, FileMode.Open))
                {
                    using (FileStream outputFileStream = new FileStream(outputFile, FileMode.Create))
                    {
                        using (ICryptoTransform encryptor = aes.CreateEncryptor(key, iv))
                        {
                            outputFileStream.Write(salt, 0, salt.Length);

                            using (CryptoStream cryptoStream = new CryptoStream(outputFileStream, encryptor, CryptoStreamMode.Write))
                            {
                                inputFileStream.CopyTo(cryptoStream);
                            }
                        }
                    }
                }
            }
        }

        public static bool DecryptFile(string inputFile, string outputFile, string password)
        {
            using (FileStream inputFileStream = new FileStream(inputFile, FileMode.Open))
            {
                byte[] salt = new byte[16];
                inputFileStream.Read(salt, 0, salt.Length);

                using (AesManaged aes = new AesManaged())
                {
                    byte[] key = GenerateKey(password, salt, aes.KeySize / 8);
                    byte[] iv = GenerateIV(password, salt, aes.BlockSize / 8);

                    using (ICryptoTransform decryptor = aes.CreateDecryptor(key, iv))
                    {
                        using (FileStream outputFileStream = new FileStream(outputFile, FileMode.Create))
                        {
                            using (CryptoStream cryptoStream = new CryptoStream(inputFileStream, decryptor, CryptoStreamMode.Read))
                            {
                                try
                                {
                                    cryptoStream.CopyTo(outputFileStream);
                                    return true;
                                }
                                catch (Exception ex)
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
