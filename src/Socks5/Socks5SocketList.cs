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

using System.Collections.Generic;
using System.Net.Sockets;
using System;

namespace Sokgo.Socks5
{

	class Socks5SocketList : List<Socket>
	{
		// consts
		protected const int MAX_SOCKET= 1024;

		// data members
		protected static int m_nMax= MAX_SOCKET;

		// constructor(s)
		public Socks5SocketList()
		{
		}

		public Socks5SocketList(int nReserve) : base(nReserve)
		{
		}

		// methods
		public new void Add(Socket sock)	// add unique
		{
			if ((Count < m_nMax) && (sock != null) && (!Contains(sock)))
				base.Add(sock);
		}

		public void AddRange(Socks5SocketList socks)	// add uniques
		{
			foreach (Socket s in socks)
				Add(s);
		}

		public static void SetMax(int nMaxSocket)
		{
			m_nMax= Math.Max(1, Math.Min(nMaxSocket, MAX_SOCKET));
		}
	}
}
