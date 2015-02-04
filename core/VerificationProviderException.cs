using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mios.Payment {
	[Serializable]
	public class VerificationProviderException : Exception {
		public VerificationProviderException() { }
		public VerificationProviderException(string message) : base(message) { }
		public VerificationProviderException(string message, Exception inner) : base(message, inner) { }
		protected VerificationProviderException(
		System.Runtime.Serialization.SerializationInfo info,
		System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}
}
	