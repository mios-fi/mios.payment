using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.OpenSsl;
using Xunit;
using System.Security.Cryptography;

namespace Tests {
	public class ProcessPEMsTest {
		[Fact]
		public void ShouldReadRSAParametersFromPrivateKey() {
			RSAParameters parameters;
			using(var stream = typeof(ProcessPEMsTest).Assembly.GetManifestResourceStream("Tests.Resources.kaupmees_priv.pem"))
			using(var reader = new StreamReader(stream)) {
				AsymmetricCipherKeyPair pair = null;
				while(true) {
					object obj = new PemReader(reader).ReadObject();
					if(obj == null) break;
					pair = obj as AsymmetricCipherKeyPair;
				}
				if(pair == null)
					throw new ArgumentOutOfRangeException("The specified stream does not contain a private key");
				parameters = DotNetUtilities.ToRSAParameters((RsaPrivateCrtKeyParameters)pair.Private);
			}
			Assert.NotNull(parameters);
		}
	}
}
