using System;

using System.Collections.Specialized;
using System.Globalization;
using NLog;
using System.Web;

namespace Mios.Payment.Providers {
  public class SamlinkPaymentProvider : IPaymentProvider {
    static readonly Logger log = LogManager.GetCurrentClassLogger();

    public string Account { get; set; }
		public string Secret { get; set; }
		public string Url { get; set; }
		public SamlinkPaymentProvider() {
		}
		public SamlinkPaymentProvider(string parameterString) : this() {
			var parameters = HttpUtility.ParseQueryString(parameterString);
			if(parameters["account"] == null) {
        throw new ArgumentException("Missing 'account' parameter in initialization string.");
      }
      if(parameters["secret"]==null) {
        throw new ArgumentException("Missing 'secret' parameter in initialization string.");
      }
      if(parameters["url"]==null) {
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
					{"NET_VERSION","001"},
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
					{"NET_LOGON","TRUE"}
				}
			};
			details.Fields["NET_MAC"] =
				String.Format("{0}&{1}&{2}&{3}&{4}&{5}&{6}&{7}&",
					details.Fields["NET_VERSION"],
					details.Fields["NET_STAMP"],
					Account,
					details.Fields["NET_AMOUNT"],
					details.Fields["NET_REF"],
					details.Fields["NET_DATE"],
					details.Fields["NET_CUR"],
					Secret).Hash("MD5").ToUpperInvariant();
			return details;
		}

		public bool VerifyResponse(string identifier, decimal amount, NameValueCollection fields) {
			if(!identifier.Equals(fields["NET_RETURN_STAMP"])) {
        log.Error("Identifier comparison failed when verifying response from Sampo, expected {0} found {1}",
          identifier, fields["NET_RETURN_STAMP"]);
        return false;
			}
			var expected = 
				String.Format("{0}&{1}&{2}&{3}&{4}&",
					fields["NET_RETURN_VERSION"],
					fields["NET_RETURN_STAMP"],
					fields["NET_RETURN_REF"],
					fields["NET_RETURN_PAID"],
					Secret).Hash("MD5").ToUpperInvariant();
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
	}
}
