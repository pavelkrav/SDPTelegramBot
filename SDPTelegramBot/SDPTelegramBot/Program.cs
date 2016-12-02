using System;

namespace SDPTelegramBot
{
	class Program
	{
		static void Main(string[] args)
		{
			Client client = new Client();
			client.Start(0, true);

			Console.ReadKey();
		}
	}
}
