﻿#region License (GPLv3)
/*
	Copyright (C) 2011,2012,2013,2024 X.Gerbier

	This file is part of Sokgo.

	Sokgo is free software: you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	Sokgo is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with Sokgo.  If not, see <http://www.gnu.org/licenses/>.
*/
#endregion

using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;

namespace Sokgo.Socks5
{
	class Socks5ConfigSection : ConfigurationSection
	{
		[ConfigurationProperty("ListenPort", DefaultValue= "1080", IsRequired= false)]
		[IntegerValidator(MinValue=1, MaxValue=UInt16.MaxValue)]
		public int ListenPort
		{
			get { return (int)this["ListenPort"]; }
		//	set { this["ListenPort"]= value; }
		}

		[ConfigurationProperty("ListenHost", DefaultValue= "0.0.0.0", IsRequired= false)]
		public String ListenHost
		{
			get { return (String)this["ListenHost"]; }
		}

		[ConfigurationProperty("ListenHostIPv6", DefaultValue= "", IsRequired= false)]
		public String ListenHostIPv6
		{
			get { return (String)this["ListenHostIPv6"]; }
		}

		[ConfigurationProperty("OutgoingHost", DefaultValue= "", IsRequired= false)]
		public String OutgoingHost
		{
			get { return (String)this["OutgoingHost"]; }
		}

		[ConfigurationProperty("OutgoingHostIPv6", DefaultValue= "", IsRequired= false)]
		public String OutgoingHostIPv6
		{
			get { return (String)this["OutgoingHostIPv6"]; }
		}

		/*
		[ConfigurationProperty("ConnectTimeout", DefaultValue= "300", IsRequired= false)]
		[IntegerValidator(MinValue=60)]
		public int ConnectTimeout																	// sec.
		{
			get { return (int)this["ConnectTimeout"]; }
		}

		[ConfigurationProperty("ReceiveTcpTimeout", DefaultValue= "300", IsRequired= false)]
		[IntegerValidator(MinValue=60)]
		public int ReceiveTcpTimeout																// sec.
		{
			get { return (int)this["ReceiveTcpTimeout"]; }
		}

		[ConfigurationProperty("ReceiveUdpTimeout", DefaultValue= "300", IsRequired= false)]
		[IntegerValidator(MinValue=60)]
		public int ReceiveUdpTimeout																// sec.
		{
			get { return (int)this["ReceiveUdpTimeout"]; }
		}
		*/

		[ConfigurationProperty("SelectThreadCount", DefaultValue= "8", IsRequired= false)]
		[IntegerValidator(MinValue=1, MaxValue=Int32.MaxValue)]
		public int SelectThreadCount
		{
			get { return (int)this["SelectThreadCount"]; }
		}

		[ConfigurationProperty("SelectSocketMax", DefaultValue= "200", IsRequired= false)]
		[IntegerValidator(MinValue=10, MaxValue=Int32.MaxValue)]
		public int SelectSocketMax
		{
			get { return (int)this["SelectSocketMax"]; }
		}
	}
}