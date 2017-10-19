using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Mios.Payment.Providers {
	public class PayPalPaymentProvider : IPaymentProvider {
		public string Account { get; set; }
		public string Secret { get; set; }
		public string Signature { get; set; }
		public string Currency { get; set; }
		public bool Sandbox { get; set; }

		public PayPalPaymentProvider() {
			Currency = "EUR";
		}

		public PaymentDetails GenerateDetails(string identifier, decimal amount, string returnUrl, string errorUrl, string message) {
			var response = Task.Run(() => MakeApiCall(new Dictionary<string,string> {
				{"METHOD", "SetExpressCheckout"},
			    {"PAYMENTREQUEST_0_DESC", message},
				{"PAYMENTREQUEST_0_PAYMENTACTION", "SALE"},
				{"PAYMENTREQUEST_0_AMT", amount.ToString("f2",CultureInfo.InvariantCulture)},
				{"PAYMENTREQUEST_0_CURRENCYCODE", Currency},
				{"SOLUTIONTYPE", "Sole"},
				{"RETURNURL", returnUrl},
				{"CANCELURL", errorUrl}
			})).Result;

			if(response["ACK"] != "Success") {
				ThrowErrorFor(response);
			}

			return new PaymentDetails {
				Url = "https://www"+(Sandbox?".sandbox":"")+".paypal.com/cgi/bin/webscr",
				Fields = new NameValueCollection { 
					{"cmd", "_express-checkout"},
					{"token", response["TOKEN"]}
				}
			};
		}

		public void ThrowErrorFor(NameValueCollection response) {
			var messages = response.AllKeys
				.Where(t => t.StartsWith("L_ERRORCODE"))
				.Select(t =>
					"Code: "+response[t]+"\n" +
						"Message: "+response[t.Replace("L_ERRORCODE", "L_SHORTMESSAGE")]+"\n" +
						"Description: "+response[t.Replace("L_ERRORCODE", "L_LONGMESSAGE")]
				);
			throw new InvalidOperationException("PayPal returned errors.\n"+String.Join("\n\n", messages));
		}

		public bool VerifyResponse(string identifier, decimal amount, NameValueCollection fields) {
			var response = Task.Run(() => MakeApiCall(new Dictionary<string,string> {
				{"METHOD", "DoExpressCheckoutPayment"},
				{"TOKEN", fields["TOKEN"]},
				{"PAYERID", fields["PAYERID"]},
				{"PAYMENTREQUEST_0_PAYMENTACTION", "SALE"},
				{"PAYMENTREQUEST_0_AMT", amount.ToString("f2", CultureInfo.InvariantCulture)},
				{"PAYMENTREQUEST_0_CURRENCYCODE", Currency}
			})).Result;
			return response["ACK"] == "Success";
		}


		private async Task<NameValueCollection> MakeApiCall(IDictionary<string, string> fields) {
			fields["USER"] = Account;
			fields["PWD"] = Secret;
			fields["SIGNATURE"] = Signature;
			fields["VERSION"] = "93";
			var client = new HttpClient();
			var endpointUrl = new Uri("https://api-3t"+(Sandbox?".sandbox":"")+".paypal.com/nvp"); 
			var response = await client.PostAsync(endpointUrl, new FormUrlEncodedContent(fields));
			response.EnsureSuccessStatusCode();
			var responseString = await response.Content.ReadAsStringAsync();
			return HttpUtility.ParseQueryString(responseString);
		}
	}
}
