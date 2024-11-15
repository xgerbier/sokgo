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
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Sokgo.SocketMono
{
	static class SocketMonoExt
	{

		// method(s)
		public static Socket SetSocketOptionIPv6Only_Mono(this Socket sock, bool value)
		{
			#if MONO
			if (sock.AddressFamily == AddressFamily.InterNetworkV6)
			{
				try
				{
					// Mono : System.Net.Sockets.SocketException (0x80004005): 'Address already in use' if socket is not IPv6 only and IPv4 already created
					// By default with mono, IPv6Only = false
					bool ipV6Only = ((int)sock.GetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only) != 0);
					if (!ipV6Only)
					{
							#if MONO_LT_4_1

							// Mono 2.8 (mono-2.8-ubuntu; .NET 2.0-4.0) and Mono < 4.1.0 : error System.Net.Sockets.SocketOptionName 0x1b (27) is not supported at IPv6 level
							// SocketOptionName.IPv6Only not implemented (https://github.com/mono/mono/blob/2.8/mcs/class/System/System.Net.Sockets/SocketOptionName.cs)
							if (Environment.OSVersion.Platform == PlatformID.Unix)
								SokgoUnix.setsockopt(sock, SocketOptionLevel.IPv6, SokgoUnix.IPV6_V6ONLY, true);

							#else	// !MONO_LT_4_1

							sock.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, true);

							#endif	// !MONO_LT_4_1
						}
					}
				catch (SocketException) { }
			}
			#endif	// MONO

			return sock;
		}

		public static Socket SetSocketOptionReuseAddr_Mono(this Socket sock, bool value)
		{
			#if MONO
			try
			{
				// In mono environment (6.8), socket option ReuseAddress (SO_REUSEADDR) is set to true for all created sockets; SocketError.AddressAlreadyInUse will not be thrown in Socket.Bind() if the port is already in use
				// cf. create socket in mono_w32socket_socket() : https://github.com/mono/mono/blob/mono-6.8.0.105/mono/metadata/w32socket-unix.c#L734
				// We unset SO_REUSEADDR here, before to bind the socket and ensure SocketError.AddressAlreadyInUse wil be thrown if the port is already in use.
				if ((int)sock.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress) != 0)
				{
					sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
				}

			}
			catch (SocketException)	{ }
			#endif	// MONO

			return sock;
		}

		// internal method(s)

	}
}