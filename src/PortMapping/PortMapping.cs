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

namespace Sokgo.Port
{
	class PortMapping : IDisposable
	{
		// consts
		protected const int MAPPING_MAX_DURATION		= 2*3600;		// seconds
		protected const int MAPPING_TICK_PERIOD			= 10*1000;		// ms

		// inner class(es)/struct(s)

		protected class OutgoingPort
		{
			public ushort Port= 0x0000;
			public DateTime LastDate= DateTime.MinValue;
			public WeakReference<Socket> SocketBound= null;

			public OutgoingPort()
			{
			}
			public OutgoingPort(ushort port)
			{
				Port= port;
			}
			public OutgoingPort(OutgoingPort op)
			{
				Port= op.Port;
				LastDate= op.LastDate;
				SocketBound= op.SocketBound;
			}
		}

		// data members

		protected PortRange m_outgoingPortRange;
		protected IDictionary<IPEndPoint, OutgoingPort> m_mappingIpV4 = new Dictionary<IPEndPoint, OutgoingPort>();	// [incomingPort]: OutgoingPort
		protected IDictionary<IPEndPoint, OutgoingPort> m_mappingIpV6 = new Dictionary<IPEndPoint, OutgoingPort>();	// [incomingPort]: OutgoingPort
		protected Thread m_thread;
		protected bool m_threadTicking= true;

		// constructor(s)

		public PortMapping(ushort outgoingPortRangeMin, ushort outgoingPortRangeMax)
		{
			m_outgoingPortRange= new PortRange(outgoingPortRangeMin, outgoingPortRangeMax);
			m_thread= new Thread(_Run);
			m_thread.Start();
		}

		// properties

		// method(s)

		public void Dispose()
		{
			m_threadTicking= false;
		}

		public bool BindOutgoingSocket(Socket sock, IPAddress ip, IPEndPoint incomingPort)
		{
			OutgoingPort outgoingPort;
			AddressFamily af= sock.AddressFamily;

			// IPv6 : first, try to map same existing IPv4 port
			if (af == AddressFamily.InterNetworkV6)
			{
				outgoingPort= FindMapping(AddressFamily.InterNetwork, incomingPort, true);
				if (BindSocket(sock, ip, incomingPort, outgoingPort))
					return true;
			}

			outgoingPort= FindMapping(af, incomingPort);
			if (BindSocket(sock, ip, incomingPort, outgoingPort))
				return true;

			if (BindSocket(sock, ip, incomingPort, m_outgoingPortRange))
				return true;

			return false;
		}

		// internal method(s)

		protected bool BindSocket(Socket sock, IPAddress ip, IPEndPoint incomingPort, OutgoingPort outgoingPort)
		{
			return (outgoingPort != null) ? BindSocket(sock, ip, incomingPort, outgoingPort.Port) : false;
		}

		protected bool BindSocket(Socket sock, IPAddress ip, IPEndPoint incomingPort, ushort outgoingPort)
		{
			bool bound= false;
			try
			{
				sock.Bind(new IPEndPoint(ip, outgoingPort));
				bound= true;
				UpdateMappingKey(incomingPort, sock);
			}
			catch (SocketException) { }
			return bound;
		}

		protected bool BindSocket(Socket sock, IPAddress ip, IPEndPoint incomingPort, PortRange ports)
		{
			bool bound= false;
			try
			{
				sock.Bind(ip, ports);
				bound= true;
				UpdateMappingKey(incomingPort, sock);
			}
			catch (SocketException) { }
			return bound;
		}

		protected IDictionary<IPEndPoint, OutgoingPort> GetFamilyMapping(AddressFamily af)
		{
			return (af == AddressFamily.InterNetworkV6) ? m_mappingIpV6 : m_mappingIpV4;
		}

		protected OutgoingPort FindMapping(AddressFamily af, IPEndPoint incomingPort, bool ignoreSocketBound= false)
		{
			return FindMapping(GetFamilyMapping(af), incomingPort, ignoreSocketBound);
		}

		protected OutgoingPort FindMapping(IDictionary<IPEndPoint, OutgoingPort> mapping, IPEndPoint incomingPort, bool ignoreSocketBound= false)
		{
			lock (mapping)
			{
				OutgoingPort port;
				if (mapping.TryGetValue(incomingPort, out port))
				{
					return new OutgoingPort(port);	// return a copy to use outside the lock
				}
				return null;
			}
		}

		protected void UpdateMappingKey(IPEndPoint incomingPort, Socket sock)
		{
			IDictionary<IPEndPoint, OutgoingPort> mapping= GetFamilyMapping(sock.AddressFamily);
			lock (mapping)
			{
				OutgoingPort port;
				if (!mapping.TryGetValue(incomingPort, out port))
				{
					port= new OutgoingPort((ushort)((IPEndPoint)sock.LocalEndPoint).Port);
					mapping.Add(incomingPort, port);
				}

				port.LastDate= DateTime.Now;
				port.SocketBound= new WeakReference<Socket>(sock);
			}
		}

		static bool IsSocketDisposed(Socket sock)
		{
			bool disposed= false;
			try
			{
				EndPoint ep= sock.LocalEndPoint;
			}
			catch (ObjectDisposedException)
			{
				disposed= true;
			}
			return disposed;
		}

		void TrimMapping(IDictionary<IPEndPoint, OutgoingPort> mapping)
		{
			lock (mapping)
			{
				IList<IPEndPoint> removeKeys= new List<IPEndPoint>(mapping.Count);

				foreach (KeyValuePair<IPEndPoint, OutgoingPort> kv in mapping)
				{
					OutgoingPort port= kv.Value;
					Socket sock;
					if ((port.SocketBound.TryGetTarget(out sock)) && (!IsSocketDisposed(sock)))
					{
						port.LastDate= DateTime.Now;
					}
					else
					{
						TimeSpan dt= DateTime.Now - port.LastDate;
						if (dt.TotalSeconds >= MAPPING_MAX_DURATION)
							removeKeys.Add(kv.Key);
					}
				}

				foreach (IPEndPoint incomingPort in removeKeys)
				{
					mapping.Remove(incomingPort);
				}
			}
		}

		protected void _Run()
		{
			Thread.Sleep(MAPPING_TICK_PERIOD);

			do
			{
				TrimMapping(m_mappingIpV4);
				TrimMapping(m_mappingIpV6);

				Thread.Sleep(MAPPING_TICK_PERIOD);

			} while (m_threadTicking);
		}
    }
}