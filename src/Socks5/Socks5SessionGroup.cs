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
using System.Net.Sockets;
using System.Net;
using System.Diagnostics;
using System.Threading;
using System.Linq;

namespace Sokgo.Socks5
{
	class Socks5SessionGroup
	{
		// consts
		protected const int SELECT_TRIM_PERIOD		= 30 * 1000;		// ms
		protected const int SELECT_ERROR_PERIOD		= 10 * 60 * 1000;	// minutes
		protected const int SELECT_MIN_CAPACITY		= 100;
		protected const int SELECT_ERROR_SLEEP		= 200;				// ms

		// data members
		protected static int m_nCountId	= 0;
		protected int m_nId;
		protected Socks5SessionList m_sessions							= new Socks5SessionList();
		protected Socks5SocketList m_sockSelectReads					= new Socks5SocketList(SELECT_MIN_CAPACITY);
		protected Socks5SocketList m_sockSelectWrites					= new Socks5SocketList(SELECT_MIN_CAPACITY);
		protected Socks5SocketList m_sockSelectErrors					= new Socks5SocketList(SELECT_MIN_CAPACITY);
		protected Dictionary<Socket, Socks5Session> m_mapSockSession	= new Dictionary<Socket,Socks5Session>(SELECT_MIN_CAPACITY);
		protected DateTime m_dtTrimSelect								= DateTime.Now;
		protected DateTime m_dtLastError								= DateTime.Now;
		protected Thread m_thread				= null;
		protected bool m_bThreadRunning			= false;
		protected Socket m_sockRecv				= null;					// 2 local sockets send/recv to internally interrupt select() when an external event occurs (ex: dns)
		protected Socket m_sockSend				= null;					// or a session status change (ex: the socket lists awaiting read/write change)
		protected byte[] m_dataSendSelect		= new byte[] { };
		protected byte[] m_dataRecvSelect		= new byte[16];
		protected EndPoint m_epRecvFrom			= new IPEndPoint(IPAddress.Any, 0);
		protected int m_nSockSelectReadCount	= 0;
		protected int m_nSockSelectWriteCount	= 0;
		protected int m_nSockSelectErrorCount	= 0;
		protected SocketError m_nLastError		= SocketError.Success;

		// constructor(s)
		public Socks5SessionGroup()
		{
			m_nId= m_nCountId++;
		}

		// properties
		public int Id
		{
			get { return m_nId; }
		}

		public Socks5SessionList Sessions
		{
			get { return m_sessions; }
		}

		// method(s)
		public void Start()
		{
			if (m_bThreadRunning)
				return;

			m_bThreadRunning= true;
			m_thread= new Thread(_Run);
			m_thread.Start();
		}

		public void Stop()
		{
			if (!m_bThreadRunning)
				return;

			lock (m_sessions)
			{
				foreach (Socks5Session session in m_sessions)
					session.Stop();
			}
			m_bThreadRunning= false;
			SendSockSelect();	// interrupt select() for closing
		}

		public void SessionStart(Socket socket)
		{
			if (!m_bThreadRunning)
				return;

			Socks5Session session= null;
			try
			{
				session= new Socks5Session(socket, this);
			}
			catch (SocketException /*e*/)
			{
				// catch System.Net.Sockets.SocketException: The socket is not connected
				return;
			}
			if ((session == null) || (!session.Start(SessionClosedCallback)))
				return;

			lock (m_sessions)
			{
				m_sessions.Add(session);
			}

			SendSockSelect();	// interrupt select() to notify socket status changed in sessions
		}

		public void SessionTrimExcess()
		{
			lock (m_sessions)
			{
				m_sessions.TrimExcess();
			}
		}

		public int GetLoad()
		{
			/*
			lock (m_sessions)
			{
				return m_sessions.Count;
			}
			*/
		//	return m_sessions.Count;

			return Math.Max(m_nSockSelectReadCount, Math.Max(m_nSockSelectWriteCount, m_nSockSelectErrorCount));
		}

		public void NotifyDnsResponse()
		{
			SendSockSelect();	// interrupt select() to notify socket status changed in sessions; force rebuild socket list for all sessions with Socks5Session.GetAwaitingReadSockets()/GetAwaitingWriteSockets()
		}

		// internal method(s)
		protected void SessionClosedCallback(Socks5Session session)
		{
			if (!m_bThreadRunning)
				return;

			lock (m_sessions)
			{
				m_sessions.Remove(session);
			}

			SendSockSelect();	// interrupt select() to notify socket status changed in sessions; force rebuild socket list for all sessions with Socks5Session.GetAwaitingReadSockets()/GetAwaitingWriteSockets()
		}

		protected void _Run()
		{
			m_sockSend= new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			m_sockRecv= new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			m_sockRecv.Bind(new IPEndPoint(IPAddress.Loopback, 0x0000));

			while (m_bThreadRunning)
			{
				m_sockSelectReads.Clear();
				m_sockSelectWrites.Clear();
				m_sockSelectErrors.Clear();
				m_mapSockSession.Clear();

				// reduce capacity periodically
				TimeSpan dt= DateTime.Now - m_dtTrimSelect;
				if (dt.TotalMilliseconds >= SELECT_TRIM_PERIOD)
				{
					m_sockSelectReads.Capacity= SELECT_MIN_CAPACITY;
					m_sockSelectWrites.Capacity= SELECT_MIN_CAPACITY;
					m_sockSelectErrors.Capacity= SELECT_MIN_CAPACITY;
					m_mapSockSession= new Dictionary<Socket,Socks5Session>(SELECT_MIN_CAPACITY);
					m_dtTrimSelect= DateTime.Now;
				}

				m_sockSelectReads.Add(m_sockRecv);

				lock (m_sessions)
				{
					foreach (Socks5Session session in m_sessions)
					{
						Socks5SocketList reads= session.GetAwaitingReadSockets();
						Socks5SocketList writes= session.GetAwaitingWriteSockets();

						if ((reads != null) && (reads.Count > 0))
						{
							m_sockSelectReads.AddRange(reads);
							m_sockSelectErrors.AddRange(reads);

							foreach (Socket sock in reads)
								m_mapSockSession[sock]= session;
						}

						if ((writes != null) && (writes.Count > 0))
						{
							m_sockSelectWrites.AddRange(writes);
							m_sockSelectErrors.AddRange(writes);

							foreach (Socket sock in writes)
								m_mapSockSession[sock]= session;
						}
					}
				}

				Trace.Debug("[{0}:{1}]Socks5SessionGroup._Run - reads={2} writes={3} sessions={4} load={5}",Thread.CurrentThread.GetHashCode(), Id, m_sockSelectReads.Count, m_sockSelectWrites.Count, m_sessions.Count, GetLoad());

				int nReads= m_sockSelectReads.Count;
				int nWrites= m_sockSelectWrites.Count;
				int nErrors= m_sockSelectErrors.Count;

				m_nSockSelectReadCount= nReads;
				m_nSockSelectWriteCount= nWrites;
				m_nSockSelectErrorCount= nErrors;

				try
				{
					Socket.Select(m_sockSelectReads, m_sockSelectWrites, m_sockSelectErrors, Int32.MaxValue);
					m_nLastError= SocketError.Success;
				}
				catch (ObjectDisposedException e)
				{
					// a socket has been disposed during Select()
					Trace.Debug("[{0}:{1}]Socks5SessionGroup._Run - ",Thread.CurrentThread.GetHashCode(), e.ToString());

					m_sockSelectReads.Clear();
					m_sockSelectWrites.Clear();
					m_sockSelectErrors.Clear();

					m_nLastError= SocketError.Success;
				}
				catch (SocketException e)
				{
					m_sockSelectReads.Clear();
					m_sockSelectWrites.Clear();
					m_sockSelectErrors.Clear();

					TimeSpan dtErr= DateTime.Now - m_dtLastError;
					if ((dtErr.TotalMilliseconds >= SELECT_ERROR_PERIOD) || (m_nLastError != e.SocketErrorCode))
					{
						String strRepeat= (m_nLastError == e.SocketErrorCode) ? String.Format(" (repeated for {0} minutes)", SELECT_ERROR_PERIOD / (60 * 1000)) : "";
						Trace.Error("ERROR: select() failed with socket error {0} ({1}) in session group {2}{3}", e.ErrorCode, e.SocketErrorCode, Id, strRepeat);
						Trace.Error(e.ToString());

						if (e.SocketErrorCode == SocketError.InvalidArgument)
						{
							// reached max socket supported by Select()
							Trace.Error("-- select([{0}], [{1}], [{2}])", nReads, nWrites, nErrors);
							Trace.Error("-- reduce value for \"SelectSocketMax\" in config file");
						}

						m_dtLastError= DateTime.Now;
						m_nLastError= e.SocketErrorCode;
					}

					Thread.Sleep(SELECT_ERROR_SLEEP);
				}

				// Linux: in some case, an erroneous socket can be present in read/write ready list AND in error list. We need to 'except' the error list

				foreach (Socket sock in m_sockSelectReads.Except(m_sockSelectErrors))
				{
					if (sock == m_sockRecv)
					{
						ReceiveSockSelect();
					}
					else
					{
						Socks5Session session;
						if (m_mapSockSession.TryGetValue(sock, out session))
							session.SocketReadReady(sock);
					}
				}

				foreach (Socket sock in m_sockSelectWrites.Except(m_sockSelectErrors))
				{
					Socks5Session session;
					if (m_mapSockSession.TryGetValue(sock, out session))
						session.SocketWriteReady(sock);
				}

				foreach (Socket sock in m_sockSelectErrors)
				{
					Socks5Session session;
					if (m_mapSockSession.TryGetValue(sock, out session))
						session.SocketFailure(sock);
				}
			}
		}

		protected bool SendSockSelect()
		{
			if ((m_sockSend == null) || (m_sockRecv == null))
				return false;

			// SendSockSelect() can be called from various threads (DnsResponse() from Dns threads, group thread)
			lock (m_sockSend)
			{
				try
				{
					m_sockSend.SendTo(m_dataSendSelect, m_sockRecv.LocalEndPoint);
				}
				catch (SocketException /*e*/)
				{
					return false;
				}
			}
			return true;
		}

		protected bool ReceiveSockSelect()
		{
			if (m_sockRecv == null)
				return false;

			try
			{
				m_sockRecv.ReceiveFrom(m_dataRecvSelect, ref m_epRecvFrom);
			}
			catch (SocketException /*e*/)
			{
				return false;
			}
			return true;
		}
	}
}