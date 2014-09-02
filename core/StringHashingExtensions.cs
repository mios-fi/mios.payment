using System;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Specialized;
using System.Web;
using System.Globalization;

namespace Mios.Payment {
	public static class StringHashingExtensions {
		public static string Hash(this string str, string algorithm) {
			var hash = HashAlgorithm.Create(algorithm)
				.ComputeHash(Encoding.ASCII.GetBytes(str));
			return BitConverter.ToString(hash).Replace("-", String.Empty);
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