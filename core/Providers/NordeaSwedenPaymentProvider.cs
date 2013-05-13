using System;
using System.Globalization;
using System.Collections.Specialized;
using NLog;
using System.Web;

namespace Mios.Payment.Providers {
	public class NordeaSwedenPaymentProvider : IPaymentProvider {
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		public string Account { get; set; }
		public string Currency { get; set; }
		public string Secret { get; set; }
		public string KVV { get; set; }
		public string Url { get; set; }
		public NordeaSwedenPaymentProvider() {
			Url = "https://solo3.nordea.fi/cgi-bin/SOLOPM01";
		}
		public NordeaSwedenPaymentProvider(string parameterString)
			: this() {
			var parameters = HttpUtility.ParseQueryString(parameterString);
			if(parameters["account"] == null)
				throw new ArgumentException("Missing required 'account' parameter in initialization string.");
			if(parameters["secret"] == null)
				throw new ArgumentException("Missing required 'secret' parameter in initialization string.");
			if(parameters["kvv"] == null)
				throw new ArgumentException("Missing required 'kvv' parameter in initialization string.");
			Account = parameters["account"];
			Secret = parameters["secret"];
			
			KVV = parameters["kvv"];
			Url = parameters["url"] ?? Url;
			Currency = parameters["currency"] ?? "SEK";
		}

		public PaymentDetails GenerateDetails(string identifier, decimal amount, string returnUrl, string errorUrl, string message) {
			if(String.IsNullOrEmpty(Account)) {
				throw new InvalidOperationException("A merchant identifier must be assigned before generating details");
			}
			if(String.IsNullOrEmpty(Secret)) {
				throw new InvalidOperationException("A merchant key must be assigned before generating details");
			}
			var referenceNumber = ReferenceCalculator.GenerateReferenceNumber(identifier);
			var fields = new NameValueCollection(StringComparer.Ordinal);
			fields["NB_VERSION"] = "0002";
			fields["NB_RCV_ID"] = Account;
			fields["NB_STAMP"] = referenceNumber;
			fields["NB_DB_AMOUNT"] = amount.ToString("N2", CultureInfo.CreateSpecificCulture("sv-se"));
			fields["NB_DB_CUR"] = Currency;
			fields["NB_DB_REF"] = referenceNumber;
			fields["NB_RETURN"] = returnUrl;
			fields["NB_CANCEL"] = errorUrl;
			fields["NB_REJECT"] = errorUrl;
			fields["NB_HMAC"] = String.Format("{0}&{1}&{2}&{3}&{4}",
					fields["NB_RCV_ID"],
					fields["NB_STAMP"],
					fields["NB_DB_AMOUNT"],
					fields["NB_DB_CUR"],
					fields["NB_DB_REF"]
				)
				.HMAC("HMACSHA256", Secret)
				.Substring(0,32)
				.ToUpperInvariant();
			fields["NB_KVV"] = KVV;
			return new PaymentDetails {
				Url = Url,
				Fields = fields
			};
		}

		public bool VerifyResponse(string identifier, decimal amount, NameValueCollection fields) {
			var referenceNumber = ReferenceCalculator.GenerateReferenceNumber(identifier);
			if(!referenceNumber.Equals(fields["NB_RETURN_DB_REF"])) {
				log.Error("Identifier comparison failed when verifying response, expected {0} found {1}",
					identifier, fields["NB_RETURN_DB_REF"]);
				return false;
			}
			var expected = String.Format("{0}&{1}&{2}&{3}&{4}", new[] {
						fields["NB_RETURN_STAMP"],
						fields["NB_RETURN_DB_AMOUNT"],
						fields["NB_RETURN_DB_CUR"],
						fields["NB_RETURN_DB_REF"],
						fields["NB_PAID"]
					})
					.HMAC("HMACSHA256", Secret)
					.Substring(0,32)
					.ToUpperInvariant();
			if(expected.Equals(fields["NB_HMAC"])) {
				return true;
			}
			log.Error(
				"HMAC check failed when verifying response, expected {0} found {1}, value computed from {2}, {3}, {4}, {5}, {6}",
				expected,
				fields["NB_HMAC"],
				fields["NB_RETURN_STAMP"],
				fields["NB_RETURN_DB_AMOUNT"],
				fields["NB_RETURN_DB_CUR"],
				fields["NB_RETURN_DB_REF"],
				fields["NB_PAID"]
			);
			return false;
		}
	}
}
