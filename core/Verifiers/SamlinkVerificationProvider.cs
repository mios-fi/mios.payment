using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Mios.Payment.Verifiers {
	public class SamlinkVerificationProvider : IVerificationProvider {
		public string Account { get; set; }
		public string Secret { get; set; }
		public string KeyVersion { get; set; }
		public Uri EndpointUrl { get; set; }
		public int Version { get; set; }
		public string Currency { get; set; }

		public SamlinkVerificationProvider() {
			Version = 1;
			Currency = "EUR";
		}
		public SamlinkVerificationProvider(string data) {
			var parameters = HttpUtility.ParseQueryString(data);
			Account = parameters["account"];
			Secret = parameters["secret"];
			KeyVersion = KeyVersion ?? parameters["keyVersion"];
			Currency = Currency ?? parameters["currency"];
			Uri endpointUrl;
			if(Uri.TryCreate(parameters["endpointUrl"], UriKind.Absolute, out endpointUrl)) {
				EndpointUrl = endpointUrl;
			}
			int version = Version;
			if(int.TryParse(parameters["version"], out version)) {
				Version = version;
			}
		}

		public async Task<bool> VerifyPaymentAsync(string identifier, decimal expectedAmount) {
			if(String.IsNullOrEmpty(Account)) {
				throw new InvalidOperationException("Account must be set before calling VerifyPaymentAsync.");
			}
			if(String.IsNullOrEmpty(Secret)) {
				throw new InvalidOperationException("Secret must be set before calling VerifyPaymentAsync.");
			}
			if(EndpointUrl==null) {
				throw new InvalidOperationException("EndpointUrl must be set before calling VerifyPaymentAsync.");
			}
			if(Version==10 && String.IsNullOrEmpty(KeyVersion)) {
				throw new InvalidOperationException("KeyVersion must be set before calling VerifyPaymentAsync when using version 010 of the protocol.");
			}

			var data = new Dictionary<string,string> {
				{"NET_SELLER_ID", Account},
				{"NET_STAMP", identifier},
				{"NET_RETURN", "http://www.dermoshop.com"}
			};
			switch(Version) {
				case 1:
					data["NET_VERSION"] = "001";
					data["NET_MAC"] = Hash("MD5", data,
						"NET_VERSION",
						"NET_SELLER_ID",
						"NET_STAMP",
						"NET_REF"
					);
					break;
				case 3:
					data["NET_VERSION"] = "003";
					data["NET_ALG"] = "03";
					data["NET_MAC"] = Hash("SHA256", data,
						"NET_VERSION",
						"NET_SELLER_ID",
						"NET_STAMP",
						"NET_REF",
						"NET_ALG"
					);
					break;
				case 10:
					data["NET_VERSION"] = "010";
					data["NET_ALG"] = "03";
					data["NET_KEYVERS"] = KeyVersion;
					data["NET_MAC"] = Hash("SHA256", data,
						"NET_VERSION",
						"NET_SELLER_ID",
						"NET_STAMP",
						"NET_REF",
						"NET_ALG",
						"NET_KEYVERS"
					);
					break;
				default:
					throw new InvalidOperationException("Unsupported version "+Version);	
			}

			// Make request
			var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
			var response = await client.PostAsync(EndpointUrl, new FormUrlEncodedContent(data));
			if(response.StatusCode!=HttpStatusCode.Found) {
				throw new VerificationProviderException("Expected 302 FOUND in Samlink response but found "+response.StatusCode+".") {
					Data = {
						{ "Response", await response.Content.ReadAsStringAsync() }
					}
				};
			}

			// Parse location header with response
			var responseFields = HttpUtility.ParseQueryString(response.Headers.Location.Query);
			var responseContent = await response.Content.ReadAsStringAsync();

			// Throw for unknown response code
			if(responseFields["NET_RESPCODE"]!="OK" && responseFields["NET_RESPCODE"]!="NOTFOUND") {
				throw new VerificationProviderException("Unexpected response code "+responseFields["NET_RESPCODE"]+" in response.") {
					ResponseContent = responseContent,
					Data = {
						{ "Location", response.Headers.Location.ToString() }
					}
				};
			}

			// Return false when payment not found
			if(responseFields["NET_RESPCODE"]=="NOTFOUND") {
				return false;
			}

			// Validate response MAC
			string expectedHash;
			switch(responseFields["NET_VERSION"]) {
				case "001":
					expectedHash = Hash("MD5", responseFields,
						"NET_VERSION",
						"NET_SELLER_ID",
						"NET_RESPCODE",
						"NET_STAMP",
						"NET_REF",
						"NET_DATE",
						"NET_AMOUNT",
						"NET_CUR",
						"NET_PAID"
					);
					break;
				case "003":
					expectedHash = Hash("SHA256", responseFields,
						"NET_VERSION",
						"NET_SELLER_ID",
						"NET_RESPCODE",
						"NET_STAMP",
						"NET_REF",
						"NET_DATE",
						"NET_AMOUNT",
						"NET_CUR",
						"NET_PAID",
						"NET_ALG"
					);
					break;
				case "010":
					expectedHash = Hash("SHA256", responseFields,
						"NET_VERSION",
						"NET_SELLER_ID",
						"NET_RESPCODE",
						"NET_STAMP",
						"NET_REF",
						"NET_DATE",
						"NET_AMOUNT",
						"NET_CUR",
						"NET_PAID",
						"NET_ALG",
						"NET_KEYVERS"
					);
					break;
				default:
					// Throw for unexpected protocol versions
					throw new VerificationProviderException("Unsupported version "+responseFields["NET_VERSION"]+" in response") {
						ResponseContent = responseContent,
						Data = {
							{ "Location", response.Headers.Location.ToString() }
						}
					};
			}
			// Throw for mismatching return MAC
			if(responseFields["NET_RETURN_MAC"] != expectedHash) {
				throw new VerificationProviderException("Expected hash starting with "+expectedHash.Substring(0,10)+" but found "+responseFields["NET_RETURN_MAC"]) {
					ResponseContent = responseContent,
					Data = {
							{ "Location", response.Headers.Location.ToString() }
						}
				};
			}

			// Throw for unexpected currency
			if(responseFields["NET_CUR"]!=Currency) {
				throw new VerificationProviderException("Expected currency "+Currency+" but found "+responseFields["NET_CUR"]+" in response.") {
					ResponseContent = responseContent,
					Data = {
						{ "Location", response.Headers.Location.ToString() }
					}
				};
			}
			// Throw for unexpected amount in response
			if(decimal.Parse(responseFields["NET_AMOUNT"],CultureInfo.InvariantCulture) != expectedAmount) {
				throw new VerificationProviderException("Expected amount "+expectedAmount.ToString("f2", CultureInfo.GetCultureInfo("fi-FI"))+" but found "+responseFields["NET_AMOUNT"]) {
					ResponseContent = responseContent,
					Data = {
						{ "Location", response.Headers.Location.ToString() }
					}
				};
			}
			return true;
		}

		private string Hash(string algorithm, IDictionary<string, string> data, params string[] fields) {
			var str = String.Join("&", fields.Where(t => data.ContainsKey(t)).Select(t => data[t]))+"&"+Secret+"&";
			return str.Hash(algorithm);
		}
		private string Hash(string algorithm, NameValueCollection data, params string[] fields) {
			return Hash(algorithm, data.Keys.OfType<string>().ToDictionary(t => t, t => data[t]), fields);
		}
	}
}
