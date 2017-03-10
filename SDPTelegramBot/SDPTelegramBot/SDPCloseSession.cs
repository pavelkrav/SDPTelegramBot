using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Configuration;
using System.IO;

namespace SDPTelegramBot
{
	class SDPCloseSession
	{
		public int time { get; set; }
		public string resolution { get; set; }
		public BotUser user { get; set; }
		public SDPRequest request { get; set; }
		public bool timeMsg { get; set; }			// true if time reminder message has been sent
		public bool resolutionMsg { get; set; }		// true if resolution reminder message has been sent

		public SDPCloseSession(BotUser user, SDPRequest request)
		{
			this.user = user;
			this.request = request;
			time = 0;
			resolution = null;
			timeMsg = false;
			resolutionMsg = false;
		}

		public bool close()
		{
			if (time > 0 && resolution != null)
			{
				WebClient wc = new WebClient();
				wc.Encoding = Encoding.UTF8;
				wc.Headers["Content-Type"] = "application/xml; charset=UTF-8";
				// adding resolution
				string reqStr = ConfigurationManager.AppSettings["SDP_PATH"] + "/request/" + request.workorderid.ToString() + "/resolution" + "?OPERATION_NAME=ADD_RESOLUTION"
					+ $"&INPUT_DATA=<Details><resolution><resolutiontext>{resolution}</resolutiontext></resolution></Details>"
					+ "&TECHNICIAN_KEY=" + ConfigurationManager.AppSettings["SDP_API_KEY"];
				string xmlReqStr = null;
				xmlReqStr = wc.DownloadString(reqStr);
				// adding worklog
				reqStr = ConfigurationManager.AppSettings["SDP_PATH"] + "/request/" + request.workorderid.ToString() + "/resolution" + "?OPERATION_NAME=ADD_WORKLOG"
					+ "&INPUT_DATA=<Operation><Details><Worklogs><Worklog><technician>" + request.technician + $"</technician><workMinutes>{time.ToString()}</workMinutes><workHours>0</workHours></Worklog></Worklogs></Details></Operation>"
					+ "&TECHNICIAN_KEY=" + ConfigurationManager.AppSettings["SDP_API_KEY"];
				xmlReqStr = null;
				xmlReqStr = wc.DownloadString(reqStr);
				// changing status to Resolved
				reqStr = ConfigurationManager.AppSettings["SDP_PATH"] + "/request/" + request.workorderid.ToString() + "?OPERATION_NAME=EDIT_REQUEST"
					+ $"&INPUT_DATA=<Operation><Details><status>Выполнено</status><workminutes>{time.ToString()}</workminutes></Details></Operation>"
					+ "&TECHNICIAN_KEY=" + ConfigurationManager.AppSettings["SDP_API_KEY"];
				xmlReqStr = null;
				xmlReqStr = wc.DownloadString(reqStr);

				user.open_requests.Remove(request);

				request = new SDPRequest(request.workorderid);        // scanning xmlReqStr for status node would be faster but I dont care
				if (request.status == "Выполнено")
				{
					return true;
				}
				else return false;
			}
			else return false;
		}
	}
}
