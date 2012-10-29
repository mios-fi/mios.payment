using System;
using System.Globalization;
using System.Collections.Specialized;
using NLog;
using System.Web;

namespace Mios.Payment.Providers {
	public class NordeaPaymentProvider : IPaymentProvider {
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		public string Account { get; set; }
		public string Secret { get; set; }
		public string Url { get; set; }
		public NordeaPaymentProvider() {
			Url = "https://solo3.nordea.fi/cgi-bin/SOLOPM01";
		}
		public NordeaPaymentProvider(string parameterString)
			: this() {
			var parameters = HttpUtility.ParseQueryString(parameterString);
			Account = parameters["account"];
			Secret = parameters["secret"];
			Url = parameters["url"] ?? Url;
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
			fields["SOLOPMT_VERSION"] = "0002";
			fields["SOLOPMT_STAMP"] = referenceNumber;
			fields["SOLOPMT_RCV_ID"] = Account;
			fields["SOLOPMT_LANGUAGE"] = "1";
			fields["SOLOPMT_AMOUNT"] = amount.ToString("N2", CultureInfo.CreateSpecificCulture("fi-fi"));
			fields["SOLOPMT_REF"] = referenceNumber;
			fields["SOLOPMT_DATE"] = "EXPRESS";
			fields["SOLOPMT_MSG"] = message;
			fields["SOLOPMT_RETURN"] = returnUrl;
			fields["SOLOPMT_CANCEL"] = errorUrl;
			fields["SOLOPMT_REJECT"] = errorUrl;
			fields["SOLOPMT_CONFIRM"] = "YES";
			fields["SOLOPMT_CUR"] = "EUR";
			fields["SOLOPMT_MAC"] =
				String.Format("{0}&{1}&{2}&{3}&{4}&{5}&{6}&{7}&",
					fields["SOLOPMT_VERSION"],
					fields["SOLOPMT_STAMP"],
					fields["SOLOPMT_RCV_ID"],
					fields["SOLOPMT_AMOUNT"],
					fields["SOLOPMT_REF"],
					fields["SOLOPMT_DATE"],
					fields["SOLOPMT_CUR"],
					Secret).Hash("MD5").ToUpperInvariant();
			return new PaymentDetails {
				Url = Url,
				Fields = fields
			};
		}

		public bool VerifyResponse(string identifier, decimal amount, NameValueCollection fields) {
			var referenceNumber = ReferenceCalculator.GenerateReferenceNumber(identifier);
			if(!referenceNumber.Equals(fields["SOLOPMT-RETURN-REF"])) {
				log.Error("Identifier comparison failed when verifying response, expected {0} found {1}",
					identifier, fields["SOLOPMT-RETURN-REF"]);
				return false;
			}
			var expected =
				String.Format("{0}&{1}&{2}&{3}&{4}&",
					fields["SOLOPMT-RETURN-VERSION"],
					fields["SOLOPMT-RETURN-STAMP"],
					fields["SOLOPMT-RETURN-REF"],
					fields["SOLOPMT-RETURN-PAID"],
					Secret).Hash("MD5").ToUpperInvariant();
			if(expected.Equals(fields["SOLOPMT-RETURN-MAC"])) {
				return true;
			}
			log.Error(
				"Hash check failed when verifying response, expected {0} found {1}, value computed from {2}&{3}&{4}&{5}&{6}&",
				expected,
				fields["SOLOPMT-RETURN-MAC"],
				fields["SOLOPMT-RETURN-VERSION"],
				fields["SOLOPMT-RETURN-STAMP"],
				fields["SOLOPMT-RETURN-REF"],
				fields["SOLOPMT-RETURN-PAID"],
				"SECRET"
			);
			return false;
		}
	}
}
