using System;
using System.Collections.Specialized;
using System.Globalization;
using NLog;
using System.Web;

namespace Mios.Payment.Providers {
	public class LuottokuntaPaymentProvider : IPaymentProvider {
		static readonly Logger log = LogManager.GetCurrentClassLogger();

		public string Account { get; set; }
		public string Secret { get; set; }
		public string Url { get; set; }
		public LuottokuntaPaymentProvider() {
			Url = "https://dmp2.luottokunta.fi/dmp/html_payments";
		}
		public LuottokuntaPaymentProvider(string parameterString)
			: this() {
			var parameters = HttpUtility.ParseQueryString(parameterString);
			Account = parameters["account"];
			Secret = parameters["secret"];
			if(parameters["account"] == null) {
				throw new ArgumentException("Missing 'account' parameter in initialization string.");
			}
			if(parameters["secret"] == null) {
				throw new ArgumentException("Missing 'secret' parameter in initialization string.");
			}
			Url = parameters["url"] ?? Url;
		}


		public PaymentDetails GenerateDetails(string identifier, decimal amount, string returnUrl, string errorUrl, string message) {
			var details = new PaymentDetails {
				Url = Url,
				Fields = new NameValueCollection(StringComparer.Ordinal) {
				{"Merchant_Number",Account},
				{"Card_Details_Transmit","0"},
				{"Language","FI"},
				{"Device_Category","1"},
				{"Order_ID",identifier},
				{"Amount",Math.Floor(amount*100).ToString(CultureInfo.InvariantCulture)},
				{"Currency_Code","978"},
				{"Order_Description",message},
				{"Success_Url",returnUrl},
				{"Failure_Url",errorUrl},
				{"Cancel_Url",errorUrl},
				{"Transaction_Type","1"}
			}
			};
			details.Fields["Authentication_Mac"] = 
				String.Format("{0}&{1}&{2}&{3}&{4}&{5}",
					details.Fields["Merchant_Number"],
					details.Fields["Order_ID"],
					details.Fields["Amount"],
					details.Fields["Currency_Code"],
					details.Fields["Transaction_Type"],
					Secret).Hash("SHA256").ToUpperInvariant();
			return details;
		}

		public bool VerifyResponse(string identifier, decimal amount, NameValueCollection fields) {
			var formattedAmount = Math.Floor(amount * 100).ToString(CultureInfo.InvariantCulture);
			var expected =
				String.Format("{0}&{1}&{2}&{3}&{4}&{5}",
					Secret,
					"1",
					"978",
					formattedAmount,
					identifier,
					Account).Hash("SHA256").ToLowerInvariant();
			if(expected.Equals(fields["LKMAC"])) {
				return true;
			}
			log.Error(
				"Hash check failed when verifying response, expected {0} found {1}, value computed from {2}{3}{4}{5}{6}{7}",
				expected,
				fields["LKMAC"],
				"SECRET",
				"1",
				"978",
				formattedAmount,
				identifier,
				Account
			);
			return false;
		}
	}
}
