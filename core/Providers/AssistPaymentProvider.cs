using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;

namespace Mios.Payment.Providers {
	public class AssistPaymentProvider : IPaymentProvider {
		public bool TestMode { get; set; }
		public string Account { get; set; }
		public string Currency { get; set; }
		public string Url { get; set; }
		public ICollection<string> Providers { get; set; }

		private static string[] KnownProviders = new[] { 
			"Card", 
			"YM", 
			"WM", 
			"QIWI", 
			"QIWIMts", 
			"QIWIMegafon", 
			"QIWIBeeline"
		};

		public AssistPaymentProvider() {
			Url = "https://payments148.paysecure.ru/pay/order.cfm";
			Currency = "RUR";
			Providers = new List<string>();
		}

		public AssistPaymentProvider(string parameterString)
			: this() {
			var parameters = HttpUtility.ParseQueryString(parameterString);
			if(String.IsNullOrEmpty(parameters["account"]))
				throw new ArgumentException("Missing required 'account' parameter in initialization string.");
			Account = parameters["account"];
			Url = parameters["url"] ?? Url;
			TestMode = parameters["testMode"] == "true";
			Currency = parameters["currency"] ?? Currency;
			if(parameters["providers"] != null) {
				Providers = parameters["providers"].Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
			}
		}

		public PaymentDetails GenerateDetails(string identifier, decimal amount, string returnUrl, string errorUrl, string message) {
			var fields = new NameValueCollection {
				{"Merchant_ID", Account },
				{"OrderNumber", identifier },
				{"OrderAmount", amount.ToString("F2",CultureInfo.InvariantCulture) },
				{"OrderCurrency", Currency },
				{"OrderComment", message },
				{"URL_RETURN_OK", returnUrl },
				{"URL_RETURN_NO", errorUrl },
        {"TestMode", TestMode ? "1" : "0" }
			};
			if(Providers.Count > 0) {
				foreach(var type in KnownProviders) {
					fields.Add(type + "Payment", Providers.Contains(type) ? "1" : "0");
				}
			}
			return new PaymentDetails {
				Url = Url,
				Fields = fields
			};
		}
		public bool VerifyResponse(string identifier, decimal amount, NameValueCollection fields) {
			// Assist does not provide direct response on payment
			// status so we always have to respond with false.
			return false;
		}
	}
}
