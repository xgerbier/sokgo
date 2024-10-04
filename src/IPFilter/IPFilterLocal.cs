#region License (GPLv3)
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
using System.Net;
using System.Net.Sockets;

namespace Sokgo.IPFilter
{
	class IPFilterLocal
	{

		// Private network		: https://en.wikipedia.org/wiki/Private_network
		// Unique local address	: https://en.wikipedia.org/wiki/Unique_local_address
		// Link-local address	: https://en.wikipedia.org/wiki/Link-local_address
		public static bool Check(IPAddress ip)
		{
			byte[] ipData= ip.GetAddressBytes();

			if (ip.AddressFamily == AddressFamily.InterNetworkV6)
			{
				// Internet Protocol Version 6 Address Space: http://www.iana.org/assignments/ipv6-address-space/ipv6-address-space.xml

				// IPv6: ::1
				if (ipData.Length >= 16)
				{
					byte n120= 0;
					for (int i= 0; i < 15; i++)
						n120|= ipData[i];
					if ((n120 == 0x00) && (ipData[15] == 0x01))
						return true;
				}

				// IPv6: fc00::/7, fe80::/10, ff00::/8
				if ( (ipData.Length >= 2) &&
					 ( ((ipData[0] & 0xFE) == 0xFC)  ||
					   ((ipData[0] == 0xFE) && ((ipData[1] & 0xC0) == 0x80)) ||
					   (ipData[0] == 0xFF)
					 )
				   )
				{
					return true;
				}
			}
			else
			{
				// Address Allocation for Private Internets	: http://tools.ietf.org/html/rfc1918
				// Special Use IPv4 Addresses				: http://tools.ietf.org/html/rfc5735#page-6

				// IPv4: 0.0.0.0/8, 10.0.0.0/8, 127.0.0.0/8, 169.254.0.0/16, 172.16.0.0/12, 192.168.0.0/16
				if ( (ipData.Length >= 2) &&
					 ( (ipData[0] == 0) ||
					   (ipData[0] == 10) ||
					   (ipData[0] == 127) ||
					   ((ipData[0] == 169) && (ipData[1] == 254)) ||
					   ((ipData[0] == 172) && ((ipData[1] & 0xF0) == 16)) ||
					   ((ipData[0] == 192) && (ipData[1] == 168))
					 )
				   )
				{
					return true;
				}
			}

			return false;
		}
	}
}
