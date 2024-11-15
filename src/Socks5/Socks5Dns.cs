using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Threading;
using System.Diagnostics;
using System.Net.Sockets;

namespace Sokgo.Socks5
{

	public delegate void Socks5DnsResponseNotify(String strHost, IPAddress ip, Object objUser);

	class Socks5Dns
	{
		// inner class(es)/struct(s)
		protected class Entry
		{

			// data members
			protected String m_strHostName;
			protected IPAddress m_ip= null;
			protected List<ResponseNotify> m_notifies= new List<ResponseNotify>();

			// constructor(s)
			public Entry(String strHostName, ResponseNotify notify)
			{
				m_strHostName= strHostName;
				m_notifies.Add(notify);
			}

			// properties
			public String HostName
			{
				get { return m_strHostName; }
			}

			public IPAddress Ip
			{
				get { return m_ip; }
				set { m_ip= value; }
			}

			public List<ResponseNotify> Notifies
			{
				get { return m_notifies; }
			}
		}

		protected struct ResponseNotify
		{
			// properties
			public readonly Socks5DnsResponseNotify Delegate;
			public readonly Object UserData;

			// constructor(s)
			public ResponseNotify(Socks5DnsResponseNotify cb, Object objUser)
			{
				Delegate= cb;
				UserData= objUser;
			}
		}

		// consts
		protected enum LocalSocketStatus
		{
			None,
			Failed,
			Connecting,
			Connected,
		}
		protected const int MAX_LOCAL_SOCKET_WAIT	= 10 * 1000;	// ms
		protected const int MAX_RESOLVE_TIMEOUT		= 2 * 1000;		// ms
		protected const int NB_THREAD				= 4;
		protected const int THRESHOLD_WAIT_ENTRY	= 16;

		// data members
		protected static bool m_bInitialized			= false;
		protected static Thread[] m_thread				= new Thread[NB_THREAD];
		protected static AutoResetEvent m_ev			= new AutoResetEvent(false);
		protected static LocalSocketStatus m_status		= LocalSocketStatus.None;
		protected static Queue<Entry> m_waitEntries		= new Queue<Entry>();
		protected static List<Entry> m_requestEntries	= new List<Entry>();
	//	protected static Dictionary<String,Entry> m_resolvedEntries= new Dictionary<String,Entry>();

		// constructor(s)

		// properties

		// methods
		public static bool ResolveHost(String strHost, Socks5DnsResponseNotify cbNotify, Object objUser)
		{
			if ((strHost == null) || (cbNotify == null))
				return false;

			if (!m_bInitialized)
				throw new InvalidOperationException("Socks5Dns.Init() not called");

			AddEntry(new Entry(strHost, new ResponseNotify(cbNotify, objUser)));
			m_ev.Set();
			return true;
		}

		public static bool Init()
		{
			if (m_bInitialized)
				return true;

			for (int i= 0; i < NB_THREAD; i++)
			{
				m_thread[i]= new Thread(Run);
				m_thread[i].Start();
			}

			m_bInitialized= true;
			return true;
		}

		// internal methods
		protected static void Run()
		{
			while (true)
			{
				m_ev.WaitOne();

				Entry e;
				while ((e= GetNextEntry()) != null)
				{
					lock (m_requestEntries)
					{
						m_requestEntries.Add(e);
					}

					DateTime t0= DateTime.Now;

					IPAddress[] ips= null;
					try
					{
						ips= Dns.GetHostAddresses(e.HostName);
					}
					catch (Exception /*e*/) { }

					TimeSpan dt= DateTime.Now - t0;

					lock (m_requestEntries)
					{
						m_requestEntries.Remove(e);
					}

					e.Ip= null;
					bool bIpv6Enabled= (Socks5Server.GetListenIPv6Address() != null);
					if (bIpv6Enabled)
						e.Ip= GetFirstIp(ips, AddressFamily.InterNetworkV6);
					if (e.Ip == null)
						e.Ip= GetFirstIp(ips, AddressFamily.InterNetwork);
					Debug.Assert(e.Notifies != null);
					foreach (ResponseNotify notify in e.Notifies)
						notify.Delegate(e.HostName, e.Ip, notify.UserData);

				//	m_resolvedEntries.Add(e.HostName, e);

					Trace.Debug("[{0}]Socks5Dns.Run - \"{1}\" -> \"{2}\" ({3} ms; {4} notification(s))", Thread.CurrentThread.GetHashCode(), e.HostName, e.Ip, Math.Ceiling(dt.TotalMilliseconds), e.Notifies.Count);
				}
			}
		}

		protected static IPAddress GetFirstIp(IPAddress[] ips, AddressFamily af)
		{
			if (ips == null)
				return null;

			foreach (IPAddress ip in ips)
			{
				if (ip.AddressFamily == af)
					return ip;
			}

			return null;
		}

		protected static void AddEntry(Entry e)
		{
			lock (m_requestEntries)
			{
				Entry reqEntry= m_requestEntries.Find(delegate(Entry _e)
				{
					return (_e.HostName == e.HostName);
				});
				if (reqEntry != null)
				{
					reqEntry.Notifies.AddRange(e.Notifies);
					return;
				}
			}

			lock (m_waitEntries)
			{
				// already waiting request ?
				Entry waitEntry= FindWaitingEntry(e.HostName);
				if (waitEntry != null)
				{
					waitEntry.Notifies.AddRange(e.Notifies);
					return;
				}

				m_waitEntries.Enqueue(e);
			}
		}

		protected static Entry GetNextEntry()
		{
			Entry e= null;

			lock (m_waitEntries)
			{
				if (m_waitEntries.Count > 0)
				{
					e= m_waitEntries.Dequeue();
					if (m_waitEntries.Count < THRESHOLD_WAIT_ENTRY)
						m_waitEntries.TrimExcess();		// try to trim excess if needed (<90%)
				}
			}

			return e;
		}

		protected static Entry FindWaitingEntry(String strHostName)
		{
			// m_waitEntries must be locked
			foreach (Entry e in m_waitEntries)
			{
				if ((e != null) && (e.HostName == strHostName))
					return e;
			}

			return null;
		}
	}
}
