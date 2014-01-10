using System;
using System.Collections.Specialized;
using System.Globalization;

using NLog;
using System.Web;

namespace Mios.Payment.Providers {
	public class HandelsbankenPaymentProvider : IPaymentProvider {
		static readonly Logger log = LogManager.GetCurrentClassLogger();

		public string Account { get; set; }
		public string Secret { get; set; }
		public string Url { get; set; }
		public string Currency { get; set; }
		public HandelsbankenPaymentProvider() {
			Url = "https://secure.handelsbanken.se/bb/glss/servlet/ssco_dirapp";
		}

		public HandelsbankenPaymentProvider(string parameterString)
			: this() {
			var parameters = HttpUtility.ParseQueryString(parameterString);
			if(parameters["account"] == null) {
				throw new ArgumentException("Missing required 'account' parameter in initialization string.");
			}
			if(parameters["secret"] == null) {
				throw new ArgumentException("Missing required 'secret' parameter in initialization string.");
			}
			Account = parameters["account"];
			Secret = parameters["secret"];
			Url = parameters["url"] ?? Url;
		}
		public PaymentDetails GenerateDetails(string identifier, decimal amount, string returnUrl, string errorUrl, string message) {
			var referenceNumber = ReferenceCalculator.GenerateReferenceNumber(identifier);
			var formattedAmount = amount.ToString("N2", CultureInfo.CreateSpecificCulture("fi-fi"));
			var details = new PaymentDetails {
				Url = Url,
				Fields = new NameValueCollection(StringComparer.Ordinal) {
					{ "entryid",  "switch"},
					{ "appaction", "doDirectPay"},
					{ "switchaction", "3"},
					{ "handOverDatatype", "1"},
					{ "appname", "ssse"},
					{ "language", "sv"},
					{ "country",  "se"},
					{ "butikid",     Account },
					{ "ordernummer",    identifier },
					{ "orderbelopp",    formattedAmount },
					{ "retururl", returnUrl },
					{ "senastebokningstid", DateTime.Now.AddDays(1).ToString("yyyyMMddHHmmss") }
				}
			};
			details.Fields["Kontrollsumma"] =
				String.Format("{0}{1}{2}{3}{4}{5}{6}{7}",
					details.Fields["Butikid"],
					details.Fields["Ordernummer"],
					details.Fields["Orderbelopp"],
					Secret
				).Hash("MD5").ToLowerInvariant();
			return details;
		}

		public bool VerifyResponse(string identifier, decimal amount, NameValueCollection fields) {
			if(!identifier.Equals(fields["ordernummer"])) {
				log.Error("Reference number comparison failed when verifying response from Danske Bank, expected {0} found {1}",
					identifier, fields["ordernummer"]);
				return false;
			}
			if(!"0".Equals(fields["status"])) {
				log.Error("Expected status {0} for successful payment, received {1}",
					"0", fields["status"]);
				return false;
			}
			var expected =
				String.Format("{0}{1}{2}{3}{4}{5}{6}",
					fields["butikid"],
					fields["ordernummer"],
					fields["orderbelopp"],
					fields["status"],
					fields["timestamp"],
					Secret).Hash("MD5").ToLowerInvariant();
			if(expected.Equals(fields["kontrollsumma"])) {
				return true;
			}
			log.Error(
				"Hash check failed when verifying response from Danske Bank, expected {0} found {1}, value computed from {2}{3}{4}{5}{6}{7}",
				expected,
				fields["kontrollsumma"],
				fields["butikid"],
				fields["ordernummer"],
				fields["orderbelopp"],
				fields["status"],
				fields["timestamp"],
				"SECRET"
			);
			return false;
		}
	}
}
