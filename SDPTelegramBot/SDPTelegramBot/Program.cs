using System;

namespace SDPTelegramBot
{
	class Program
	{
		static void Main(string[] args)
		{
			Client client = new Client();
			client.Start(0, true);
			////SDPRequest request = new SDPRequest(6813);
			//Console.WriteLine(request.description);
			//Console.WriteLine(SDPRequest.convertFromHTML(request.description));

			Console.ReadKey();
		}
	}
}
