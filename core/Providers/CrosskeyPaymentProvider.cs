using System;
using System.Collections.Specialized;
using System.Globalization;

using NLog;
using System.Web;

namespace Mios.Payment.Providers {
	public class CrosskeyPaymentProvider : IPaymentProvider {
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		public string Identifier { get; set; }
		public string Account { get; set; }
		public string Secret { get; set; }
		public string Url { get; set; }
		public string ReceiverName { get; set; }
		public string HashAlgorithm { get; set; }

		public CrosskeyPaymentProvider(string parameterString) {
			HashAlgorithm = "SHA256";
			var parameters = HttpUtility.ParseQueryString(parameterString);
			if(String.IsNullOrEmpty(parameters["account"]))
				throw new InvalidOperationException("A merchant account must be assigned before generating details");
			if(String.IsNullOrEmpty(parameters["identifier"]))
				throw new InvalidOperationException("A merchant identifier must be assigned before generating details");
			if(String.IsNullOrEmpty(parameters["secret"]))
				throw new InvalidOperationException("A merchant key must be assigned before generating details");
			if(String.IsNullOrEmpty(parameters["url"]))
				throw new InvalidOperationException("A service endpoint url must be assigned before generating details");
			if(String.IsNullOrEmpty(parameters["receiverName"]))
				throw new InvalidOperationException("A receiver name must be assigned before generating details");
			Identifier = parameters["identifier"];
			Account = parameters["account"];
			Secret = parameters["secret"];
			Url = parameters["url"];
			ReceiverName = parameters["receiverName"];
		}

		public PaymentDetails GenerateDetails(string identifier, decimal amount, string returnUrl, string errorUrl, string message) {
			var referenceNumber = ReferenceCalculator.GenerateReferenceNumber(identifier);
			var fields = new NameValueCollection(StringComparer.Ordinal);
			fields["AAB_VERSION"] = "0002";
			fields["AAB_STAMP"] = identifier;
			fields["AAB_RCV_ID"] = Identifier;
			fields["AAB_RCV_ACCOUNT"] = Account;
			fields["AAB_RCV_NAME"] = ReceiverName;
			fields["AAB_AMOUNT"] = amount.ToString("f2", CultureInfo.GetCultureInfo("fi"));
			fields["AAB_REF"] = referenceNumber;
			fields["AAB_DATE"] = "EXPRESS";
			fields["AAB_RETURN"] = returnUrl;
			fields["AAB_CANCEL"] = errorUrl;
			fields["AAB_REJECT"] = errorUrl;
			fields["AAB_CONFIRM"] = "YES";
			fields["AAB_KEYVERS"] = "0001";
			fields["AAB_CUR"] = "EUR";
			fields["AAB_LANGUAGE"] = CultureInfo.CurrentCulture.Name.StartsWith("sv") ? "2" : "1";
			fields["BV_UseBVCookie"] = "NO";
			if(HashAlgorithm=="SHA256") {
				fields["AAB_ALG"] = "03";
			}
			fields["AAB_MAC"] =
				String.Format("{0}&{1}&{2}&{3}&{4}&{5}&{6}&{7}&",
					fields["AAB_VERSION"],
					fields["AAB_STAMP"],
					fields["AAB_RCV_ID"],
					fields["AAB_AMOUNT"],
					fields["AAB_REF"],
					fields["AAB_DATE"],
					fields["AAB_CUR"],
					Secret).Hash(HashAlgorithm).ToUpperInvariant();
			return new PaymentDetails {
				Url = Url,
				Fields = fields
			};
		}

		public bool VerifyResponse(string identifier, decimal amount, NameValueCollection fields) {
			var referenceNumber = ReferenceCalculator.GenerateReferenceNumber(identifier);
			if(!referenceNumber.Equals(fields["AAB-RETURN-REF"])) {
				log.Error("Identifier comparison failed when verifying response, expected {0} found {1}",
					identifier, fields["AAB-RETURN-REF"]);
				return false;
			}
			var expected =
				String.Format("{0}&{1}&{2}&{3}&{4}&",
					fields["AAB-RETURN-VERSION"],
					fields["AAB-RETURN-STAMP"],
					fields["AAB-RETURN-REF"],
					fields["AAB-RETURN-PAID"],
					Secret).Hash(HashAlgorithm).ToUpperInvariant();
			if(expected.Equals(fields["AAB-RETURN-MAC"])) {
				return true;
			}
			log.Error(
				"Hash check failed when verifying response, expected {0} found {1}, value computed from {2}&{3}&{4}&{5}&{6}&",
				expected,
				fields["AAB-RETURN-MAC"],
				fields["AAB-RETURN-VERSION"],
				fields["AAB-RETURN-STAMP"],
				fields["AAB-RETURN-REF"],
				fields["AAB-RETURN-PAID"],
				"SECRET"
			);
			return false;
		}
	}
}
