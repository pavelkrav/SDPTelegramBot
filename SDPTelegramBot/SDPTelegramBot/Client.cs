﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Xml;
using System.Configuration;
using System.IO;
using System.Web;
using Newtonsoft.Json;

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
			tickCheckNewRequests();
			tickCheckOpenRequestsChanges();
			//tickCheckTelegramUserRequests();

		}

		// check new requests, push them if pending, add pending request to user open requests list
		private void tickCheckNewRequests()
		{
			int newRequestsAmountSDP = checkNewRequestsSDP();
			if (reqAmountSDP < newRequestsAmountSDP)
			{
				for (int i = reqAmountSDP + 1; i <= newRequestsAmountSDP; i++)
				{
					SDPRequest request = new SDPRequest(i);
					if (request.sdp_status == "Success" && (request.status != "Выполнено" || request.status != "Закрыто"))
					{
						pushNewRequestToTechnician(request);
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

		private void tickCheckOpenRequestsChanges()
		{
			foreach (BotUser user in userList)
			{
				foreach (SDPRequest request in user.open_requests)
				{
					SDPRequest sdpreq = new SDPRequest(request.workorderid);
					//check if request is closed
					if (sdpreq.status == "Выполнено" || request.status == "Закрыто")
					{
						pushOnCloseRequestNotification(request);
						// change open requests list for old tech
						List<SDPRequest> newlist = user.open_requests;
						user.open_requests = new List<SDPRequest>();
						foreach (SDPRequest req in newlist)
						{
							if (req.workorderid != request.workorderid)
							{
								user.open_requests.Add(req);
							}
						}
					}

					//check if request is assigned to another technician
					if (request.technician != sdpreq.technician)
					{
						int new_tech = -1;
						for (int i = 0; i < userList.Count; i++)
						{
							if (userList[i].sdp_name == sdpreq.technician)
							{
								new_tech = i;
							}
						}
						pushTechnicianChangedNotificationToOld(user, sdpreq.technician, request);
						// change open requests list for old tech
						List<SDPRequest> newlist_old = user.open_requests;
						user.open_requests = new List<SDPRequest>();
						foreach (SDPRequest req in newlist_old)
						{
							if (req.workorderid != request.workorderid)
							{
								user.open_requests.Add(req);
							}
						}
						// check if new technician is registered in bot
						if (new_tech >= 0)
						{
							pushTechnicianChangedNotificationToNew(userList[new_tech], request.technician, sdpreq);
							// change open requests list for new tech							
							userList[new_tech].open_requests.Add(sdpreq);
						}
					}
					//check request priority
					if (request.priority != sdpreq.priority)
					{
						pushPriorityChangeToTechnician(user.tel_id, sdpreq, request.priority);
						// change open requests list
						List <SDPRequest> newlist = user.open_requests;
						user.open_requests = new List<SDPRequest>();
						foreach(SDPRequest req in newlist)
						{
							if (req.workorderid != request.workorderid)
							{
								user.open_requests.Add(req);
							}
							else
							{
								user.open_requests.Add(new SDPRequest(req.workorderid));
							}
						}
					}
				}
			}
		}

		private void tickCheckTelegramUserRequests()
		{
			TELRequest request = new TELRequest("getUpdates", "offset", offset.ToString());
			GetUpdates updates = JsonConvert.DeserializeObject<GetUpdates>(request.getResponseString());
			foreach (GetUpdatesResult result in updates.result)
			{
				if (result.message.text.Length > 0)
				{
					string message = getTelegramBotAnswer(result.message.text);

					List<string> param = new List<string>() { "chat_id", "text" };
					List<string> param_def = new List<string>() { result.message.from.id.ToString(), message };
					TELRequest answer = new TELRequest("sendMessage", param, param_def);
					answer.pushRequest();
				}
				offset++;
			}
		}

		private string getTelegramBotAnswer(string messageText)
		{
			if(getTelegramBotCommand(ref messageText))
			{

			}
		}

		private bool getTelegramBotCommand(ref string request)
		{

			if (request[0] == '/' || request[0] == '!')
			{

			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Returns new request amount. Does not update field reqAmountSDP. Supposed to run every tick.
		/// </summary>
		/// <returns>New request amount</returns>
		private int checkNewRequestsSDP()			// may be optimized with single http request?
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
			while (err < 5);	// deep = 5

			return newReqAmount;
		}

		private void pushNewRequestToTechnician(SDPRequest request)
		{
			long tel_id = 0;

			foreach (BotUser user in userList)
			{
				if (request.technician == user.sdp_name)
					tel_id = user.tel_id;
			}

			// check if technician is supposed to get notifications
			if (tel_id > 0)
			{
				string message = null;
				message += $"Вам поступила новая заявка ID{request.workorderid} от {request.requester}";
				message += "\n" + $"Приоритет: {request.priority}";
				message += "\n" + request.subject;
				message += "\n" + request.shortdescription;     // need to decode html string to plain text from full description
				if (request.area != "Рождественка")
					message += $"\nПлощадка: {request.area}";
				
				List<string> param = new List<string>() { "chat_id", "text" };
				List<string> param_def = new List<string>() {tel_id.ToString(), message };
				TELRequest msg = new TELRequest("sendMessage", param, param_def);
				msg.pushRequest();
			}
		}

		private void pushPriorityChangeToTechnician(long tel_id, SDPRequest request, string old_priority)
		{
			// check if technician is supposed to get notifications
			if (tel_id > 0)
			{
				string message = $"Приоритет Вашей заявки ID{request.workorderid} ({request.subject}) изменен с {old_priority} на {request.priority}.";
				List<string> param = new List<string>() { "chat_id", "text" };
				List<string> param_def = new List<string>() { tel_id.ToString(), message };
				TELRequest msg = new TELRequest("sendMessage", param, param_def);
				msg.pushRequest();
			}
		}

		private void pushTechnicianChangedNotificationToOld(BotUser old_tech, string new_tech, SDPRequest request)
		{
			// check if technician is supposed to get notifications
			if (old_tech.tel_id > 0)
			{
				string message = $"Ваша заявка ID{request.workorderid} ({request.subject}) назначена на нового инженера - {new_tech}.";
				List<string> param = new List<string>() { "chat_id", "text" };
				List<string> param_def = new List<string>() { old_tech.tel_id.ToString(), message };
				TELRequest msg = new TELRequest("sendMessage", param, param_def);
				msg.pushRequest();
			}
		}

		private void pushTechnicianChangedNotificationToNew(BotUser new_tech, string old_tech, SDPRequest request)
		{
			// check if technician is supposed to get notifications
			if (new_tech.tel_id > 0)
			{
				string message = $"Заявка ID{request.workorderid}, ранее назначенная на {old_tech}, переадресована Вам:";
				List<string> param = new List<string>() { "chat_id", "text" };
				List<string> param_def = new List<string>() { new_tech.tel_id.ToString(), message };
				TELRequest msg = new TELRequest("sendMessage", param, param_def);
				msg.pushRequest();
				pushNewRequestToTechnician(request);	// merge in 1 message later
			}
		}

		private void pushOnCloseRequestNotification(SDPRequest request)
		{
			long tel_id = 0;

			foreach (BotUser user in userList)
			{
				if (request.technician == user.sdp_name)
					tel_id = user.tel_id;
			}

			// check if technician is supposed to get notifications
			if (tel_id > 0)
			{
				string message = $"Ваша заявка ID{request.workorderid} ({request.subject}) закрыта.";
				List<string> param = new List<string>() { "chat_id", "text" };
				List<string> param_def = new List<string>() { tel_id.ToString(), message };
				TELRequest msg = new TELRequest("sendMessage", param, param_def);
				msg.pushRequest();
			}
		}

		public void pushMessageToAll(string message)
		{
			foreach (BotUser user in userList)
			{
				List<string> param = new List<string>() { "chat_id", "text" };
				List<string> param_def = new List<string>() { user.tel_id.ToString(), message };
				TELRequest msg = new TELRequest("sendMessage", param, param_def);
				msg.pushRequest();
			}
		}

	}
}
