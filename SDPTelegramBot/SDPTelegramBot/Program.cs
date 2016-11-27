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
using HtmlAgilityPack;


namespace SDPTelegramBot
{
	class Program
	{
		static void Main(string[] args)
		{
			//long offset = 628589177;
			//while (true)
			//{
			//	TELRequest tr = new TELRequest("getUpdates", "offset", offset.ToString());
			//	GetUpdates updates = JsonConvert.DeserializeObject<GetUpdates>(tr.getResponseString());
			//	foreach (GetUpdatesResult res in updates.result)
			//	{
			//		Console.WriteLine($"{res.message.from.last_name}\t{res.message.text}");
			//		List<string> param = new List<string>() { "chat_id", "text" };
			//		List<string> param_def = new List<string>() { res.message.from.id.ToString(), "работает =)" };
			//		TELRequest answer = new TELRequest("sendMessage", param, param_def);
			//		answer.pushRequest();
			//		offset++;
			//	}
			//}



			//List<BotUser> list = new List<BotUser>() { new Admin("sd", 123), new Technician("dg", 445), new Technician("gh", 567), new BotUser("jk", 231) };
			//foreach (BotUser bu in list)
			//{
			//	Console.WriteLine(bu);
			//}

			Client client = new Client();
			client.Start(0, true);



			Console.ReadKey();
		}
	}
}
