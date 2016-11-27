using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Xml;
using System.Configuration;
using System.IO;
using System.Web;

namespace SDPTelegramBot
{
	class Client
	{
		private int reqAmountSDP;
		private int techAmount;
		private int adminAmount;
		private long offset;

		private List<BotUser> userList;

		private string iniPath;

		public Client()
		{
			iniPath = ConfigurationManager.AppSettings["INI_PATH"];
			userList = new List<BotUser>();

			try
			{
				using (XmlReader reader = XmlReader.Create(iniPath))
				{
					reader.ReadToFollowing("ini");
					reader.ReadToFollowing("lastrequest");
					reqAmountSDP = reader.ReadElementContentAsInt();
					reader.ReadToFollowing("offset");
					offset = reader.ReadElementContentAsLong();

					reader.ReadToFollowing("technicians");
					reader.ReadToFollowing("amount");
					techAmount = reader.ReadElementContentAsInt();

					for (int i = 0; i < techAmount; i++)
					{
						reader.ReadToFollowing("technician");
						string sdp_name = reader.ReadElementContentAsString();
						reader.ReadToFollowing("tel_id");
						long tel_id = reader.ReadElementContentAsLong();
						userList.Add(new Technician(sdp_name, tel_id)); 
					}

					reader.ReadToFollowing("admins");
					reader.ReadToFollowing("amount");
					adminAmount = reader.ReadElementContentAsInt();

					for (int i = 0; i < adminAmount; i++)
					{
						reader.ReadToFollowing("admin");
						string sdp_name = reader.ReadElementContentAsString();
						reader.ReadToFollowing("tel_id");
						long tel_id = reader.ReadElementContentAsLong();
						userList.Add(new Admin(sdp_name, tel_id)); 
					}
				}
				initialUpdateReqAmountSDP();
				initialOpenRequestsCheck();
				Console.WriteLine();
				consoleOutput();
			}
			catch (Exception e)
			{
				Console.WriteLine("Could not find \"ini.xml\" file or it is not valid.");
				Console.WriteLine(e.Message);
				Console.ReadKey();
				Environment.Exit(0);
			}
		}

		public void consoleOutput()
		{
			Console.WriteLine($"Technicians: {techAmount}");
			foreach (BotUser tech in userList)
			{
				if (tech.ToString() == "Technician")
				{
					Console.WriteLine($"{tech.sdp_name}\tid: {tech.tel_id}\tOpen requests: {tech.open_requests.Count}");
				}
			}
			Console.WriteLine($"\nAdmins: {adminAmount}");
			foreach (BotUser admin in userList)
			{
				if (admin.ToString() == "Admin")
				{
					Console.WriteLine($"{admin.sdp_name}\tid: {admin.tel_id}\tOpen requests: {admin.open_requests.Count}");
				}
			}
			Console.WriteLine();
		}

		private void initialUpdateReqAmountSDP()
		{
			int newReqAmount = reqAmountSDP;
			int err = 0;
			int i = 0;
			SDPRequest req = new SDPRequest(reqAmountSDP);
			do
			{
				i++;
				Console.Write($"Request #{reqAmountSDP + i} ");
				req = new SDPRequest(reqAmountSDP + i);
				if (req.sdp_status == "Failed")
				{
					Console.Write("Failed\n");
					err++;
				}
				else if (req.sdp_status == "Success")
				{
					Console.Write("Success\n");
					newReqAmount = req.workorderid;
					err = 0;
				}
				else
					err = 100;
			}
			while (err < 30);
			Console.Clear();

			try
			{
				XmlDocument doc = new XmlDocument();
				doc.Load(iniPath);
				XmlElement ini = doc.DocumentElement;
				XmlNode lastreq = ini.FirstChild;
				lastreq.RemoveChild(lastreq.FirstChild);
				lastreq.AppendChild(doc.CreateTextNode(newReqAmount.ToString()));

				doc.Save(iniPath);
				Console.WriteLine("\"ini.xml\" file has been changed");
			}
			catch (Exception e)
			{
				Console.WriteLine("Could not find \"ini.xml\" file or it is not valid.");
				Console.WriteLine("ini.xml file has not been changed");
				Console.WriteLine(e.Message);
			}
			reqAmountSDP = newReqAmount;
			Console.WriteLine($"Last request is #{reqAmountSDP}");
		}

		private void saveRequestsAmountAs(int amount)
		{
			XmlDocument doc = new XmlDocument();
			doc.Load(iniPath);
			XmlElement ini = doc.DocumentElement;
			XmlNode lastreq = ini.FirstChild;
			lastreq.RemoveChild(lastreq.FirstChild);
			lastreq.AppendChild(doc.CreateTextNode(amount.ToString()));

			doc.Save(iniPath);
		}

		private void initialOpenRequestsCheck()
		{
			long week = 86400 * 7 * 1000;

			SDPRequest req = new SDPRequest(reqAmountSDP);
			long lTime = req.createdtime;

			int i = reqAmountSDP;
			do
			{
				req = new SDPRequest(i);
				if (req.sdp_status == "Failed")
				{
					Console.WriteLine($"Checked request #{i} - Does not exist");
					i--;
				}
				else if (req.sdp_status == "Success" && (req.status == "Выполнено" || req.status == "Закрыто"))
				{
					Console.WriteLine($"Checked request #{i} - Resolved");
					i--;
				}
				else if (req.sdp_status == "Success" && (req.status == "Зарегистрирована" || req.status == "В ожидании"))
				{
					//
					foreach(BotUser user in userList)
					{
						if (user.sdp_name == req.technician)
						{
							user.open_requests.Add(req);
						}
					}
					Console.WriteLine($"Checked request #{i} - Pending");
					i--;
				}
				else
				{
					Console.WriteLine($"Checked request #{i} - Unexpected error");
					i--;
				}
			}
			while (req.createdtime > lTime - week * 5 || req.sdp_status == "Failed");   // checking for last 5 weeks
			Console.Clear();
		}

		public void Start(int ticks = 0, bool msg = false)
		{
			if (ticks == 0)
			{
				while (true)
				{
					Tick(msg);
				}
			}
			else if (ticks > 0)
			{
				for (int i = 0; i < ticks; i++)
				{
					Tick(msg);
				}
			}
			else
			{
				Console.WriteLine("Ticks amount should be greater than 0");
				Console.ReadKey();
				Environment.Exit(0);
			}
		}

		/// <summary>
		/// Single tick of bot
		/// </summary>
		/// <param name="msg">Console tick info output if true. Default false.</param>
		private void Tick(bool msg = false)
		{
			if(msg)
			{
				Console.WriteLine($"New tick\t{DateTime.Now.ToString("hh:mm:ss")}\toffset = {offset}\tlast request = {reqAmountSDP}");
			}

			// check new requests, push them if pending, add pending request to user open requests list
			int newRequestsAmountSDP = checkNewRequestsSDP();
			if (reqAmountSDP < newRequestsAmountSDP)
			{
				for (int i = reqAmountSDP + 1; i <= newRequestsAmountSDP; i++)
				{
					SDPRequest request = new SDPRequest(i);
					if (request.sdp_status == "Success" && (request.status != "Выполнено" || request.status != "Закрыто"))
					{
						pushRequestToTechnician(request);
					}
					reqAmountSDP++;
					foreach (BotUser user in userList)
					{
						if (request.technician == user.sdp_name)
						{
							user.open_requests.Add(request);
						}
					}
					saveRequestsAmountAs(reqAmountSDP);
				}
			}


		}

		/// <summary>
		/// Returns new request amount. Does not update field reqAmountSDP.
		/// </summary>
		/// <returns>New request amount</returns>
		private int checkNewRequestsSDP()
		{
			int newReqAmount = reqAmountSDP;
			int err = 0;
			int i = 0;
			SDPRequest req = new SDPRequest(reqAmountSDP);
			do
			{
				i++;
				//Console.Write($"Request #{reqAmount + i} ");
				req = new SDPRequest(reqAmountSDP + i);
				if (req.sdp_status == "Failed")
				{
					//Console.Write("Failed\n");
					err++;
				}
				else if (req.sdp_status == "Success")
				{
					//Console.Write("Success\n");
					newReqAmount = req.workorderid;
					err = 0;
				}
				else
					err = 100;
			}
			while (err < 3);

			return newReqAmount;
		}

		private void pushRequestToTechnician(SDPRequest request)
		{
			long tel_id = 0;

			foreach (BotUser user in userList)
			{
				if (request.technician == user.sdp_name)
					tel_id = user.tel_id;
			}

			if (tel_id > 0)
			{
				string message = null;
				message += $"Поступила новая заявка ID{request.workorderid} от {request.requester}";
				message += "\n" + $"Приоритет: {request.priority}";
				message += "\n" + request.subject;
				message += "\n" + request.shortdescription;     // need to decode html string to plain text
				if (request.area != "Рождественка")
					message += $"\nПлощадка: {request.area}";
				
				List<string> param = new List<string>() { "chat_id", "text" };
				List<string> param_def = new List<string>() {tel_id.ToString(), message };
				TELRequest msg = new TELRequest("sendMessage", param, param_def);
				msg.pushRequest();
			}
		}

	}
}
