using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mios.Payment {
	public interface IVerificationProvider {
		Task<bool> VerifyPaymentAsync(string identifier, decimal? expectedAmount);
	}
}
