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
using System.IO;
using System.Diagnostics;

namespace Sokgo.Socks5
{
	class Socks5IPEndPointWriter
	{
		// consts
		public const int MAX_SIZE= 19;	// [0]ATyp; [1:16]IpV6; [17:2]Port

		// data members
		protected Stream m_stream;

		//constructor(s)
		public Socks5IPEndPointWriter(Stream stream)
		{
			if (stream == null)
				throw new ArgumentException();
			m_stream= stream;
		}

		// methods
		public int Write(IPEndPoint endp)
		{
			Debug.Assert(m_stream != null);
			if (endp == null)
				endp= new IPEndPoint(IPAddress.Any, 0x0000);

			// Address type & IP
			Socks5AddressType type= Socks5AddressType.IPv4;
			byte[] ip= endp.Address.GetAddressBytes();
			int nIpLen= ip.Length;
			bool bInvalidIP= false;

			switch (endp.AddressFamily)
			{
				case AddressFamily.InterNetwork:
					type= Socks5AddressType.IPv4;
					if (nIpLen != 4)
						bInvalidIP= true;
					break;

				case AddressFamily.InterNetworkV6:
					type= Socks5AddressType.IPv6;
					if (nIpLen != 16)
						bInvalidIP= true;
					break;

				default:
					bInvalidIP= true;
					break;
			}

			if (bInvalidIP)
			{
				type= Socks5AddressType.IPv4;
				ip= new byte[] { 0, 0, 0, 0 };
				nIpLen= 4;
			}

			// Port (big endian)
			int nPort= endp.Port;

			m_stream.WriteByte((byte)type);
			m_stream.Write(ip, 0, nIpLen);
			m_stream.WriteByte((byte)((nPort >> 8) & 0xFF));
			m_stream.WriteByte((byte)(nPort & 0xFF));

			return nIpLen + 3;
		}
	}
}
