using System;
using Mios.Payment;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Globalization;

namespace Mios.Payment.Verifiers {
	public class NordeaVerificationProvider : IVerificationProvider {
		public string Account { get; set; }
		public string Secret { get; set; }
		public Uri EndpointUrl { get; set; }

		public NordeaVerificationProvider() {
			EndpointUrl = new Uri("https://solo3.nordea.fi/cgi-bin/SOLOPM10");
		}
		public NordeaVerificationProvider(string data) : this() {
			var parameters = HttpUtility.ParseQueryString(data);
			if( parameters["account"]==null ) {
				throw new ArgumentException("Missing 'account' parameter in initialization string.");
			}
			if(parameters["secret"]==null) {
				throw new ArgumentException("Missing 'secret' parameter in initialization string.");
			}
			Account = parameters["account"];
			Secret = parameters["secret"];
		}

		public async Task<bool> VerifyPaymentAsync(string identifier, decimal amount) {
			var data = new Dictionary<string, string> {
				{"SOLOPMT_VERSION", "0001"},
				{"SOLOPMT_TIMESTMP", DateTime.Now.ToString("yyyyMMddhhmmss0001", CultureInfo.InvariantCulture)},
				{"SOLOPMT_RCV_ID", Account},
				{"SOLOPMT_LANGUAGE", "3"},
				{"SOLOPMT_RESPTYPE", "xml"},
				{"SOLOPMT_REF", ReferenceCalculator.GenerateReferenceNumber(identifier) },
				{"SOLOPMT_KEYVERS", "0001"},
				{"SOLOPMT_ALG", "01"},
			};
			data["SOLOPMT_MAC"] = 
				String.Format("{0}&{1}&{2}&{3}&{4}&{5}&{6}&{7}&{8}&",
					data["SOLOPMT_VERSION"],
					data["SOLOPMT_TIMESTMP"],
					data["SOLOPMT_RCV_ID"],
					data["SOLOPMT_LANGUAGE"],
					data["SOLOPMT_RESPTYPE"],
					data["SOLOPMT_REF"],
					data["SOLOPMT_KEYVERS"],
					data["SOLOPMT_ALG"],
					Secret
				)
				.Hash("MD5");

			// Make request
			var client = new HttpClient();
			var response = await client.PostAsync(EndpointUrl, new FormUrlEncodedContent(data));
			response.EnsureSuccessStatusCode();
			var responseContent = await response.Content.ReadAsStringAsync();
			if(responseContent.Contains("<SOLOPMT_RESPCODE>OK</SOLOPMT_RESPCODE>")) {
				return true;
			}
			if(responseContent.Contains("<SOLOPMT_RESPCODE>Notfound</SOLOPMT_RESPCODE>")) {
				return false;
			}
			throw new VerificationProviderException("SOLO verification service returned unknown response.") { 
				Data = {
					{"Response", responseContent} 
				} 
			};
		}
	}
}
