using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDPTelegramBot
{
	class BotUser
	{
		public string sdp_name { get; protected set; }
		public long tel_id { get; protected set; }
		public List<SDPRequest> open_requests { get; set; }
		public string abbreviation { get; protected set; }

		public BotUser(string sdp_name, long tel_id, string abbreviation)
		{
			this.sdp_name = sdp_name;
			this.tel_id = tel_id;
			this.open_requests = new List<SDPRequest>();
			this.abbreviation = abbreviation;
		}

		public override string ToString()
		{
			return "BotUser";
		}

	}

	class Technician : BotUser
	{
		public Technician(string sdp_name, long tel_id, string abbreviation) : base(sdp_name, tel_id, abbreviation)
		{
			
		}

		public override string ToString()
		{
			return "Technician";
		}
	}

	class Admin : BotUser
	{
		public Admin(string sdp_name, long tel_id, string abbreviation) : base(sdp_name, tel_id, abbreviation)
		{

		}

		public override string ToString()
		{
			return "Admin";
		}
	}
}
