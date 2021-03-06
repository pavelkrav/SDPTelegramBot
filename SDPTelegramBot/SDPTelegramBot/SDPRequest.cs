﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Xml;
using System.Configuration;
using System.IO;

namespace SDPTelegramBot
{
	class SDPRequest
	{
		public string sdp_status { get; set; }  // "Failed" if no such request; otherwise "Success"

		public int workorderid { get; set; }
		public string requester { get; set; }
		public string createdby { get; set; }
		public long createdtime { get; set; }
		public long resolvedtime { get; set; }
		public string shortdescription { get; set; }
		public string timespentonreq { get; set; }
		public string subject { get; set; }
		public string category { get; set; }
		public string subcategory { get; set; }
		public string technician { get; set; }
		public string status { get; set; }
		public string priority { get; set; }
		public string group { get; set; }
		public string description { get; set; }
		public string area { get; set; }

		public SDPRequest(int reqID)
		{
			WebClient wc = new WebClient();
			wc.Encoding = Encoding.UTF8;
			wc.Headers["Content-Type"] = "application/xml; charset=UTF-8";

			string reqStr = ConfigurationManager.AppSettings["SDP_PATH"] + "/request/" + reqID.ToString() + "?OPERATION_NAME=GET_REQUEST&TECHNICIAN_KEY=" + ConfigurationManager.AppSettings["SDP_API_KEY"];

			string xmlReqStr = null;
			try
			{
				xmlReqStr = wc.DownloadString(reqStr);
			}
			catch
			{
				xmlReqStr = "";
			}

			if (xmlReqStr.Length > 0)
			{

				try
				{
					using (XmlReader reader = XmlReader.Create(new StringReader(xmlReqStr)))
					{
						reader.ReadToFollowing("status");
						sdp_status = reader.ReadElementContentAsString();

						if (sdp_status == "Success")
						{
							reader.ReadToFollowing("value");
							workorderid = reader.ReadElementContentAsInt();
							reader.ReadToFollowing("value");
							requester = reader.ReadElementContentAsString();
							reader.ReadToFollowing("value");
							createdby = reader.ReadElementContentAsString();
							reader.ReadToFollowing("value");
							createdtime = reader.ReadElementContentAsLong();
							reader.ReadToFollowing("value");
							reader.ReadToFollowing("value");
							reader.ReadToFollowing("value");
							reader.ReadToFollowing("value");
							reader.ReadToFollowing("value");
							resolvedtime = reader.ReadElementContentAsLong();
							reader.ReadToFollowing("value");
							reader.ReadToFollowing("value");
							shortdescription = reader.ReadElementContentAsString();
							reader.ReadToFollowing("value");
							timespentonreq = reader.ReadElementContentAsString();
							reader.ReadToFollowing("value");
							subject = reader.ReadElementContentAsString();
							reader.ReadToFollowing("value");
							reader.ReadToFollowing("value");
							reader.ReadToFollowing("value");
							reader.ReadToFollowing("value");
							reader.ReadToFollowing("value");
							reader.ReadToFollowing("value");
							category = reader.ReadElementContentAsString();
							reader.ReadToFollowing("value");
							subcategory = reader.ReadElementContentAsString();
							reader.ReadToFollowing("value");
							technician = reader.ReadElementContentAsString();
							reader.ReadToFollowing("value");
							status = reader.ReadElementContentAsString();
							reader.ReadToFollowing("value");
							priority = reader.ReadElementContentAsString();
							reader.ReadToFollowing("value");
							reader.ReadToFollowing("value");
							reader.ReadToFollowing("value");
							group = reader.ReadElementContentAsString();
							reader.ReadToFollowing("value");
							description = reader.ReadElementContentAsString();
							reader.ReadToFollowing("value");
							area = reader.ReadElementContentAsString();
						}
						else
						{
							workorderid = reqID;
						}
					}
				}
				catch (Exception e)
				{
					Console.WriteLine(e.Message);
				}
			}
		}

		public static DateTime longToDateTime(long dateNumber)
		{
			long beginTicks = new DateTime(1970, 1, 1, 3, 0, 0, DateTimeKind.Utc).Ticks;
			return new DateTime(beginTicks + dateNumber * 10000);
		}

		public void consoleOutput()
		{
			if (sdp_status == "Success")
			{
				Console.WriteLine($"Request ID: {workorderid}");
				Console.WriteLine($"Requester: {requester}");
				Console.WriteLine($"Created by: {createdby}");
				Console.WriteLine($"Subject: {subject}");
				Console.WriteLine($"Category: {category}");
				Console.WriteLine($"Subcategory: {subcategory}");
				Console.WriteLine($"Short description: {shortdescription}");
				Console.WriteLine($"Technician: {technician}");
				Console.WriteLine($"Created time: {longToDateTime(createdtime)}");
				if (resolvedtime > 0)
				{
					Console.WriteLine($"Resolved time: {longToDateTime(resolvedtime)}");
					Console.WriteLine($"Time spent: {timespentonreq}");
				}
				Console.WriteLine($"Status: {status}");
				Console.WriteLine($"Priority: {priority}");
				Console.WriteLine($"Group: {group}");
				Console.WriteLine($"Area: {area}");
			}
			else
			{
				Console.WriteLine($"Request #{workorderid} does not exist.");
			}
		}

		public void consoleOutputShort()
		{
			if (sdp_status == "Success")
			{
				Console.WriteLine($"Request ID: {workorderid}");
				Console.WriteLine($"Subject: {subject}");
				Console.WriteLine($"Short description: {shortdescription}");
				Console.WriteLine($"Technician: {technician}");
				Console.WriteLine($"Created time: {longToDateTime(createdtime)}");
				if (resolvedtime > 0)
				{
					Console.WriteLine($"Resolved time: {longToDateTime(resolvedtime)}");
					Console.WriteLine($"Time spent: {timespentonreq}");
				}
				Console.WriteLine($"Status: {status}");
				Console.WriteLine($"Area: {area}");
			}
			else
			{
				Console.WriteLine($"Request #{workorderid} does not exist.");
			}
		}

		static private bool closeRequest(SDPRequest request)	// true if resolved
		{
			WebClient wc = new WebClient();
			wc.Encoding = Encoding.UTF8;
			wc.Headers["Content-Type"] = "application/xml; charset=UTF-8";
			// adding resolution
			string reqStr = ConfigurationManager.AppSettings["SDP_PATH"] + "/request/" + request.workorderid.ToString() + "/resolution" + "?OPERATION_NAME=ADD_RESOLUTION"
				+ "&INPUT_DATA=<Details><resolution><resolutiontext>Resolved via telegram bot</resolutiontext></resolution></Details>"
				+ "&TECHNICIAN_KEY=" + ConfigurationManager.AppSettings["SDP_API_KEY"];
			string xmlReqStr = null;
			xmlReqStr = wc.DownloadString(reqStr);
			// adding worklog
			reqStr = ConfigurationManager.AppSettings["SDP_PATH"] + "/request/" + request.workorderid.ToString() + "/resolution" + "?OPERATION_NAME=ADD_WORKLOG"
				+ "&INPUT_DATA=<Operation><Details><Worklogs><Worklog><technician>" + request.technician + "</technician><workMinutes>10</workMinutes><workHours>0</workHours></Worklog></Worklogs></Details></Operation>"
				+ "&TECHNICIAN_KEY=" + ConfigurationManager.AppSettings["SDP_API_KEY"];
			xmlReqStr = null;
			xmlReqStr = wc.DownloadString(reqStr);
			// changing status to Resolved
			reqStr = ConfigurationManager.AppSettings["SDP_PATH"] + "/request/" + request.workorderid.ToString() + "?OPERATION_NAME=EDIT_REQUEST"
				+ "&INPUT_DATA=<Operation><Details><status>Выполнено</status><workminutes>10</workminutes></Details></Operation>"
				+ "&TECHNICIAN_KEY=" + ConfigurationManager.AppSettings["SDP_API_KEY"];
			xmlReqStr = null;
			xmlReqStr = wc.DownloadString(reqStr);

			SDPRequest edited_req = new SDPRequest(request.workorderid);		// scanning xmlReqStr for status node would be faster
			if (edited_req.status == "Выполнено")
			{
				return true;
			}
			else return false;
		}

		static public string convertFromHTML(string text)
		{
			if (String.IsNullOrWhiteSpace(text))
				return text;
			string temp = text;
			temp = temp.Replace("\n", " ");
			// special symbols
			temp = temp.Replace("&quot;", "\"");
			temp = temp.Replace("&nbsp;", " ");
			temp = temp.Replace("&lt;", "<");
			temp = temp.Replace("&gt;", ">");
			// break row
			temp = temp.Replace("<div>", "\n");
			temp = temp.Replace("</div>", "");
			temp = temp.Replace("<p>", "\n");
			temp = temp.Replace("</p>", "");
			temp = temp.Replace("<br>", "\n");
			// other tags
			int openBr = 0;
			int closeBr = 0;
			string result = null;
			for (int i = 0; i < temp.Length; i++)
			{
				if (temp[i] == '<')
					openBr++;
				else if (temp[i] == '>')
					closeBr++;
				if (openBr == 0)
					result += temp[i];
				else if (openBr == closeBr)
				{
					openBr = 0;
					closeBr = 0;
				}
			}

			return result;
		}

		static public void handleBurovRequest(int id)
		{
			WebClient wc = new WebClient();
			wc.Encoding = Encoding.UTF8;
			wc.Headers["Content-Type"] = "application/xml; charset=UTF-8";
			string reqStr = ConfigurationManager.AppSettings["SDP_PATH"] + "/request/" + id.ToString() + "?OPERATION_NAME=EDIT_REQUEST"
				+ "&INPUT_DATA=<Operation><Details><group>Тех.Поддержка МинТранс</group><technician>Кравцов Павел Олегович</technician><Отделения>Рождественка</Отделения><priority>Обычный</priority><category>Доступ</category><subcategory>Учетная запись</subcategory></Details></Operation>"
				+ "&TECHNICIAN_KEY=" + ConfigurationManager.AppSettings["SDP_API_KEY"];
			string xmlReqStr = null;
			xmlReqStr = wc.DownloadString(reqStr);
		}

		/// <summary>
		/// Formatting description string for excel output
		/// </summary>
		/// <param name="len">Length of line</param>
		/// <returns>String with special symbols</returns>
		public string getDescForExcel(int len)
		{
			string result = "";
			bool sw = false;
			for (int i = 0; i < shortdescription.Length; i++)
			{
				char cur = shortdescription[i];
				if (i != 0)
					if (i % len == 0)
					{
						sw = true;
					}
				if (sw && (cur == ' ' || cur == ',' || cur == ';'))
				{
					result += "\x000A\t\t\t\t\t\t\t";
					sw = false;
				}
				result += cur;
			}
			return result;
		}
	}
}
