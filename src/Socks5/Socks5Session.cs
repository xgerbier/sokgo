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
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using Sokgo.Filter;

namespace Sokgo.Socks5
{
	class Socks5SessionList : List<Socks5Session>
	{
	}

	delegate void Socks5SessionClosedCallback(Socks5Session session);

	class Socks5Session
	{
		// consts
		protected const byte SOCKS5_PROTOCOL_VERSION	= 0x05;
		protected const int DATA_BUFFER_SIZE			= 3 * 128;
		protected const int TICK_PERIOD					= 15 * 60 * 1000;	// ms
		protected const int MAX_INACTIVITY_DURATION		= 5 * 60 * 1000;	// ms

		// data members
		protected IPEndPoint m_endpClient	= new IPEndPoint(IPAddress.Any, 0x000);
		protected IPEndPoint m_endpRemote	= new IPEndPoint(IPAddress.Any, 0x000);
		protected IPEndPoint m_endpLocal	= new IPEndPoint(IPAddress.Any, 0x000);
		protected Socks5SessionState m_state= Socks5SessionState.Unconnected;
		protected Socks5SessionDataState m_dataStateClient= Socks5SessionDataState.None;
		protected Socks5SessionDataState m_dataStateRemote= Socks5SessionDataState.None;
		protected DateTime m_dtCreatedTime	= DateTime.Now;
		protected DateTime m_dtLastActivity	= DateTime.Now;
		protected Socket m_sockClient		= null;
		protected Socks5Connector m_connector				= null;
		protected Socks5SessionClosedCallback m_cbClosed	= null;
		protected Socks5SessionGroup m_group				= null;
		protected Timer m_timer;
		protected Socks5SocketList m_sockReads	= new Socks5SocketList(3);
		protected Socks5SocketList m_sockWrites	= new Socks5SocketList(2);

		protected int m_nRequestSize		= 0;
		protected byte[] m_data				= new byte[DATA_BUFFER_SIZE];
		protected int m_nMethodCount;
		protected Socks5Method m_method;
		protected Socks5Result m_result;

		//constructor(s)
		public Socks5Session(Socket sockClient, Socks5SessionGroup group)
		{
			if (sockClient == null)
				throw new ArgumentNullException();

			m_sockClient= sockClient;
			m_endpClient= (IPEndPoint)sockClient.RemoteEndPoint;
			m_endpLocal= (IPEndPoint)sockClient.LocalEndPoint;
			m_timer= new Timer(Tick, null, TICK_PERIOD, TICK_PERIOD);
			m_group= group;
		}

		// properties
		public Socket ClientSocket
		{
			get { return m_sockClient; }
		//	set { m_sockClient= value; }
		}

		public IPEndPoint ClientEndPoint
		{
			get { return m_endpClient; }
		//	set { m_endpClient= value; }
		}

		public IPEndPoint RemoteEndPoint
		{
			get { return m_endpRemote; }
		//	set { m_endpRemote= value; }
		}

		public IPEndPoint LocalEndPoint
		{
			get { return m_endpLocal; }
		//	set { m_endpLocal= value; }
		}

		public Socks5SessionState State
		{
			get { return m_state; }
		//	set { m_state= value; }
		}

		public DateTime CreatedTime
		{
			get { return m_dtCreatedTime; }
		}

		public Socks5SessionGroup Group
		{
			get { return m_group; }
		}

		// method(s)
		public bool Start(Socks5SessionClosedCallback cbClose)
		{
			if ((m_sockClient == null) || (m_state != Socks5SessionState.Unconnected))
				return false;

			m_cbClosed= cbClose;

			DoSession(new EventInit());
			return true;
		}

		public void Stop()
		{
			lock (this)
			{
				_Stop();
			}
		}

		public Socks5SocketList GetAwaitingReadSockets()
		{
			m_sockReads.Clear();

			switch (m_state)
			{
				case Socks5SessionState.OpenWaiting:
				case Socks5SessionState.RequestWaiting:
					if (m_sockClient != null)
						m_sockReads.Add(m_sockClient);
					break;

				case Socks5SessionState.Connected:
					if (m_connector != null)
						m_connector.UpdateAwaitingReadSockets(m_sockReads);
					break;
			}

			return m_sockReads;
		}

		public Socks5SocketList GetAwaitingWriteSockets()
		{
			m_sockWrites.Clear();

			switch (m_state)
			{
				case Socks5SessionState.OpenAck:
				case Socks5SessionState.RequestAck:
					if (m_sockClient != null)
						m_sockWrites.Add(m_sockClient);
					break;

				case Socks5SessionState.RequestConnect:
				case Socks5SessionState.Connected:
					if (m_connector != null)
						m_connector.UpdateAwaitingWriteSockets(m_sockWrites);
					break;
			}

			return m_sockWrites;
		}

		public void SocketReadReady(Socket sock)
		{
			DoSession(new EventSocketReadReady(sock));
		}

		public void SocketWriteReady(Socket sock)
		{
			DoSession(new EventSocketWriteReady(sock));
		}

		public void SocketFailure(Socket sock)
		{
		//	int nLastError= (int)sock.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error);
			DoSession(new EventSocketFailure(sock));
		}

		// internal method(s)

		protected void DnsResponse(String strHostName, IPAddress ip, Object objUserData)
		{
			DoSession(new EventDnsResponse(strHostName, ip));

			// notify to group select()
			if (m_group != null)
				m_group.NotifyDnsResponse();
		}

		protected bool DoSession(Event ev)
		{
			lock (this)
			{
				try
				{
					if (_Run(ev))
						return true;
				}
				catch (Exception /*e*/)
				{
				//	Trace.WriteLine(e.ToString());
				}

				_Stop();
			}

			return false;
		}

		protected void _Stop()
		{
			if (m_state == Socks5SessionState.Closed)
				return;

			m_state= Socks5SessionState.CloseWaiting;
			Close();
			m_state= Socks5SessionState.Closed;

			// notify
			if (m_cbClosed != null)
			{
				m_cbClosed(this);
				m_cbClosed= null;
			}
		}

		protected bool _Run(Event ev)
		{
			bool bResult= false;

			switch (m_state)
			{
				case Socks5SessionState.Unconnected:
					bResult= _Run_Unconnected(ev);
					break;

				case Socks5SessionState.OpenWaiting:
					bResult= _Run_OpenWait(ev);
					break;

				case Socks5SessionState.OpenAck:
					bResult= _Run_OpenAck(ev);
					break;

				case Socks5SessionState.RequestWaiting:
					bResult= _Run_RequestWait(ev);
					break;

				case Socks5SessionState.RequestWaitingDns:
					bResult= _Run_RequestWaitingDns(ev);
					break;

				case Socks5SessionState.RequestConnect:
					bResult= _Run_RequestConnect(ev);
					break;

				case Socks5SessionState.RequestAck:
					bResult= _Run_RequestAck(ev);
					break;

				case Socks5SessionState.Connected:
					bResult= _Run_Connected(ev);
					break;
			}

			return bResult;
		}

		protected bool _Run_Unconnected(Event ev)
		{
			if ( (m_state != Socks5SessionState.Unconnected) ||
				 (m_sockClient == null) ||
				 (ev.Type != EventType.Init)
			   )
				return false;

			m_nRequestSize= 0;
			m_state= Socks5SessionState.OpenWaiting;
			return true;
		}

		protected bool _Run_OpenWait(Event ev)
		{
			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5Session._Run_OpenWait");

			if ( (m_state != Socks5SessionState.OpenWaiting) ||
				 (m_sockClient == null) ||
				 (ev.Type != EventType.SocketReadReady) ||
				 (((EventSocketReady)ev).Socket != m_sockClient) ||
				 (m_sockClient.Available == 0) ||
				 (DATA_BUFFER_SIZE - m_nRequestSize <= 0)
			   )
				return false;

			Trace.Debug("available: " + m_sockClient.Available);

			// RECV: [0]Protocol version; [1]Method count; [2..]Methods
			NetworkStream stream= new NetworkStream(m_sockClient, false);
			int nReadSize= stream.Read(m_data, m_nRequestSize, DATA_BUFFER_SIZE - m_nRequestSize);
			m_nRequestSize+= nReadSize;
			if (m_nRequestSize < 2)
				return true;	// incomplete request buffer; wait remaining data

			// RECV: [0]Protocol version
			byte nVer= m_data[0];
			if (nVer != SOCKS5_PROTOCOL_VERSION)
				return false;

			// RECV: [1]Method count
			m_nMethodCount= m_data[1];
			if ((m_nMethodCount < 0) || (m_nRequestSize < 2 + m_nMethodCount))
				return true;	// incomplete request buffer; wait remaining data

			m_state= Socks5SessionState.OpenReceived;

			// RECV: [2..]Methods
			m_method= ChooseMethod(m_data, 2, m_nMethodCount);

			m_state= Socks5SessionState.OpenAck;
			return true;
		}

		protected bool _Run_OpenAck(Event ev)
		{
			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5Session._Run_OpenAck");

			if ( (m_state != Socks5SessionState.OpenAck) ||
				 (m_sockClient == null) ||
				 (ev.Type != EventType.SocketWriteReady) ||
				 (((EventSocketReady)ev).Socket != m_sockClient)
			   )
				return false;

			// SEND: [0]Protocol version; [1]Method response
			m_data[0]= SOCKS5_PROTOCOL_VERSION;
			m_data[1]= (byte)m_method;
			NetworkStream stream= new NetworkStream(m_sockClient, false);
			stream.Write(m_data, 0, 2);

			if (m_method == Socks5Method.NotAcceptable)
				return false;

			m_nRequestSize= 0;
			m_state= Socks5SessionState.RequestWaiting;
			return true;
		}

		protected bool _Run_RequestWait(Event ev)
		{
			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5Session._Run_RequestWait");

			if ( (m_state != Socks5SessionState.RequestWaiting) ||
				 (m_sockClient == null) ||
				 (ev.Type != EventType.SocketReadReady) ||
				 (((EventSocketReady)ev).Socket != m_sockClient) ||
				 (m_sockClient.Available == 0) ||
				 (DATA_BUFFER_SIZE - m_nRequestSize <= 0)
			   )
				return false;

			// RECV: [0]Protocol version; [1]Command; [2]Reserved; [3]Address type; [4..]Address; [..]Port
			NetworkStream stream= new NetworkStream(m_sockClient, false);
			int nReadSize= stream.Read(m_data, m_nRequestSize, DATA_BUFFER_SIZE - m_nRequestSize);
			m_nRequestSize+= nReadSize;
			if (nReadSize < 3)
				return true;	// incomplete request buffer; wait remaining data

			bool bFailed= false;

			// RECV: [0]Protocol version; [1]Command; [2]Reserved
			Socks5Command cmd= (Socks5Command)m_data[1];
			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5Session._DoSession - Cmd=\"" + cmd.ToString() + "\"");

			m_result= Socks5Result.Succeeded;
			if (m_data[0] != SOCKS5_PROTOCOL_VERSION)
			{
				bFailed= true;
				m_result= Socks5Result.SocksServerFailure;
			}
			else if (!Enum.IsDefined(typeof(Socks5Command), cmd))
			{
				bFailed= true;
				m_result= Socks5Result.CommandNotSupported;
			}

			// RECV: [3]Address type; [4..]Address; [..]Port
			Socks5IPEndPointReader endpReader= new Socks5IPEndPointReader(new MemoryStream(m_data, 3, m_nRequestSize - 3));
			if ((!bFailed) && ((m_endpRemote= endpReader.Read(DnsResponse)) == null))
			{
				switch (endpReader.Status)
				{
					case Socks5IPEndPointReader.StatusCode.InvalidIPv4:
					case Socks5IPEndPointReader.StatusCode.InvalidIPv6:
					case Socks5IPEndPointReader.StatusCode.InvalidDomain:
					case Socks5IPEndPointReader.StatusCode.InvalidPort:
						return true;	// incomplete request buffer; wait remaining data

					case Socks5IPEndPointReader.StatusCode.InvalidAddressType:
						bFailed= true;
						m_result= Socks5Result.AddressTypeNotSupported;
						break;

					default:
						bFailed= true;
						m_result= Socks5Result.SocksServerFailure;
						break;
				}
			}

			m_state= Socks5SessionState.RequestReceived;

			if ((!bFailed) && (endpReader.Status == Socks5IPEndPointReader.StatusCode.OK) &&
				(cmd == Socks5Command.Connect) && (!IPFilter.IsAllowed(m_endpRemote.Address)))
			{
				// local ip request (no dns)
				bFailed= true;
				m_result= Socks5Result.NotAllowed;
			}

			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5Session._DoSession - EndPoint Remote=" + (endpReader?.ToString() ?? "(null)"));
			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5Session._DoSession - EndPoint Client=" + m_sockClient.RemoteEndPoint);
			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5Session._DoSession - EndPoint Local=" + m_sockClient.LocalEndPoint);

			if (bFailed)
			{
				m_state= Socks5SessionState.RequestAck;
				return true;
			}

			// Init connector
			if ((m_connector= Socks5ConnectorFactory.Create(cmd, this)) == null)
			{
				m_result= Socks5Result.CommandNotSupported;
				m_state= Socks5SessionState.RequestAck;
				return true;
			}

			if (endpReader.Status == Socks5IPEndPointReader.StatusCode.WaitingDns)
			{
				m_state= Socks5SessionState.RequestWaitingDns;
				return true;
			}

			if (!m_connector.BeginConnect(this))
			{
				m_result= m_connector.GetConnectResult();
				m_state= Socks5SessionState.RequestAck;
				return true;
			}

			m_state= Socks5SessionState.RequestConnect;
			return true;
		}

		protected bool _Run_RequestWaitingDns(Event ev)
		{
			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5Session._Run_RequestWaitingDns");

			if ( (m_state != Socks5SessionState.RequestWaitingDns) ||
				 (m_sockClient == null) ||
				 (m_connector == null) ||
				 (ev.Type != EventType.DnsResponse)
			   )
				return false;

			EventDnsResponse _ev= (EventDnsResponse)ev;
			if (_ev.Ip == null)
			{
				// Dns failed
				m_result= Socks5Result.HostUnreachable;
				m_state= Socks5SessionState.RequestAck;
				return true;
			}

			if (!IPFilter.IsAllowed(_ev.Ip))
			{
				// local ip request (localhost, etc)
				m_result= Socks5Result.NotAllowed;
				m_state= Socks5SessionState.RequestAck;
				return true;
			}

			m_endpRemote.Address= _ev.Ip;
			if (!m_connector.BeginConnect(this))
			{
				m_result= m_connector.GetConnectResult();
				m_state= Socks5SessionState.RequestAck;
				return true;
			}

			m_state= Socks5SessionState.RequestConnect;
			return true;
		}

		protected bool _Run_RequestConnect(Event ev)
		{
			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5Session._Run_RequestConnect");

			if ( (m_state != Socks5SessionState.RequestConnect) ||
				 (m_sockClient == null) ||
				 (m_connector == null) ||
				 ((ev.Type != EventType.SocketWriteReady) && (ev.Type != EventType.SocketFailure)) ||
				 ((m_connector.Type == Socks5ConnectorType.Tcp) && (((EventSocketReady)ev).Socket != m_connector.LocalToRemoteSocket)) ||
				 ((m_connector.Type == Socks5ConnectorType.Udp) && (((EventSocketReady)ev).Socket != m_sockClient))
			   )
				return false;

			m_result= m_connector.GetConnectResult();
			m_state= Socks5SessionState.RequestAck;

		//	Trace.WriteLine("[" + Thread.CurrentThread.GetHashCode() + "] Result=" + m_result);
			return true;
		}

		protected bool _Run_RequestAck(Event ev)
		{
			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5Session._Run_RequestAck");

			if ( (m_state != Socks5SessionState.RequestAck) ||
				 (m_sockClient == null) ||
				 (ev.Type != EventType.SocketWriteReady) ||
				 (((EventSocketReady)ev).Socket != m_sockClient)
			   )
				return false;

			IPEndPoint endpLocal;
			IPEndPoint endpPublic;
			if (m_connector != null)
			{
				endpLocal= m_connector.LocalToClientEndPoint;
				endpPublic= m_connector.PublicLocalToClientEndPoint;
			}
			else
			{
				endpLocal= endpPublic= new IPEndPoint(IPAddress.Any, 0x0000);
			}

			NetworkStream stream= new NetworkStream(m_sockClient, false);

			// SEND: [0]Protocol version; [1]Result; [2]Reserved
			m_data[0]= SOCKS5_PROTOCOL_VERSION;
			m_data[1]= (byte)m_result;
			m_data[2]= (byte)0;

			// SEND: [3]Address type; [4..]Address; [..]Port
			Socks5IPEndPointWriter endpWriter= new Socks5IPEndPointWriter(new MemoryStream(m_data, 3, Socks5IPEndPointWriter.MAX_SIZE));
			int nAddrLen= endpWriter.Write(endpPublic);

			Trace.Debug ("[{0}] Result \"{1}\" to {2} - local bind {3} - public bind {4}", Thread.CurrentThread.GetHashCode(), m_result, m_sockClient.RemoteEndPoint, endpLocal, endpPublic);

			stream.Write(m_data, 0, nAddrLen + 3);
			if ((m_result != Socks5Result.Succeeded) || (m_connector == null) || (!m_connector.EndConnect()))
				return false;

			m_state= Socks5SessionState.Connected;

			return true;
		}

		protected bool _Run_Connected(Event ev)
		{
			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5Session._Run_Connected");

			if ( (m_state != Socks5SessionState.Connected) ||
				 (m_sockClient == null) ||
				 ( (ev.Type != EventType.SocketReadReady) &&
				   (ev.Type != EventType.SocketWriteReady)
				 )
			   )
				return false;

			bool bResult= false;
			switch (ev.Type)
			{
				case EventType.SocketReadReady:
					bResult= _Run_ReadWait(ev);
					break;

				case EventType.SocketWriteReady:
					bResult= _Run_WriteAck(ev);
					break;
			}

			return bResult;
		}

		protected bool _Run_ReadWait(Event ev)
		{
			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5Session._Run_ReadWait");

			if ( (ev.Type != EventType.SocketReadReady) ||
				 (m_connector == null)
			   )
				return false;

			bool bResult= m_connector.Read(((EventSocketReady)ev).Socket);

			m_dtLastActivity= DateTime.Now;
			return bResult;
		}

		protected bool _Run_WriteAck(Event ev)
		{
			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5Session._Run_WriteAck");

			if ( (ev.Type != EventType.SocketWriteReady) ||
				 (m_connector == null) )
				return false;

			return m_connector.Write(((EventSocketReady)ev).Socket);
		}

		protected bool DataFromClientWaiting(Event ev, out bool bEndData)
		{
			bEndData= false;
			if (ev.Type != EventType.ConnectorReadReady)
				return false;

			return true;
		}

		protected void Tick(Object o)
		{
			TimeSpan dt= DateTime.Now - m_dtLastActivity;
			if (dt.TotalMilliseconds >= MAX_INACTIVITY_DURATION)
				Stop();
		}

		protected void SetDataState(Socks5ConnectorDataSource src, Socks5SessionDataState state)
		{
			switch (src)
			{
				case Socks5ConnectorDataSource.FromClient:
					m_dataStateClient= state;
					break;

				case Socks5ConnectorDataSource.FromRemote:
					m_dataStateRemote= state;
					break;
			}
		}

		protected Socks5SessionDataState GetDataState(Socks5ConnectorDataSource src)
		{
			switch (src)
			{
				case Socks5ConnectorDataSource.FromClient:
					return m_dataStateClient;

				case Socks5ConnectorDataSource.FromRemote:
					return m_dataStateRemote;
			}

			return Socks5SessionDataState.None;
		}

		protected Socks5Method ChooseMethod(byte[] nMethods, int nOffset, int nLength)
		{
			for (int i= nOffset; i < nOffset + nLength; i++)
			{
				if (nMethods[i] == (byte)Socks5Method.NoAuthRequired)
					return Socks5Method.NoAuthRequired;
			}
			return Socks5Method.NotAcceptable;
		}

		protected void Close()
		{
			if (m_timer != null)
			{
				m_timer.Dispose();
				m_timer= null;
			}

			if (m_connector != null)
			{
				m_connector.Close();
				m_connector= null;
			}

			if (m_sockClient != null)
			{
				m_sockClient.Close();
				m_sockClient= null;
			}
		}

		// inner class(es)/struct(s)
		protected enum EventType
		{
			Init,
			Connect,
			SocketReadReady,
			SocketWriteReady,
			SocketFailure,
			DnsResponse,

			// TO DELETE
			SocketEndRead,
			SocketEndWrite,
			ConnectorReadReady,
			ConnectorWriteAck,
		}

		protected abstract class Event
		{
			// data members
			protected EventType m_nType;

			// constructor(s)
			protected Event(EventType nType)
			{
				m_nType= nType;
			}

			// properties
			public EventType Type
			{
				get { return m_nType; }
			}
		}

		protected class EventInit : Event
		{
			// constructor(s)
			public EventInit() : base(EventType.Init)
			{
			}
		}

		protected class EventConnect : Event
		{
			// data members
			protected Socks5Result m_nResult;

			// constructor(s)
			public EventConnect(Socks5Result nResult) : base(EventType.Connect)
			{
				m_nResult= nResult;
			}

			// properties
			public Socks5Result Result
			{
				get { return m_nResult; }
			}
		}

		protected abstract class EventSocketReady : Event
		{
			// data members
			protected Socket m_sock;

			// constructor(s)
			protected EventSocketReady(EventType nType, Socket sock) : base(nType)
			{
				m_sock= sock;
			}

			// properties
			public Socket Socket
			{
				get { return m_sock; }
			}
		}

		protected class EventSocketReadReady : EventSocketReady
		{
			// data members

			// constructor(s)
			public EventSocketReadReady(Socket sock) : base(EventType.SocketReadReady, sock)
			{
			}
		}

		protected class EventSocketWriteReady : EventSocketReady
		{
			// data members

			// constructor(s)
			public EventSocketWriteReady(Socket sock) : base(EventType.SocketWriteReady, sock)
			{
			}
		}

		protected class EventSocketFailure : EventSocketReady
		{
			// data members

			// constructor(s)
			public EventSocketFailure(Socket sock) : base(EventType.SocketFailure, sock)
			{
			}
		}

		protected class EventDnsResponse : Event
		{
			// data members
			protected String m_strHostName;
			protected IPAddress m_ip;

			// constructor(s)
			public EventDnsResponse(String strHostName, IPAddress ip) : base(EventType.DnsResponse)
			{
				m_strHostName= strHostName;
				m_ip= ip;
			}

			// properties
			public String HostName
			{
				get { return m_strHostName; }
			}

			public IPAddress Ip
			{
				get { return m_ip; }
			}
		}

	}
}
