using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Mios.Payment.Providers {
	public class StripePaymentProvider : IPaymentProvider {
		public Uri EndpointUrl { get; set; }
		public string Secret { get; set; }
		public string GatewayUrl { get; set; }
		public string Currency { get; set; }
		public decimal SmallestCurrencyUnit { get; set; }
		public StripePaymentProvider() {
			Currency = "EUR";
			SmallestCurrencyUnit = 0.01m;
			EndpointUrl = new Uri("https://api.stripe.com/v1/");
		}

		public PaymentDetails GenerateDetails(string identifier, decimal amount, string returnUrl, string errorUrl, string message) {
			var details = new PaymentDetails {
				Url = GatewayUrl,
				Fields = new NameValueCollection {
					{"identifier", identifier},
					{"amount", (amount/SmallestCurrencyUnit).ToString("f0", CultureInfo.InvariantCulture)},
					{"returnUrl", returnUrl},
					{"errorUrl", errorUrl},
					{"message", message}
				}
			};
			details.Fields["mac"] = String
				.Join("", 
					details.Fields["identifier"],
					details.Fields["amount"],
					details.Fields["message"]
				)
				.HMAC("HMACSHA256", Encoding.UTF8.GetBytes(Secret));
			
			return details;
		}

		public bool VerifyResponse(string identifier, decimal amount, NameValueCollection fields) {
			if(String.IsNullOrEmpty(Secret)) {
				throw new InvalidOperationException("A secret key must be set before verifying payments.");
			}
			if(fields["stripeToken"]==null)
				return false;

			var formFields = new Dictionary<string, string> {
				{"amount", (amount/SmallestCurrencyUnit).ToString("f0", CultureInfo.InvariantCulture) },
				{"currency", Currency },
				{"source", fields["stripeToken"]},
			};
			if(fields["message"]!=null) {
				var expectedMAC = String
					.Join("", 
						identifier,
						formFields["amount"],
						fields["message"]
					)
					.HMAC("HMACSHA256", Encoding.UTF8.GetBytes(Secret));
				if(expectedMAC.Equals(fields["mac"])==false) {
					// Message manipulated
					return false;
				}
				formFields["description"] = fields["message"];
			}

			var request = WebRequest.CreateHttp(new Uri(EndpointUrl, "charges"));
			request.ContentType = "application/x-www-form-urlencoded";
			request.Headers["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(Secret+":")); ;
			request.Method = "POST";
			var dataString = String.Join("&", formFields.Select(t => Uri.EscapeDataString(t.Key)+"="+Uri.EscapeDataString(t.Value??String.Empty)));
			using(var writer = new StreamWriter(request.GetRequestStream())) {
				writer.Write(dataString);
			}
			try {
				var response = (HttpWebResponse)request.GetResponse();
				return response.StatusCode==HttpStatusCode.OK;
			} catch(WebException) {
				return false;
			}

		}
	}
}
