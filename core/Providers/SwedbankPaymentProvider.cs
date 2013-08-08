using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Org.BouncyCastle.OpenSsl;
using System.Security.Cryptography;
using System.Web;
using System.IO;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Parameters;
using NLog;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;

namespace Mios.Payment.Providers {
	public class SwedbankPaymentProvider : IPaymentProvider {
		private static readonly Logger Log = LogManager.GetCurrentClassLogger();

		public string Account { get; set; }
		public string MerchantId { get; set; }
		public string ReceiverName { get; set; }
		public string Url { get; set; }
		public string Currency { get; set; }
		public string Language { get; set; }

		private RSAParameters privateKey;
		private X509Certificate bankCertificate;

		public SwedbankPaymentProvider(string parameterString) {
			var parameters = HttpUtility.ParseQueryString(parameterString);
			if(parameters["account"] == null)
				throw new ArgumentException("Missing required 'account' parameter in initialization string.");
			if(parameters["merchantId"] == null)
				throw new ArgumentException("Missing required 'merchantId' parameter in initialization string.");
			if(parameters["receiverName"] == null)
				throw new ArgumentException("Missing required 'receiverName' parameter in initialization string.");

			Account = parameters["account"];
			MerchantId = parameters["merchantId"];
			ReceiverName = parameters["receiverName"];
			Language = parameters["language"] ?? "EST";

			if(parameters["privateKeyPath"]!=null) {
				privateKey = ReadPrivateKey(parameters["privateKeyPath"]);
			} else if(parameters["privateKey"]!=null) {
				privateKey = ReadPrivateKey(new StringReader(parameters["privateKey"]));
			} else {
				throw new ArgumentException("Missing required 'privateKey' parameter in initialization string.");
			}
			
			if(parameters["bankCertificatePath"]!=null) {
				bankCertificate = ReadCertificate(parameters["bankCertificatePath"]);
			} else if(parameters["bankCertificate"]!=null) {
				bankCertificate = ReadCertificate(new StringReader(parameters["bankCertificate"]));
			} else {
				throw new ArgumentException("Missing required 'bankCertificate' parameter in initialization string.");
			}
			Url = parameters["url"] ?? "";
			Currency = parameters["currency"] ?? "EUR";
		}

		public PaymentDetails GenerateDetails(string reference, string stamp, decimal amount, string returnUrl, string errorUrl, string message) {
			var fields = new NameValueCollection(StringComparer.Ordinal) {
				{ "VK_SERVICE", "1001" },
				{ "VK_VERSION", "008" },
				{ "VK_SND_ID", MerchantId },
				{ "VK_STAMP", stamp },
				{ "VK_AMOUNT", amount.ToString(CultureInfo.InvariantCulture)},
				{ "VK_CURR", Currency },
				{ "VK_ACC", Account },
				{ "VK_NAME", ReceiverName },
				{ "VK_REF", reference },
				{ "VK_MSG", message },
				{ "VK_RETURN", returnUrl },
				{ "VK_LANG", Language },
				{ "VK_ENCODING", "UTF-8" },
			};
			fields["VK_MAC"] = Sign(new[] {
				fields["VK_SERVICE"], 
				fields["VK_VERSION"], 
				fields["VK_SND_ID"], 
				fields["VK_STAMP"], 
				fields["VK_AMOUNT"], 
				fields["VK_CURR"], 
				fields["VK_ACC"], 
				fields["VK_NAME"], 
				fields["VK_REF"], 
				fields["VK_MSG"]
			});
			return new PaymentDetails {
				Url = Url,
				Fields = fields
			};
		}

		public PaymentDetails GenerateDetails(string identifier, decimal amount, string returnUrl, string errorUrl, string message) {
			return GenerateDetails(
				ReferenceCalculator.GenerateReferenceNumber(identifier),
				identifier,
				amount,
				returnUrl,
				errorUrl,
				message);
		}

		public bool VerifyResponse(string identifier, decimal amount, NameValueCollection fields) {
			decimal amountPaid;
			if(!decimal.TryParse(fields["VK_AMOUNT"], NumberStyles.Number, CultureInfo.InvariantCulture, out amountPaid))
				return false;
			if(amountPaid!=amount)
				return false;
			if(fields["VK_STAMP"]!=identifier)
				return false;
			return Verify(
				new[] { 
					fields["VK_SERVICE"],
					fields["VK_VERSION"],
					fields["VK_SND_ID"],
					fields["VK_REC_ID"],
					fields["VK_STAMP"],
					fields["VK_T_NO"],
					fields["VK_AMOUNT"],
					fields["VK_CURR"],
					fields["VK_REC_ACC"],
					fields["VK_REC_NAME"],
					fields["VK_SND_ACC"],
					fields["VK_SND_NAME"],
					fields["VK_REF"],
					fields["VK_MSG"],
					fields["VK_T_DATE"]
				}, 
				fields["VK_MAC"]
			);
		}

		private RSAParameters ReadPrivateKey(TextReader reader) {
			while(true) {
				object obj = new PemReader(reader).ReadObject();
				if(obj == null)
					throw new ArgumentOutOfRangeException("No private key found.");
				var pair = obj as AsymmetricCipherKeyPair;
				if(pair!=null)
					return DotNetUtilities.ToRSAParameters((RsaPrivateCrtKeyParameters)pair.Private);
			}
		}
		private RSAParameters ReadPrivateKey(string fileName) {
			using(var reader = new StreamReader(fileName)) {
				return ReadPrivateKey(reader);
			}
		}

		private X509Certificate ReadCertificate(TextReader reader) {
			while(true) {
				object obj = new PemReader(reader).ReadObject();
				if(obj == null)
					throw new ArgumentOutOfRangeException("No certificate found.");
				var certificate = obj as Org.BouncyCastle.X509.X509Certificate;
				if(certificate!=null)
					return DotNetUtilities.ToX509Certificate(certificate);
			}
		}
		private X509Certificate ReadCertificate(string fileName) {
			using(var reader = new StreamReader(fileName)) {
				return ReadCertificate(reader);
			}
		}

		private string Sign(IEnumerable<string> values) {
			return Sign(String.Join("", values.Select(t => t.Length.ToString("000") + t).ToArray()));
		}
		private string Sign(string message) {
			using(var rsa = new RSACryptoServiceProvider()) {
				var messageBytes = Encoding.UTF8.GetBytes(message);
				try {
					rsa.ImportParameters(privateKey);
					var signature = rsa.SignData(messageBytes, CryptoConfig.MapNameToOID("SHA1"));
					return Convert.ToBase64String(signature);
				} finally {
					rsa.PersistKeyInCsp = false;
				}
			}
		}

		private bool Verify(IEnumerable<string> values, string signature) {
			return Verify(
				String.Join("", values.Select(t => t.Length.ToString("000") + t).ToArray()), 
				Convert.FromBase64String(signature)
			);
		}
		private bool Verify(string message, byte[] signature) {
			using(var rsa = (RSACryptoServiceProvider)new X509Certificate2(bankCertificate).PublicKey.Key) {
				var messageBytes = Encoding.UTF8.GetBytes(message);
				try {
					return rsa.VerifyData(messageBytes, CryptoConfig.MapNameToOID("SHA1"), signature);
				} finally {
					rsa.PersistKeyInCsp = false;
				}
			}
		}
	}
}
