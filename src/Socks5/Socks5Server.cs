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

#define TRACE_SELECT

using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Diagnostics;
using System.Threading;
using System.Configuration;
using System.Collections;
using System.IO;
using System.Reflection;

namespace Sokgo.Socks5
{
	class Socks5Server
	{
		// consts
		protected const int TRIM_SESSIONS_PERIOD	= 10 * 1000;	// ms
		protected const int FORCEGC_PERIOD			= 30 * 1000;	// ms
		protected const int SELECT_MIN_CAPACITY		= 2;
		protected const int GROUP_MIN_CAPACITY		= 8;

		// data members
		protected Socket m_sockListener;
		protected Socket m_sockListenerIPv6;
	//	protected Socks5SessionList m_sessions	= new Socks5SessionList();
		protected List<Socks5SessionGroup> m_sessionGrps	= new List<Socks5SessionGroup>(GROUP_MIN_CAPACITY);
		protected Thread m_thread							= null;
		protected bool m_bRunning							= false;
		protected Timer m_timerForceGC;
		protected Timer m_timerShrinkList;
		protected static Socks5ConfigSection m_cfgSection	= null;
		protected static IPAddress m_ipListen				= null;
		protected static IPAddress m_ipV6Listen				= null;
		protected static IPAddress m_ipOutgoing				= null;
		protected static IPAddress m_ipV6Outgoing			= null;
		protected static bool m_bIpListenDone				= false;
		protected static bool m_bIpV6ListenDone				= false;
		protected static bool m_bIpOutgoingDone				= false;
		protected static bool m_bIpV6OutgoingDone			= false;
		protected Socks5SocketList m_sockSelectReads		= new Socks5SocketList(SELECT_MIN_CAPACITY);

		// constructor(s)
		public Socks5Server()
		{
			LoadConfig();
		}

		// properties
		public bool Running
		{
			get { return m_bRunning; }
		}

		static public Socks5ConfigSection Config
		{
			get { return m_cfgSection; }
		}

		// method(s)
		public bool Start()
		{
			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5Server.Start");

			if ((m_thread != null) || (m_bRunning))
				return false;

		//	ThreadPool.SetMaxThreads(2, 2);
			for (int i= 0; i < m_cfgSection.SelectThreadCount; i++)
			{
				Socks5SessionGroup grp= new Socks5SessionGroup();
				m_sessionGrps.Add(grp);
				grp.Start();
			}
			Socks5SocketList.SetMax(m_cfgSection.SelectSocketMax);

			m_bRunning= true;
			m_thread= new Thread(Run);
			m_thread.Start();

			// init timers
			m_timerForceGC= new Timer(TickForceGC, null, FORCEGC_PERIOD, FORCEGC_PERIOD);
			m_timerShrinkList= new Timer(TickShrinkList, null, TRIM_SESSIONS_PERIOD, TRIM_SESSIONS_PERIOD);

			return true;
		}

		public void Stop()
		{
			lock (this)
			{
				m_bRunning= false;

				// stop timers
				if (m_timerForceGC != null)
				{
					m_timerForceGC.Dispose();
					m_timerForceGC= null;
				}
				if (m_timerShrinkList != null)
				{
					m_timerShrinkList.Dispose();
					m_timerShrinkList= null;
				}
			}
		}

		public static IPAddress GetListenIPv4Address()
		{
			if (!m_bIpListenDone)
			{
				m_ipListen= GetIPAddress(Config.ListenHost, AddressFamily.InterNetwork);
				m_bIpListenDone= true;
			}
			return m_ipListen;
		}

		public static IPAddress GetListenIPv6Address()
		{
			if (!m_bIpV6ListenDone)
			{
				m_ipV6Listen= GetIPAddress(Config.ListenHostIPv6, AddressFamily.InterNetworkV6);
				m_bIpV6ListenDone= true;
			}
			return m_ipV6Listen;
		}

		public static IPAddress GetListenAddress(AddressFamily af)
		{
			return (af == AddressFamily.InterNetworkV6) ? GetListenIPv6Address() : GetListenIPv4Address();
		}

		public static IPAddress GetOutgoingIPv4Address()
		{
			if (!m_bIpOutgoingDone)
			{
				String strOutgoingHost= Config.OutgoingHost;
				m_ipOutgoing= ((strOutgoingHost.Length > 0) ? GetIPAddress(strOutgoingHost, AddressFamily.InterNetwork) : GetListenIPv4Address());
				m_bIpOutgoingDone= true;
			}
			return m_ipOutgoing;
		}

		public static IPAddress GetOutgoingIPv6Address()
		{
			if (!m_bIpV6OutgoingDone)
			{
				String strOutgoingHostIPv6= Config.OutgoingHostIPv6;
				m_ipV6Outgoing= ((strOutgoingHostIPv6.Length > 0) ? GetIPAddress(strOutgoingHostIPv6, AddressFamily.InterNetworkV6) : GetListenIPv6Address());
				m_bIpV6OutgoingDone= true;
			}
			return m_ipV6Outgoing;
		}

		public static IPAddress GetOutgoingIPAddress(AddressFamily af)
		{
			return (af == AddressFamily.InterNetworkV6) ? GetOutgoingIPv6Address() : GetOutgoingIPv4Address();
		}

		// internal method(s)
		protected void Run()
		{
			try
			{
				_Run();
				Stop();
			}
			catch (Exception e)
			{
				Trace.Error("ERROR: unhandled exception");
				Trace.Error(e.ToString());
				throw;
			}
		}

		protected void _Run()
		{
			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5Server.Run");

			int nPort= Config.ListenPort;

			IPAddress ipHost= GetListenIPv4Address();
			if (ipHost == null)
			{
				Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5Server.Run - Invalid host \"" + Config.ListenHost + "\"");
				Trace.Error("ERROR: no listen host defined");
				return;
			}

			IPAddress ipHostV6= GetListenIPv6Address();

			if (!Socks5Dns.Init())
			{
				Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5Server.DoStart - Dns init failed");
				Trace.Error("ERROR: dns error");
				return;
			}

			try
			{
				m_sockListener= new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				m_sockListener.Bind(new IPEndPoint(ipHost, nPort));
				m_sockListener.Listen(128);
				Trace.Log("bound to {0}:{1}", ipHost.ToString(), nPort);
			}
			catch (SocketException /*e*/)
			{
				Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5Server.DoStart - Bind error \"" + ipHost.ToString() + ":" + nPort + "\"");
				Trace.Error("ERROR: failed to bind to {0}:{1}", ipHost.ToString(), nPort);
				return;
			}

			if (ipHostV6 != null)
			{
				Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5Server.DoStart - Enabled IPv6");
				Trace.Log("enabled IPv6");
				try
				{
					m_sockListenerIPv6= new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
					// Mono : System.Net.Sockets.SocketException (0x80004005): 'Address already in use' if socket is not ipv6 only (ipv4 already created)
					#if MONO
					bool ipV6Only = ((int)m_sockListenerIPv6.GetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only) != 0) ? true : false;
					if (!ipV6Only)
					{
						#if MONO_LT_4_1

						// Mono 2.8 (mono-2.8-ubuntu; .NET 2.0-4.0) and Mono < 4.1.0 : error System.Net.Sockets.SocketOptionName 0x1b (27) is not supported at IPv6 level
						// SocketOptionName.IPv6Only not implemented (https://github.com/mono/mono/blob/2.8/mcs/class/System/System.Net.Sockets/SocketOptionName.cs)
						if (Environment.OSVersion.Platform == PlatformID.Unix)
							SokgoUnix.setsockopt(m_sockListenerIPv6, SocketOptionLevel.IPv6, SokgoUnix.IPV6_V6ONLY, true);

						#else	// !MONO_LT_4_1

						m_sockListenerIPv6.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, true);

						#endif	// !MONO_LT_4_1
					}
					#endif	// MONO

					m_sockListenerIPv6.Bind(new IPEndPoint(ipHostV6, nPort));
					m_sockListenerIPv6.Listen(128);
					Trace.Log("bound to [{0}]:{1}", ipHostV6.ToString(),nPort);
				}
				catch (SocketException e)
				{
					Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5Server.DoStart - Bind error \"[" + ipHostV6.ToString() + "]:" + nPort + "\"");
					Trace.Error("ERROR: failed to bind to [{0}]:{1} ({2})", ipHostV6.ToString(), nPort, e.ToString());
					return;
				}
			}


			while (m_bRunning)
				Select();

			// close
			foreach (Socks5SessionGroup grp in m_sessionGrps)
				grp.Stop();
			m_sockListener.Close();
			m_sockListener= null;
		//	m_sessions.Clear();
		}

		protected void Select()
		{
			m_sockSelectReads.Clear();

			m_sockSelectReads.Add(m_sockListener);
			if (m_sockListenerIPv6 != null)
				m_sockSelectReads.Add(m_sockListenerIPv6);

			Trace.Debug("[{0}]Socks5Server.Select - reads={1} writes={2}", Thread.CurrentThread.GetHashCode(), m_sockSelectReads.Count, 0);

			try
			{
				Socket.Select(m_sockSelectReads, null, null, Int32.MaxValue);
			}
			catch (ObjectDisposedException e)
			{
				// a socket has been disposed during Select()
				Trace.Debug("[{0}]Socks5Server.Select - ", Thread.CurrentThread.GetHashCode(), e.ToString());

				m_sockSelectReads.Clear();
			}
			catch (SocketException e)
			{
				Trace.Error("ERROR: select() failed with socket error {0} ({1})", e.ErrorCode, e.SocketErrorCode);
				Trace.Error(e.ToString());

				m_sockSelectReads.Clear();
			}

			foreach (Socket sock in m_sockSelectReads)
			{
				Socket sockAccept= null;

				try
				{
					sockAccept= sock.Accept();
				}
				catch (SocketException e)
				{
					Trace.Error("ERROR: accept() failed with socket error {0} ({1})", e.ErrorCode, e.SocketErrorCode);
					Trace.Error(e.ToString());
				}

				if (sockAccept != null)
					AcceptTcpClient(sockAccept);
			}

		}

		protected void AcceptTcpClient(Socket socket)
		{
			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5Server.DoAcceptTcpClient");

			Socks5SessionGroup grp= GetGroupMinLoad();
			if (grp != null)
				grp.SessionStart(socket);
		}

		protected Socks5SessionGroup GetGroupMinLoad()
		{
			if (m_sessionGrps.Count == 0)
				return null;

			Socks5SessionGroup grpMin= m_sessionGrps[0];
			int nMinLoad= grpMin.GetLoad();
			for (int i= 1; i < m_sessionGrps.Count; i++)
			{
				Socks5SessionGroup grp= m_sessionGrps[i];
				int nLoad= grp.GetLoad();
				if (nLoad < nMinLoad)
				{
					grpMin= grp;
					nMinLoad= nLoad;
				}
			}

			return grpMin;
		}

		protected void TickForceGC(Object o)
		{
			if (!m_bRunning)
				return;

			GC.Collect();
			/*
			long nVirtualMem= Process.GetCurrentProcess().VirtualMemorySize64;
			long nPrivateMem= Process.GetCurrentProcess().PrivateMemorySize64;
			long nManagedMem= GC.GetTotalMemory(true);
			Trace.WriteLine("Virtual Mem= {0:F3} Mb - Private Mem= {1:F3} Mb - Managed Mem= {2:F3} Mb", (float)nVirtualMem / (1024 * 1024), (float)nPrivateMem / (1024 * 1024), (float)nManagedMem / (1024 * 1024));
			*/
		}

		protected void TickShrinkList(Object o)
		{
			if (!m_bRunning)
				return;

			foreach (Socks5SessionGroup grp in m_sessionGrps)
				grp.SessionTrimExcess();
		}

		protected static void LoadConfig()
		{
			if (m_cfgSection != null)
				return;

			Configuration config= ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
			m_cfgSection= (Socks5ConfigSection)config.Sections.Get("Socks5");
			if (m_cfgSection == null)
				m_cfgSection= new Socks5ConfigSection();
		}

		protected static IPAddress GetIPAddress(String strHost, AddressFamily af)
		{
			IPAddress ipHost= null;
			if (strHost.Length > 0)
			{
				IPAddress ipParse;
				if (IPAddress.TryParse(strHost, out ipParse))
				{
					// IP
					if (ipParse.AddressFamily == af)
						ipHost= ipParse;
				}
				else
				{
					// Host name
					IPHostEntry ipEntries;
					try
					{
						ipEntries= Dns.GetHostEntry(strHost);
					}
					catch (SocketException /*e*/)
					{
						return null;
					}

					// retrieve first IP with wanted AddressFamily (IPv4, IPv6)
					IPAddress[] ipAddressList= ipEntries.AddressList;
					int i= 0;
					while ((i < ipAddressList.Length) && (ipHost == null))
					{
						IPAddress ipCurrent= ipAddressList[i++];
						if (ipCurrent.AddressFamily == af)
							ipHost= ipCurrent;
					}
				}
			}
			else if (af == AddressFamily.InterNetwork)
			{
				ipHost= IPAddress.Any;
			}

			return ipHost;
		}

		protected static void TraceSockets(String strPrefix, ArrayList sockets)
		{
			if (sockets.Count == 0)
			{
				Trace.Error("{0}: -", strPrefix);
				return;
			}

			int n= 0;
			foreach (Socket s in sockets)
			{
				EndPoint epLocal= null;
				EndPoint epRemote= null;
				String strLocal= "";
				String strRemote= "";
				String strStatus= "";

				String strSocketInfo= "(null)";

				if (s != null)
				{
					try
					{
						epLocal= s.LocalEndPoint;
						strLocal= epLocal.ToString();
					}
					catch (Exception /*e*/) { }
					try
					{
						epRemote= s.RemoteEndPoint;
						strRemote= epRemote.ToString();
					}
					catch (Exception /*e*/) { }

					try
					{
						int nAcceptConnection= (int)s.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.AcceptConnection);
						if (nAcceptConnection != 0)
							strStatus+= "L";
					}
					catch (Exception /*e*/) { }

					switch (s.ProtocolType)
					{
						case ProtocolType.Tcp:
							strStatus+= "T";
							break;

						case ProtocolType.Udp:
							strStatus+= "U";
							break;
					}
					if (s.AddressFamily == AddressFamily.InterNetworkV6)
						strStatus+= "6";

					if (s.IsBound)
						strStatus+= "B";
					if (s.Connected)
						strStatus+= "C";

				//	if (!String.IsNullOrEmpty(strStatus))
				//		strStatus+= " ";

					strSocketInfo= String.Format("{0} [{1}] [{2}]", strStatus, strLocal, strRemote);
				}

				Trace.Error("{0}[{1}]: {2}", strPrefix, n, strSocketInfo);
				n++;
			}
		}

	}


}
