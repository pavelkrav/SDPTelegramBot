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
using Newtonsoft.Json;
using Microsoft.Office.Interop.Excel;

namespace SDPTelegramBot
{
	class Client
	{
		private int reqAmountSDP;
		private int techAmount;
		private int adminAmount;
		private long offset;

		private List<BotUser> userList;
		private List<SDPCloseSession> closeSessionList;

		private string iniPath;

		public Client()
		{
			iniPath = ConfigurationManager.AppSettings["INI_PATH"];
			userList = new List<BotUser>();
			closeSessionList = new List<SDPCloseSession>();

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
						reader.ReadToFollowing("abb");
						string abb = reader.ReadElementContentAsString();
						userList.Add(new Technician(sdp_name, tel_id, abb));
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
						reader.ReadToFollowing("abb");
						string abb = reader.ReadElementContentAsString();
						userList.Add(new Admin(sdp_name, tel_id, abb)); 
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
				int spc = 32 - tech.sdp_name.Length; // total shit =)
				string spaces = null;				// tho no sense to do it properly
				if (spc > 0)
				{
					for (int i = 0; i < spc; i++)
					{
						spaces += " ";
					}
				}
				if (tech.ToString() == "Technician")
				{
					Console.WriteLine($"{tech.sdp_name:31} ({tech.abbreviation}){spaces}\tid: {tech.tel_id}\tOpen requests: {tech.open_requests.Count}");
				}
			}
			Console.WriteLine($"\nAdmins: {adminAmount}");
			foreach (BotUser admin in userList)
			{
				if (admin.ToString() == "Admin")
				{
					Console.WriteLine($"{admin.sdp_name} ({admin.abbreviation})\tid: {admin.tel_id}\tOpen requests: {admin.open_requests.Count}");
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

			reqAmountSDP = newReqAmount;
			saveRequestsAmountAs(reqAmountSDP);
			Console.WriteLine($"Last request is #{reqAmountSDP}");
		}

		private void saveRequestsAmountAs(int amount)
		{
			try
			{
				XmlDocument doc = new XmlDocument();
				doc.Load(iniPath);
				XmlNode lastreq = doc.SelectSingleNode("/ini/lastrequest");
				lastreq.RemoveChild(lastreq.FirstChild);
				lastreq.AppendChild(doc.CreateTextNode(amount.ToString()));

				doc.Save(iniPath);
			}
			catch (Exception e)
			{
				Console.WriteLine("Could not find \"ini.xml\" file or it is not valid.");
				Console.WriteLine("ini.xml file has not been changed");
				Console.WriteLine(e.Message);
				Console.ReadKey();
				Environment.Exit(0);
			}
		}

		private void saveOffsetAs(long offset)
		{
			try
			{
				XmlDocument doc = new XmlDocument();
				doc.Load(iniPath);
				XmlNode offsetNode = doc.SelectSingleNode("/ini/offset");
				offsetNode.RemoveChild(offsetNode.FirstChild);
				offsetNode.AppendChild(doc.CreateTextNode(offset.ToString()));

				doc.Save(iniPath);
			}
			catch (Exception e)
			{
				Console.WriteLine("Could not find \"ini.xml\" file or it is not valid.");
				Console.WriteLine("ini.xml file has not been changed");
				Console.WriteLine(e.Message);
				Console.ReadKey();
				Environment.Exit(0);
			}
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
				Console.WriteLine($"New tick\t{DateTime.Now.ToString("H:mm:ss")}\toffset = {offset}\tlast request = {reqAmountSDP}");
			}
			try
			{
				tickCheckCloseSessions();
				tickCheckNewRequests();
				tickCheckOpenRequestsChanges();
				tickCheckTelegramUserRequests();
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}

		}

		// check close sessions, push messages
		private void tickCheckCloseSessions()
		{
			TELRequest request = new TELRequest("getUpdates", "offset", offset.ToString());
			GetUpdates updates = JsonConvert.DeserializeObject<GetUpdates>(request.getResponseString());

			foreach (SDPCloseSession session in	closeSessionList)
			{
				if(session.time <= 0)
				{
					if (!session.timeMsg)
					{
						string message = $"Введите количество минут, затраченное на выполнение заявки ID{session.request.workorderid}";
						List<string> param = new List<string>() { "chat_id", "text" };
						List<string> param_def = new List<string>() { session.user.tel_id.ToString(), message };
						TELRequest answer = new TELRequest("sendMessage", param, param_def);
						answer.pushRequest();
						session.timeMsg = true;
					}
					else if (!session.resolutionMsg && session.time > 0)
					{
						string message = $"Введите решения для завки ID{session.request.workorderid}";
						List<string> param = new List<string>() { "chat_id", "text" };
						List<string> param_def = new List<string>() { session.user.tel_id.ToString(), message };
						TELRequest answer = new TELRequest("sendMessage", param, param_def);
						answer.pushRequest();
						session.resolutionMsg = true;
					}
				}
				if(session.time > 0 && !String.IsNullOrEmpty(session.resolution))
				{
					bool closed = session.close();
					closeSessionList.Remove(session);
					string message = null;
					if (closed)
						message = $"Заявка ID{session.request.workorderid} успешно закрыта.";
					else
						message = $"Заявка ID{session.request.workorderid} не была закрыта. Обратитесь к администратору бота.";
					List<string> param = new List<string>() { "chat_id", "text" };
					List<string> param_def = new List<string>() { session.user.tel_id.ToString(), message };
					TELRequest answer = new TELRequest("sendMessage", param, param_def);
					answer.pushRequest();
				}
			}
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
					if (request.requester == "Буров Александр Вячеславович")
						SDPRequest.handleBurovRequest(i);
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
				}
				saveRequestsAmountAs(reqAmountSDP);
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
					if (request.technician != sdpreq.technician && !String.IsNullOrWhiteSpace(sdpreq.technician))
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
					if (request.priority != sdpreq.priority && !String.IsNullOrWhiteSpace(sdpreq.priority))
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

		// every user message should be handled here
		private void tickCheckTelegramUserRequests()
		{
			TELRequest request = new TELRequest("getUpdates", "offset", offset.ToString());
			GetUpdates updates = JsonConvert.DeserializeObject<GetUpdates>(request.getResponseString());
			foreach (GetUpdatesResult result in updates.result)
			{
				foreach (SDPCloseSession session in closeSessionList)
				{
					// check if message includes text and user id accordance
					if (result.message.text.Length > 0 && result.message.from.id == session.user.tel_id)
					{
						// check if it is not a bot command
						string userMessage = result.message.text;
						if (!(userMessage[0] == '/' || userMessage[0] == '!' || userMessage[0] == '.'))
						{
							// time first
							if (session.time <= 0)
							{
								try
								{
									int minutes = Convert.ToInt32(userMessage);
									if (minutes > 0)
										session.time = minutes;
									else throw new ArgumentOutOfRangeException();
								}
								catch
								{
									string message = "Введите корректное значение времени выполнения";
									List<string> param = new List<string>() { "chat_id", "text" };
									List<string> param_def = new List<string>() { session.user.tel_id.ToString(), message };
									TELRequest answer = new TELRequest("sendMessage", param, param_def);
									answer.pushRequest();
								}
							}
							// then resolution
							else if (String.IsNullOrEmpty(session.resolution))
							{
								try
								{
									if (userMessage.Length > 9)
										session.resolution = userMessage;
									else throw new ArgumentOutOfRangeException();
								}
								catch
								{
									string message = "Введите корректное решение для заявки (более 9 символов)";
									List<string> param = new List<string>() { "chat_id", "text" };
									List<string> param_def = new List<string>() { session.user.tel_id.ToString(), message };
									TELRequest answer = new TELRequest("sendMessage", param, param_def);
									answer.pushRequest();
								}
							}
						}
					}
				}

				// check if message includes text
				if (result.message.text.Length > 0)
				{
					int user = -1;
					for (int i = 0; i < userList.Count; i++)	// should have used LINQ here?
					{
						if (userList[i].tel_id == result.message.from.id)
						{
							user = i;
						}
					}
					// check if user is registered in bot
					if (user >= 0)
					{
						string message = getTelegramBotAnswer(result.message.text, userList[user]);

						if (message != null)
						{
							List<string> param = new List<string>() { "chat_id", "text" };
							List<string> param_def = new List<string>() { userList[user].tel_id.ToString(), message };
							TELRequest answer = new TELRequest("sendMessage", param, param_def);
							answer.pushRequest();
						}
					}
					// otherwise
					else
					{
						string message = null;
						message += $"Hi, {result.message.from.first_name}!\n";
						message += "This bot is supposed to manage ServiceDesk Plus through telegram.\n";
						message += $"Only registered users can use the bot. Your ID for registration: {result.message.chat.id}.\n";
						message += "If you got any questions, contact administrator:\nPavel Kravtsov +7(916)179-60-67";

						if (message != null)
						{
							List<string> param = new List<string>() { "chat_id", "text" };
							List<string> param_def = new List<string>() { result.message.chat.id.ToString(), message };
							TELRequest answer = new TELRequest("sendMessage", param, param_def);
							answer.pushRequest();
						}
					}
				}
				offset++;
			}
			saveOffsetAs(offset);
		}

		private string getTelegramBotAnswer(string messageText, BotUser user)
		{
			string command = messageText;
			string subcommand = messageText;
			string answer = null;
			if (getTelegramBotCommand(ref command))
			{
				string[] name = user.sdp_name.Split(' ');
				switch (command)
				{
					case "help":        // done		
						if (name.Length >= 2)
							answer = getHelpAnswer(name[1]);
						else answer = getHelpAnswer(user.sdp_name);
						break;
					case "info":		// done
						if (getTelegramBotSubCommand(ref subcommand))
						{
							answer = getRequestInfoAnswer(user, subcommand);
						}
						else
						{
							answer = getInfoAnswer();
						}
						break;
					case "check":		// just for stupid fags
						goto case "pending";
					case "pending":		// done
						answer = getPendingAnswer(user);
						break;
					case "close":		// done
						try
						{
							if (getTelegramBotSubCommand(ref subcommand))
							{
								answer = getCloseAnswer(user, subcommand);
							}
							else
							{
								answer = "Введите номер заявки через пробел";
							}
						}
						catch
						{
							answer = "Введите номер заявки через пробел";
						}
						break;
					case "userlist":	// done
						answer = getUserlistAnswer();
						break;
					case "report":
						if (user.ToString() == "Admin")
						{
							answer = "not ready yet";
							break;
						}
						else return null;
					case "abort":
						int reqID = 0;
						foreach(SDPCloseSession session in closeSessionList)
						{
							if (user.tel_id == session.user.tel_id)
							{
								reqID = session.request.workorderid;
								closeSessionList.Remove(session);
							}
						}
						if (reqID > 0)
							answer = $"Сессии закрытия заявки ID{reqID} прервана.";
						else
							answer = "Открытых сессий нет.";
						break;
					case "hui":
						try
						{
							if (getTelegramBotSubCommand(ref subcommand))
							{
								answer = getHuiAnswer(user, subcommand);
							}
							else
							{
								answer = "Введите псевдоним посылаемого через пробел";
							}
						}
						catch
						{
							answer = "Псевдоним введен неверно";
						}
						break;
					default:
						return null;
				}

				return answer;
			}
			else
			{
				return null;
			}
		}

		private string getHelpAnswer(string name)
		{
			string answer = null;
			answer += $"Привет, {name}!\n";
			answer += "Запросы можно вводить через \"/\", \"!\" или \".\"\nСписок запросов:\n";
			answer += "/info - информация о боте\n";
			answer += "/info [ID] - информация по заявке с указанным номером\n";
			answer += "/pending - список открытых заявок\n";
			answer += "/close [ID] - закрыть заявку с указанным номером (номер следует вводить через пробел)\n";
			answer += "/userlist - Список зарегистрированных пользователей\n";
			answer += "/report - Отчет о закрытых заявках (доступно только администраторам бота)";

			return answer;
		}

		private string getInfoAnswer()
		{
			string answer = null;
			answer += "Этот бот работает с заявками из ServiceDesk \"НПО Городские Системы\". Он отслеживает новые заявки и изменения в старых, присылая оповещения инженерам.\n";
			answer += "Более того, бот обрабатывает запросы. В любой момент времени Вы можете узнать свои открытые заявки и закрыть их. Чтобы узнать список возможных запросов, используйте команду /help.\n";
			answer += "Бот работает только с зарегистрированными администратором пользователями.\n";
			answer += "При обнаружении ошибок, а также по любым вопросам, просьба обращаться к администратору:\nКравцов Павел \u002B7(916)179-60-67\n";

			return answer;
		}

		private string getRequestInfoAnswer(BotUser user, string subcommand)
		{
			int id = 0;
			try
			{
				id = Convert.ToInt32(subcommand);
			}
			catch
			{
				return "Введите допустимый номер заявки";
			}
			SDPRequest request = new SDPRequest(id);
			if (request.status == "Failed" || request.workorderid > reqAmountSDP)
				return $"Заявка ID{id} не существует.";
			else
			{
				string message = null;

				if (request.status == "Выполнено" || request.status == "Закрыто")
				{
					message += $"Заявка ID{request.workorderid} - закрыта\n";
					message += $"Выполнил: {request.technician}\n";
					message += $"Время выполнения: {request.timespentonreq}\n";
					message += $"\n{request.subject}\n";
					message += $"{SDPRequest.convertFromHTML(request.description)}\n";
					message += $"Площадка: {request.area}";
				}
				else if (request.status == "Зарегистрирована" || request.status == "В ожидании")
				{
					message += $"Заявка ID{request.workorderid}\n";
					message += $"Назначена на: {request.technician}\n";
					message += $"Приоритет: {request.priority}\n";
					message += $"\n{request.subject}\n";
					message += $"{SDPRequest.convertFromHTML(request.description)}\n";
					message += $"Площадка: {request.area}";
				}
				else
				{
					message += $"Заявка ID{request.workorderid}\n";
					message += $"Назначена на: {request.technician}\n";
					message += $"Статус: {request.status}\n";
					message += $"Приоритет: {request.priority}\n";
					message += $"\n{request.subject}\n";
					message += $"{SDPRequest.convertFromHTML(request.description)}\n";
					message += $"Площадка: {request.area}";
				}

				return message;
			}
		}

		private string getPendingAnswer(BotUser user)
		{
			string answer = null;
			if (user.open_requests.Count > 0)
			{
				foreach (SDPRequest request in user.open_requests)
				{
					answer += $"ID{request.workorderid} - {request.subject}\n";
				}
				answer += $"\nИтого: {user.open_requests.Count} Заявок\n";
				answer += "Для подробной информации по заявке введите команду /info [ID]";
			}
			else
			{
				answer += "У Вас нет открытых заявок. Поздравляю!";
			}
			return answer;
		}

		private string getCloseAnswer(BotUser user, string subcommand)
		{
			int id = 0;
			try
			{
				id = Convert.ToInt32(subcommand);
			}
			catch
			{
				return "Введите допустимый номер заявки";
			}
			SDPRequest request = new SDPRequest(id);
			if (request.status == "Failed")
				return $"Заявка ID{id} не существует.";
			else if (request.status == "Выполнено" || request.status == "Закрыто")
				return $"Заявка ID{request.workorderid} уже закрыта.";
			else if (request.technician != user.sdp_name)
				return $"Заявка ID{request.workorderid} назначена не на Вас. Текущий специалист - {request.technician}.";
			else
			{
				// check if user in close session
				bool sessionIsOpen = false;
				foreach (SDPCloseSession session in closeSessionList)
				{
					if (session.user.tel_id == user.tel_id)
						sessionIsOpen = true;
				}
				if (!sessionIsOpen)
				{
					closeSessionList.Add(new SDPCloseSession(user, request));
					return $"Сессия закрытия заявки ID{request.workorderid} открыта.";
				}
				else
				{
					return "У Вас уже есть открытая сессия закрытия заявки.";
				}
			}
		}

		private string getUserlistAnswer()
		{
			string answer = null;
			foreach (BotUser user in userList)
			{
				if (user.tel_id != 0)
					answer += $"{user.sdp_name} ({user.abbreviation})\n";
			}
			return answer;
		}

		private string getHuiAnswer(BotUser user, string subcommand)
		{
			BotUser destination = null;
			foreach (BotUser dest in userList)
			{
				if (dest.abbreviation == subcommand)
					destination = dest;
			}
			if (destination == null)
				return "Такого пользователя не существует";
			else if (destination.ToString() == "Admin")
				return "Нельзя послать администратора";
			else if (destination.tel_id == 0)
				return "Пользователь не подписан на сообщения от бота";
			else
			{
				string message = $"{user.sdp_name} посылает вас на хуй!";
				pushMessageTo(destination, message);
				return $"{destination.sdp_name} успешно послан";
			}
		}

		private bool getTelegramBotCommand(ref string request)
		{

			if (request[0] == '/' || request[0] == '!' || request[0] == '.')
			{
				request = request.Substring(1).ToLower();
				string[] splitted = request.Split(' ');
				if(splitted.Length > 2 || splitted.Length < 1)
				{
					return false;
				}
				else
				{
					request = splitted[0];
					return true;
				}
			}
			else
			{
				return false;
			}
		}

		private bool getTelegramBotSubCommand(ref string request)
		{
			if (request[0] == '/' || request[0] == '!' || request[0] == '.')
			{
				request = request.Substring(1).ToLower();
				string[] splitted = request.Split(' ');
				if (splitted.Length > 2 || splitted.Length < 1)
				{
					return false;
				}
				else
				{
					try
					{
						request = splitted[1];
						return true;
					}
					catch
					{
						return false;
					}
				}
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
			while (err < 5);	// deep = 3

			return newReqAmount;
		}

		private void pushMessageTo(BotUser user, string message)
		{
			// check if user is supposed to get notifications
			if (user.tel_id != 0)
			{
				List<string> param = new List<string>() { "chat_id", "text" };
				List<string> param_def = new List<string>() { user.tel_id.ToString(), message };
				TELRequest msg = new TELRequest("sendMessage", param, param_def);
				msg.pushRequest();
			}
		}

		private void pushMessageTo(long tel_id, string message)
		{
			// check if user is supposed to get notifications
			if (tel_id != 0)
			{
				List<string> param = new List<string>() { "chat_id", "text" };
				List<string> param_def = new List<string>() { tel_id.ToString(), message };
				TELRequest msg = new TELRequest("sendMessage", param, param_def);
				msg.pushRequest();
			}
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
				message += "\n\n" + request.subject;
				message += "\n" + SDPRequest.convertFromHTML(request.description);
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
				message += "\nДля подробной информации по заявке введите команду /info [ID]";
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
				message += "\nДля подробной информации по заявке введите команду /info [ID]";
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
				string message = $"Заявка ID{request.workorderid}, ранее назначенная на {old_tech}, переадресована Вам.";
				message += $"\nПриоритет: {request.priority}\n\n{request.subject}\n{SDPRequest.convertFromHTML(request.description)}";
				if (request.area != "Рождественка")
					message += $"\nПлощадка: {request.area}";
				List<string> param = new List<string>() { "chat_id", "text" };
				List<string> param_def = new List<string>() { new_tech.tel_id.ToString(), message };
				TELRequest msg = new TELRequest("sendMessage", param, param_def);
				msg.pushRequest();
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

			SDPRequest req = new SDPRequest(request.workorderid);
			// check if technician is supposed to get notifications
			if (tel_id > 0)
			{
				string message = $"Ваша заявка ID{request.workorderid} ({request.subject}) закрыта.\nВремя выполнения {req.timespentonreq}.";
				List<string> param = new List<string>() { "chat_id", "text" };
				List<string> param_def = new List<string>() { tel_id.ToString(), message };
				TELRequest msg = new TELRequest("sendMessage", param, param_def);
				msg.pushRequest();
			}
			// notification for me
			if (request.technician != ConfigurationManager.AppSettings["SDP_USER"])
			{
				string messageToAdmin = $"Заявка ID{request.workorderid} ({request.subject}), назначенная на {request.technician}, закрыта.";
				pushMessageTo(113054443, messageToAdmin);
			}
		}

		public void pushMessageToAll(string message)
		{
			foreach (BotUser user in userList)
			{
				// check if user is supposed to get notifications
				if (user.tel_id > 0 && user.ToString() != "Admin")
				{
					List<string> param = new List<string>() { "chat_id", "text" };
					List<string> param_def = new List<string>() { user.tel_id.ToString(), message };
					TELRequest msg = new TELRequest("sendMessage", param, param_def);
					msg.pushRequest();
				}
			}
		}

		public void createTsvReport()
		{
			long week = 86400 * 7 * 1000;

			int[] made = new int[techAmount];
			int[] pending = new int[techAmount];
			for (int j = 0; j < techAmount; j++)
			{
				made[j] = 0;
				pending[j] = 0;
			}

			SDPRequest req = new SDPRequest(reqAmountSDP);
			long lTime = req.createdtime;

			int i = reqAmountSDP;

			List<SDPRequest>[] resolvedList = new List<SDPRequest>[techAmount];
			List<SDPRequest>[] pendingList = new List<SDPRequest>[techAmount];

			for (int l = 0; l < techAmount; l++)
			{
				resolvedList[l] = new List<SDPRequest>();
				pendingList[l] = new List<SDPRequest>();
			}

			do
			{
				req = new SDPRequest(i);
				if (req.sdp_status == "Failed")
				{
					Console.WriteLine($"Checked request #{i} - Does not exist");
					i--;
				}
				else if (req.status != "Выполнено")
				{
					if (req.status == "Зарегистрирована" || req.status == "В ожидании")
					{
						for (int j = 0; j < techAmount; j++)
						{
							if (req.technician == userList[j].sdp_name)
							{
								pending[j]++;
								pendingList[j].Add(req);
							}
						}
					}
					//Console.WriteLine($"Checked request #{i} - Pending");
					i--;
				}
				else if (req.resolvedtime < lTime - week - 32400000) // 32400000 = 9 hours
				{
					//Console.WriteLine($"Checked request #{i} - Resolved");
					i--;
				}
				else if (req.sdp_status == "Success" && req.resolvedtime > lTime - week - 32400000)
				{
					for (int j = 0; j < techAmount; j++)
					{
						if (req.technician == userList[j].sdp_name)
						{
							made[j]++;
							resolvedList[j].Add(req);
						}
					}
					//Console.WriteLine($"Checked request #{i} - Recently resolved");
					i--;
				}
			}
			while (req.createdtime > lTime - week * 5 || req.sdp_status == "Failed");   // checking for last 5 weeks

			string namep = DateTime.Now.ToString(@"dd/MM/yyyy hh-mm") + "p.tsv";
			string name = DateTime.Now.ToString(@"dd/MM/yyyy hh-mm") + ".tsv";
			string path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\AppData\Local\Temp\SDP\Reports\";

			Directory.CreateDirectory(path);

			// Resolved requests
			using (StreamWriter sw = new StreamWriter(File.Open(path + namep, FileMode.Create), Encoding.UTF32))
			{
				sw.WriteLine("\tID\tТема\tДата создания\tДата выполнения\tПотрачено времени\tПлощадка");
				for (int t = 0; t < techAmount; t++)
				{
					sw.WriteLine(userList[t].sdp_name + " (Выполнено " + made[t] + ")\t\t\t\t\t");
					foreach (SDPRequest rq in resolvedList[t])
					{
						sw.Write("\t");
						sw.Write(rq.workorderid + "\t");
						sw.Write(rq.subject + "\t");
						sw.Write(SDPRequest.longToDateTime(rq.createdtime) + "\t");
						sw.Write(SDPRequest.longToDateTime(rq.resolvedtime) + "\t");
						sw.Write(rq.timespentonreq + "\t");
						sw.Write(rq.area + "\n");
					}
				}
				sw.Close();
			}

			// Pending requests
			using (StreamWriter sw = new StreamWriter(File.Open(path + name, FileMode.Create), Encoding.UTF32))
			{
				sw.WriteLine("\tID\tТема\tАвтор заявки\tДата создания\tПлощадка\tПриоритет\tОписание");
				for (int t = 0; t < techAmount; t++)
				{
					sw.WriteLine(userList[t].sdp_name + " (Открыто " + pending[t] + ")\t\t\t\t\t\t");
					foreach (SDPRequest rq in pendingList[t])
					{
						sw.Write("\t");
						sw.Write(rq.workorderid + "\t");
						sw.Write(rq.subject + "\t");
						sw.Write(rq.requester + "\t");
						sw.Write(SDPRequest.longToDateTime(rq.createdtime) + "\t");
						sw.Write(rq.area + "\t");
						sw.Write(rq.priority + "\t");
						sw.Write(rq.getDescForExcel(90) + "\n");
					}
				}
				sw.Close();
			}

			Console.Clear();
			Console.WriteLine("Generated report file: " + path + namep);

			Application excel = new Application();

			Workbook wb = excel.Workbooks.Open(path + name);
			Worksheet ws1 = (Worksheet)wb.Worksheets[1];
			ws1.Name = "Открытые заявки";
			ws1.Columns.AutoFit();
			Range rng = ws1.Range["A1", "A2"].EntireColumn;
			rng.Font.Bold = true;
			rng = ws1.Range["A1", "B1"].EntireRow;
			rng.Font.Bold = true;

			Workbook wb2 = excel.Workbooks.Open(path + namep);
			Worksheet ws2 = (Worksheet)wb2.Worksheets[1];
			ws2.Name = "Выполненные за неделю";
			rng = ws2.Range["A1", "A2"].EntireColumn;
			rng.Font.Bold = true;
			rng = ws2.Range["A1", "B1"].EntireRow;
			rng.Font.Bold = true;
			ws2.Columns.AutoFit();

			ws2.Copy(Before: ws1);

			wb2.Close(SaveChanges: false);
			wb.SaveAs(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\AppData\Local\Temp\SDP\Reports\report.xlsx");
			wb.Close(SaveChanges: false);

		}


	}
}
