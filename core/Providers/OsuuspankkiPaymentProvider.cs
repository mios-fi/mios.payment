using System;
using System.Collections.Specialized;
using System.Globalization;
using NLog;
using System.Web;

namespace Mios.Payment.Providers {
	public class OsuuspankkiPaymentProvider : IPaymentProvider {
		private static readonly Logger log = LogManager.GetCurrentClassLogger();
		public string Account { get; set; }
		public string Secret { get; set; }
		public string Url { get; set; }
		public OsuuspankkiPaymentProvider() {
			Url = "https://kultaraha.op.fi/cgi-bin/krcgi";
		}
		public OsuuspankkiPaymentProvider(string parameterString)
			: this() {
			var parameters = HttpUtility.ParseQueryString(parameterString);
			if(parameters["account"] == null) {
				throw new ArgumentException("Missing 'account' parameter in initialization string.");
			}
			if(parameters["secret"] == null) {
				throw new ArgumentException("Missing 'secret' parameter in initialization string.");
			}
			Account = parameters["account"];
			Secret = parameters["secret"];
			Url = parameters["url"] ?? Url;
		}
		public PaymentDetails GenerateDetails(string identifier, decimal amount, string returnUrl, string errorUrl, string message) {
			var referenceNumber = ReferenceCalculator.GenerateReferenceNumber(identifier);
			var details = new PaymentDetails {
				Url = Url,
				Fields = new NameValueCollection(StringComparer.Ordinal) {
					{"action_id","701"},
					{"VERSIO","1"},
					{"MAKSUTUNNUS",identifier},
					{"MYYJA",Account},
					{"SUMMA",amount.ToString("N2",CultureInfo.CreateSpecificCulture("fi-fi"))},
					{"VIITE",referenceNumber},
					{"VIESTI",message},
					{"TARKISTE-VERSIO","1"},
					{"PALUULINKKI",returnUrl},
					{"PERUUTUSLINKKI",errorUrl},
					{"VAHVISTUS","Y"},
					{"VALUUTTALAJI","EUR"}
				}
			};
			details.Fields["TARKISTE"] =
				String.Format("{0}{1}{2}{3}{4}{5}{6}{7}",
					details.Fields["VERSIO"],
					details.Fields["MAKSUTUNNUS"],
					details.Fields["MYYJA"],
					details.Fields["SUMMA"],
					details.Fields["VIITE"],
					details.Fields["VALUUTTALAJI"],
					details.Fields["TARKISTE-VERSIO"],
					Secret).Hash("MD5").ToUpperInvariant();
			return details;
		}

		public bool VerifyResponse(string identifier, decimal amount, NameValueCollection fields) {
			if(!identifier.Equals(fields["MAKSUTUNNUS"])) {
				log.Error("Identifier comparison failed when verifying response, expected {0} found {1}",
					identifier, fields["MAKSUTUNNUS"]);
				return false;
			}
			var expected =
				String.Format("{0}{1}{2}{3}{4}{5}",
					fields["VERSIO"],
					fields["MAKSUTUNNUS"],
					fields["VIITE"],
					fields["ARKISTOINTITUNNUS"],
					fields["TARKISTE-VERSIO"],
					Secret).Hash("MD5").ToUpperInvariant();
			if(expected.Equals(fields["TARKISTE"])) {
				return true;
			}
			log.Error(
				"Hash check failed when verifying response, expected {0} found {1}, value computed from {2}{3}{4}{5}{6}{7}",
				expected,
				fields["TARKISTE"],
				fields["VERSIO"],
				fields["MAKSUTUNNUS"],
				fields["VIITE"],
				fields["ARKISTOINTITUNNUS"],
				fields["TARKISTE-VERSIO"],
				"SECRET"
			);
			return false;
		}
	}
}
