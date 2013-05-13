using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Tests {
	public class HMACSHATest {
		[Fact]
		public void AgreesWithTestVectors() {
			var key = "JefeJefeJefeJefeJefeJefeJefeJefe";
			var msg = "what do ya want for nothing?";
			var hmac = HMAC.Create("HMACSHA256");
			hmac.Key = Encoding.UTF8.GetBytes(key);
			var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(msg));
			Assert.Equal("167f928588c5cc2eef8e3093caa0e87c9ff566a14794aa61648d81621a2a40c6", BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant());
		}
	}
}
