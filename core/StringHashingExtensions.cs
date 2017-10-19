using System;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;

namespace Mios.Payment {
	public static class StringHashingExtensions {
		public static string Hash(this string str, string algorithm) {
			HashAlgorithm hash;
			if("md5".Equals(algorithm, StringComparison.OrdinalIgnoreCase)) {
				hash = MD5.Create();
			} else if("sha256".Equals(algorithm, StringComparison.OrdinalIgnoreCase)) {
				hash = SHA256.Create();
			} else if("sha512".Equals(algorithm, StringComparison.OrdinalIgnoreCase)) {
				hash = SHA512.Create();
			} else {
				throw new ArgumentOutOfRangeException("Unsupported hash algorithm '" + algorithm + "'");
			}
			var digest = hash.ComputeHash(Encoding.ASCII.GetBytes(str));
			return BitConverter.ToString(digest).Replace("-", String.Empty);
		}
		public static string HMAC(this string str, string algorithm, byte[] secret) { 
			var hmac = System.Security.Cryptography.HMAC.Create(algorithm);
			hmac.Key = secret;
			var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(str));
			return BitConverter.ToString(hash).Replace("-", String.Empty);
		}
		public static string HMAC(this string str, string algorithm, string secret) {
			return HMAC(str, algorithm, HexToBytes(secret));
		}
		private static byte[] HexToBytes(string str) {
			var buffer = new byte[str.Length / 2];
			for(var i = 0; i < buffer.Length; i++) {
				buffer[i] = Byte.Parse(str.Substring(i * 2, 2), NumberStyles.HexNumber);
			}
			return buffer;
		}
	}
}