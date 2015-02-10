using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Mios.Payment.Verifiers {
	public class OpVerificationProvider : IVerificationProvider {

		public string Account { get; set; }
		public string Secret { get; set; }
		public Uri EndpointUrl { get; set; }

		public OpVerificationProvider() {
			EndpointUrl = new Uri("https://kultaraha.op.fi/cgi-bin/krcgi");
		}
		public OpVerificationProvider(string data) : this() {
			var parameters = HttpUtility.ParseQueryString(data);
			if(parameters["account"]==null) {
				throw new ArgumentException("Missing 'account' parameter in initialization string.");
			}
			if(parameters["secret"]==null) {
				throw new ArgumentException("Missing 'secret' parameter in initialization string.");
			}
			Account = parameters["account"];
			Secret = parameters["secret"];
		}

		public async Task<bool> VerifyPaymentAsync(string identifier, decimal amount) {

			var data = new Dictionary<string,string>() {
				{"action_id", "708"},
				{"VERSIO", "0006"},
				{"MYYJA", Account},
				{"KYSELYTUNNUS", "0"},
				{"MAKSUTUNNUS", identifier},
				{"VIITE", ReferenceCalculator.GenerateReferenceNumber(identifier)},
				{"TARKISTE-VERSIO", "6"},
				{"PALUU-LINKKI", "http://www.dermoshop.com"}
			};
			data["TARKISTE"] = 
				String.Join("", 
					data["VERSIO"], 
					data["MYYJA"], 
					data["KYSELYTUNNUS"], 
					data["MAKSUTUNNUS"], 
					data["VIITE"],
					data["TARKISTE-VERSIO"],
					Secret
				)
				.Hash("MD5");

			// Make request
			var client = new HttpClient();
			var response = await client.PostAsync(EndpointUrl, new FormUrlEncodedContent(data));
			response.EnsureSuccessStatusCode();
			var responseContent = await response.Content.ReadAsStringAsync();

			if(responseContent.Contains("Maksua ei ole maksettu")) {
				return false;
			} 
			if(responseContent.Contains("Maksu on maksettu")) {
				return true;
			}
			throw new VerificationProviderException("OP verification service returned unknown response.") {
				Data = {
					{"Response", responseContent}
				}
			};
		}
	}
}

