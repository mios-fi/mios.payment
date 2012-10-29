using System;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Specialized;
using System.Web;

namespace Mios.Payment {
	internal static class StringHashingExtensions {
		public static string Hash(this string str, string algorithm) {
			var hash = HashAlgorithm.Create(algorithm)
				.ComputeHash(Encoding.ASCII.GetBytes(str));
			return BitConverter.ToString(hash).Replace("-", String.Empty);
		}
	}
}


