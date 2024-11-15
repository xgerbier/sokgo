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
using System.Collections;
using System.Net.Sockets;

namespace Sokgo.Socks5
{
	abstract class Socks5Connector
	{
		// consts
		protected const int DATA_SIZE	= 4 * 1024;

		// data members
		protected static IPEndPoint m_endpDefault	= new IPEndPoint(IPAddress.Any, 0x0000);
		protected Socks5Session m_session;

		// constructor(s)
		protected Socks5Connector(Socks5Session session)
		{
			m_session= session;
		}

		// abstract methods/properties
		public abstract Socks5ConnectorType Type { get; }

		public abstract bool BeginConnect(Socks5Session session);
		public abstract Socks5Result GetConnectResult();
		public abstract bool EndConnect();

		public abstract void UpdateAwaitingReadSockets(Socks5SocketList list);
		public abstract void UpdateAwaitingWriteSockets(Socks5SocketList list);

		public abstract bool Read(Socket sock);
		public abstract bool Write(Socket sock);

		public abstract IPEndPoint LocalToClientEndPoint { get; }
		public abstract IPEndPoint LocalToRemoteEndPoint { get; }

		public IPEndPoint PublicLocalToClientEndPoint { get => ToPublicEndPoint(LocalToClientEndPoint); }
		public IPEndPoint PublicLocalToRemoteEndPoint { get => ToPublicEndPoint(LocalToRemoteEndPoint); }

		public abstract Socket LocalToClientSocket { get; }
		public abstract Socket LocalToRemoteSocket { get; }

		public abstract void Close();

		public Socks5Result ToSocks5ErrorResult(SocketError err)
		{
			Socks5Result result;

			switch (err)
			{
				case SocketError.NetworkUnreachable:
					result= Socks5Result.NetworkUnreachable;
					break;

				case SocketError.HostUnreachable:
				case SocketError.HostNotFound:
				case SocketError.TimedOut:
					result= Socks5Result.HostUnreachable;
					break;

				case SocketError.ConnectionRefused:
					result= Socks5Result.ConnectionRefused;
					break;

				default:
					result= Socks5Result.SocksServerFailure;
					break;
			}

			return result;
		}

		// internal methods
		protected IPEndPoint ToPublicEndPoint(IPEndPoint ipLocal)
		{
			if ((ipLocal.Address == IPAddress.Any) || (ipLocal.Address == IPAddress.IPv6Any) || (ipLocal.Port == 0x0000))
				return ipLocal;

			IPAddress ipPublicAddr= Socks5Server.GetPublicAddress(ipLocal.AddressFamily);
			bool validIpPublic= (ipPublicAddr != null) && (ipPublicAddr != IPAddress.Any) && (ipPublicAddr != IPAddress.IPv6Any);
			return (validIpPublic) ? new IPEndPoint(ipPublicAddr, ipLocal.Port) : ipLocal;
		}
	}
}
