using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Xml;
using System.Configuration;
using System.IO;
using Newtonsoft.Json;

namespace SDPTelegramBot
{
	class TELRequest
	{
		public string method { get; set; }
		public List<string> param { get; protected set; }
		public List<string> param_def { get; protected set; }

		public TELRequest(string method, List<string> param, List<string> param_def)
		{
			this.method = method;
			this.param = new List<string>();
			this.param_def = new List<string>();
			foreach (string str in param)
			{
				this.param.Add(str);
			}
			foreach (string str in param_def)
			{
				this.param_def.Add(str);
			}
		}

		public TELRequest(string method, string param, string param_def)
		{
			this.method = method;
			this.param = new List<string>();
			this.param_def = new List<string>();
			this.param.Add(param);
			this.param_def.Add(param_def);
		}

		public TELRequest(string method)
		{
			this.method = method;
			this.param = null;
			this.param_def = null;
		}

		public void pushRequest()
		{
			WebClient wc = new WebClient();
			wc.Encoding = Encoding.UTF8;

			string reqStr = ConfigurationManager.AppSettings["TEL_BOT_PATH"] + ConfigurationManager.AppSettings["TEL_API_KEY"] + "/" + method;
			if (param != null && param_def != null)
			{
				if (param.Count == param_def.Count)
				{
					reqStr += "?";
					for (int i = 0; i < param.Count; i++)
					{
						reqStr += param[i] + "=" + param_def[i];
						if (param.Count - 1 != i)
							reqStr += "&";
					}
				}
			}
			wc.DownloadData(reqStr);
		}

		public string getResponseString()
		{
			WebClient wc = new WebClient();
			wc.Encoding = Encoding.UTF8;

			string reqStr = ConfigurationManager.AppSettings["TEL_BOT_PATH"] + ConfigurationManager.AppSettings["TEL_API_KEY"] + "/" + method;
			if (param != null && param_def != null)
			{
				if (param.Count == param_def.Count)
				{
					reqStr += "?";
					for (int i = 0; i < param.Count; i++)
					{
						reqStr += param[i] + "=" + param_def[i];
						if (param.Count - 1 != i)
							reqStr += "&";
					}
				}
			}
			return wc.DownloadString(reqStr);
		}
	}

	class Method
	{
		public bool ok { get; set; }
	}

	class GetUpdates : Method
	{
		public List<GetUpdatesResult> result { get; set; }

		public override string ToString()
		{
			return "getUpdates";
		}
	}

	class GetUpdatesResult
	{
		public long update_id { get; set; }
		public Message message { get; set; }
	}

	class Message
	{
		public long message_id { get; set; }
		public User from { get; set; }
		public Chat chat { get; set; }
		public long date { get; set; }
		public string text { get; set; }
	}

	class User
	{
		public long id { get; set; }
		public string first_name { get; set; }
		public string last_name { get; set; }
		public string username { get; set; }
	}

	class Chat
	{
		public long id { get; set; }
		public string first_name { get; set; }
		public string last_name { get; set; }
		public string username { get; set; }
		public string type { get; set; }
	}
}
