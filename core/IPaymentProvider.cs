using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
namespace Mios.Payment {
	public interface IPaymentProvider {
		PaymentDetails GenerateDetails(string identifier, decimal amount, string returnUrl, string errorUrl, string message);
		bool VerifyResponse(string identifier, decimal amount, NameValueCollection fields);
	}
}
