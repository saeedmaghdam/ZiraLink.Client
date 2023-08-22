using System.Security.Cryptography;
using System.Text;

namespace ZiraLink.Client.Helpers
{
    public static class EncryptionHelper
    {
        public static string EncryptWithPublicKey(string publicKey, string plainText)
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(publicKey);
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = rsa.Encrypt(plainBytes, false);
                return Convert.ToBase64String(encryptedBytes);
            }
        }

        public static string DecryptWithPrivateKey(string privateKey, string encryptedText)
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(privateKey);
                byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
                byte[] decryptedBytes = rsa.Decrypt(encryptedBytes, false);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
        }
    }
}
