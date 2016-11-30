using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using Newtonsoft.Json;
using System.IO;
using System.Web;


namespace SDPTelegramBot
{
	class Program
	{
		static void Main(string[] args)
		{
			Client client = new Client();
			client.Start(0, true);

			//SDPRequest req = new SDPRequest(6730);
			//Console.WriteLine("\u041a\u0443\u043b\u0438\u043a\u043e\u0432");



			Console.ReadKey();
		}
	}
}
