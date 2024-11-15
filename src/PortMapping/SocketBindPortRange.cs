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
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;

namespace Sokgo.Port
{
	static class SocketBindPortRangeExt
	{
		// method(s)
		public static void Bind(this Socket sock, IPAddress ip, PortRange ports)
		{
			SocketBindPortRange.Bind(sock, ip, ports);
		}
	}

	class SocketBindPortRange
	{
		// method(s)
		public static void Bind(Socket sock, IPAddress ip, PortRange ports)
		{
			foreach (ushort port in ports)
			{
				if (BindSocket(sock, ip, port))
				{
					return;
				}
			}
		}

		// internal method(s)
		protected static bool BindSocket(Socket sock, IPAddress ip, ushort outgoingPort)
		{
			bool bound= false;

			try
			{
				sock.Bind(new IPEndPoint(ip, outgoingPort));
				bound= true;
			}
			catch (SocketException e)
			{
				if (e.SocketErrorCode != SocketError.AddressAlreadyInUse)
				{
					ExceptionDispatchInfo.Capture(e).Throw();
				}
			}

			return bound;
		}

	}
}