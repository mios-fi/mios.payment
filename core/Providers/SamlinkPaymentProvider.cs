using System;

using System.Collections.Specialized;
using System.Globalization;
using NLog;
using System.Web;

namespace Mios.Payment.Providers {
	public class SamlinkPaymentProvider : IPaymentProvider {
		static readonly Logger log = LogManager.GetCurrentClassLogger();

		public int Version { get; set; }
		public string Account { get; set; }
		public string Secret { get; set; }
		public string KeyVersion { get; set; }
		public string Url { get; set; }
		public SamlinkPaymentProvider() {
			Version = 1;
		}
		public SamlinkPaymentProvider(string parameterString)
			: this() {
			var parameters = HttpUtility.ParseQueryString(parameterString);
			if(parameters["account"] == null) {
				throw new ArgumentException("Missing 'account' parameter in initialization string.");
			}
			if(parameters["secret"] == null) {
				throw new ArgumentException("Missing 'secret' parameter in initialization string.");
			}
			if(parameters["url"] == null) {
				throw new ArgumentException("Missing 'url' parameter in initialization string.");
			}
			Account = parameters["account"];
			Secret = parameters["secret"];
			Url = parameters["url"];
		}

		public PaymentDetails GenerateDetails(string identifier, decimal amount, string returnUrl, string errorUrl, string message) {
			var referenceNumber = ReferenceCalculator.GenerateReferenceNumber(identifier);
			var details = new PaymentDetails {
				Url = Url,
				Fields = new NameValueCollection(StringComparer.Ordinal) {
					{"NET_STAMP",identifier},
					{"NET_SELLER_ID",Account},
					{"NET_AMOUNT",amount.ToString("N2",CultureInfo.CreateSpecificCulture("fi-fi"))},
					{"NET_CUR","EUR"},
					{"NET_REF",referenceNumber},
					{"NET_DATE","EXPRESS"},
					{"NET_MSG",message},
					{"NET_RETURN",returnUrl},
					{"NET_CANCEL",errorUrl},
					{"NET_REJECT",errorUrl},
					{"NET_CONFIRM","YES"},
				}
			};
			if(Version==1) {
				SignV1(details.Fields);
			} else if(Version==3) {
				SignV3(details.Fields);
			} else if(Version==10) { 
				SignV10(details.Fields);
			} else {
				throw new InvalidOperationException(String.Format("Specified version {0} is not supported, supported values are 1, 3, and 10", Version));
			}
			return details;
		}

		private void SignV1(NameValueCollection fields) {
			fields["NET_VERSION"] = "001";
			fields["NET_LOGON"]   = "TRUE";
			fields["NET_MAC"] = Hash("MD5",
				fields["NET_VERSION"],
				fields["NET_STAMP"],
				Account,
				fields["NET_AMOUNT"],
				fields["NET_REF"],
				fields["NET_DATE"],
				fields["NET_CUR"],
				Secret
			);
		}

		private void SignV3(NameValueCollection fields) {
			fields["NET_VERSION"] = "003";
			fields["NET_ALG"] = "03";
			fields["NET_MAC"] = Hash("SHA256",
				fields["NET_VERSION"],
				fields["NET_STAMP"],
				Account,
				fields["NET_AMOUNT"],
				fields["NET_REF"],
				fields["NET_DATE"],
				fields["NET_CUR"],
				fields["NET_RETURN"],
				fields["NET_CANCEL"],
				fields["NET_REJECT"],
				fields["NET_ALG"],
				Secret
			);
		}

		private void SignV10(NameValueCollection fields) {
			fields["NET_VERSION"] = "010";
			fields["NET_ALG"] = "03";
			fields["NET_KEYVERS"] = KeyVersion;
			fields["NET_MAC"] = Hash("SHA256",
				fields["NET_VERSION"],
				fields["NET_STAMP"],
				Account,
				fields["NET_AMOUNT"],
				fields["NET_REF"],
				fields["NET_DATE"],
				fields["NET_CUR"],
				fields["NET_RETURN"],
				fields["NET_CANCEL"],
				fields["NET_REJECT"],
				fields["NET_ALG"],
				fields["NET_KEYVERS"],
				Secret
			);
		}

		private string Hash(string algorithm, params string[] parts) {
			var str = String.Join("&", parts)+"&";
			var hash = (String.Join("&", parts)+"&").Hash(algorithm).ToUpperInvariant();
			log.Debug("Produced hash {0} using {1} for {2}", hash, algorithm, str);
			return hash;
		}

		public bool VerifyResponse(string identifier, decimal amount, NameValueCollection fields) {
			if(!identifier.Equals(fields["NET_RETURN_STAMP"])) {
				log.Error("Identifier comparison failed when verifying response from Sampo, expected {0} found {1}",
					identifier, fields["NET_RETURN_STAMP"]);
				return false;
			}
			switch(fields["NET_RETURN_VERSION"]) {
				case "001":
					return VerifyV1(fields);
				case "003": 
					return VerifyV3(fields);
				case "010":
					return VerifyV10(fields);
				default: 
					throw new InvalidOperationException("Unsupported version "+fields["NET_RETURN_VERSION"]+".");
			}
		}

		private bool VerifyV1(NameValueCollection fields) {
			var expected = Hash("MD5",
				fields["NET_RETURN_VERSION"],
				fields["NET_RETURN_STAMP"],
				fields["NET_RETURN_REF"],
				fields["NET_RETURN_PAID"],
				Secret
			);
			if(expected.Equals(fields["NET_RETURN_MAC"])) {
				return true;
			}

			log.Error(
				"Hash check failed when verifying response from Sampo, expected {0} found {1}, value computed from {2}&{3}&{4}&{5}&{6}&",
				expected,
				fields["TARKISTE"],
				fields["NET_RETURN_VERSION"],
				fields["NET_RETURN_STAMP"],
				fields["NET_RETURN_REF"],
				fields["NET_RETURN_PAID"],
				"SECRET"
			);
			return false;
		}

		private bool VerifyV3(NameValueCollection fields) {
			var expected = Hash("SHA256",
				fields["NET_RETURN_VERSION"],
				fields["NET_RETURN_STAMP"],
				fields["NET_RETURN_REF"],
				fields["NET_RETURN_PAID"],
				fields["NET_ALG"],
				Secret
			);
			if(expected.Equals(fields["NET_RETURN_MAC"])) {
				return true;
			}

			log.Error(
				"Hash check failed when verifying response from processor, expected {0} found {1}, value computed from {2}&{3}&{4}&{5}&{6}&{7}&",
				expected,
				fields["TARKISTE"],
				fields["NET_RETURN_VERSION"],
				fields["NET_RETURN_STAMP"],
				fields["NET_RETURN_REF"],
				fields["NET_RETURN_PAID"],
				fields["NET_ALG"],
				"SECRET"
			);
			return false;
		}

		private bool VerifyV10(NameValueCollection fields) {
			var expected = Hash("SHA256",
				fields["NET_RETURN_VERSION"],
				fields["NET_ALG"],
				fields["NET_RETURN_STAMP"],
				fields["NET_RETURN_REF"],
				fields["NET_RETURN_PAID"],
				fields["NET_KEYVERS"],
				Secret
			);
			if(expected.Equals(fields["NET_RETURN_MAC"])) {
				return true;
			}

			log.Error(
				"Hash check failed when verifying response from processor, expected {0} found {1}, value computed from {2}&{3}&{4}&{5}&{6}&{7}&{8}&",
				expected,
				fields["TARKISTE"],
				fields["NET_RETURN_VERSION"],
				fields["NET_ALG"],
				fields["NET_RETURN_STAMP"],
				fields["NET_RETURN_REF"],
				fields["NET_RETURN_PAID"],
				fields["NET_KEYVERS"],
				"SECRET"
			);
			return false;
		}
	}
}
