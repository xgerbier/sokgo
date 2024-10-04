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
using System.Diagnostics;
using System.Threading;
using System.Collections;
using System.IO;
using Sokgo.IPFilter;

namespace Sokgo.Socks5
{
	class Socks5ConnectorUdp : Socks5Connector
	{
		// inner class
		protected class DataUdp
		{
			// data member(s)
			protected byte[] m_data				= new byte[HEADER_MAX_SIZE + DATA_SIZE];
			protected int m_nHeaderStart		= 0;
			protected int m_nHeaderSize			= 0;
			protected int m_nRawSize			= 0;
			protected IPEndPoint m_endp			= new IPEndPoint(IPAddress.Any, 0x0000);
			protected bool m_bWaitingDns		= false;

			// constructor(s)
			public DataUdp()
			{
			//	Reset(false);
			}

			// properties
			public byte[] Data
			{
				get { return m_data; }
			}

			public int HeaderSize
			{
				get { return m_nHeaderSize; }
				set { SetHeaderSize(value);  }
			}

			public int HeaderStart
			{
				get { return m_nHeaderStart; }
			}

			public int RawSize
			{
				get { return m_nRawSize; }
				set { m_nRawSize= value; }
			}

			public int RawStart
			{
				get { return m_nHeaderStart + m_nHeaderSize; }
			}

			public int TotalSize
			{
				get { return m_nHeaderSize + m_nRawSize; }
			}

			public IPEndPoint EndPoint
			{
				get { return m_endp; }
			}

			public AddressFamily AddressFamily
			{
				get { return m_endp.AddressFamily; }
			}

			public bool WaitingDns
			{
				get { return m_bWaitingDns; }
				set { m_bWaitingDns= value; }
			}

			// methods
			public void Reset()
			{
				Reset(false);
			}

			public void Reset(bool bReserveHeader)
			{
				m_nHeaderStart= (bReserveHeader) ? HEADER_MAX_SIZE : 0;
				m_nHeaderSize= 0;
				m_nRawSize= 0;
				m_bWaitingDns= false;
				m_endp.Address= IPAddress.Any;
				m_endp.Port= 0x0000;

				SanityCheck();
			}

			// internal methods
			private void SetHeaderSize(int nSize)
			{
				SanityCheck();

				m_nHeaderSize= (nSize < HEADER_MAX_SIZE) ? nSize : HEADER_MAX_SIZE;

				if (m_nHeaderStart > 0)
				{
					// previously reserved header size
					m_nHeaderStart= HEADER_MAX_SIZE - nSize;
				}

				SanityCheck();
			}

			private void SanityCheck()
			{
				Debug.Assert(m_nHeaderStart >= 0);
				Debug.Assert(m_nHeaderSize >= 0);
				Debug.Assert(m_nHeaderStart + m_nHeaderSize <= HEADER_MAX_SIZE);
			}

		}

		// consts
		protected const int CLIENT			= 0;	// client <-> local
		protected const int REMOTE_IPV4		= 1;	// local <-> remote (IPv4)
		protected const int REMOTE_IPV6		= 2;	// local <-> remote (IPv4)
		protected const int NB_SOCK			= 3;
		protected static readonly String[] SOCK_NAME	= { "CLIENT", "REMOTE_IPV4", "REMOTE_IPV6" };

		// data members
		protected Socket m_sockUdpAssociate;
		protected Socket[] m_sock								= new Socket[NB_SOCK];		// [0]: client -> local_client; [1]: local_remote -> remote (IPv4); [2]: local_remote -> remote (IPv6)
		protected static byte[] m_dataUdpAssociate				= new byte[DATA_SIZE];
		protected IPEndPoint m_endpClientToLocal				= new IPEndPoint(IPAddress.Any, 0x0000);	// udp port bound by client
		protected IPEndPoint m_endpLocalToClient				= new IPEndPoint(IPAddress.Any, 0x0000);	// local bound udp port to communicate with client
		protected Socks5Result m_result;
		protected DataUdp m_dataRemote							= new DataUdp();	// data from remote
		protected Socks5ConnectorState m_stateRemote			= Socks5ConnectorState.None;
		protected Socks5ConnectorUdpClientState m_stateClient	= Socks5ConnectorUdpClientState.None;
		protected LinkedList<DataUdp> m_dataClientWaitingDns	= new LinkedList<DataUdp>();
		protected Queue<DataUdp> m_dataClientReadyDns			= new Queue<DataUdp>();
		protected Object m_csDataClientDns						= new Object();		// critical section
		protected const int HEADER_IPV4_SIZE					= 10;			// SOCKS5 UDP header packet for IPv4: [0:2]Rsv; [2]Frag; [3]ATyp; [4:4]IPv4; [8:2]Port
		protected const int HEADER_IPV6_SIZE					= 22;			// SOCKS5 UDP header packet for IPv6: [0:2]Rsv; [2]Frag; [3]ATyp; [4:16]IPv6; [20:2]Port
		protected const int HEADER_DOMAIN_SIZE					= 262;			// SOCKS5 UDP header packet for IPv6: [0:2]Rsv; [2]Frag; [3]ATyp; [4:1]DomainSize; [5:255]Domain; [260:2]Port
		protected const int HEADER_MAX_SIZE						= HEADER_DOMAIN_SIZE;		// max size for a SOCKS5 UDP header packet with Domain name


		// constructor(s)
		public Socks5ConnectorUdp(Socks5Session session) : base(session)
		{
		}

		// properties
		public override Socks5ConnectorType Type
		{
			get { return Socks5ConnectorType.Udp; }
		}

		public override IPEndPoint LocalToClientEndPoint
		{
			get { return ((m_endpLocalToClient != null) ? m_endpLocalToClient : m_endpDefault); }
		}

		public override IPEndPoint LocalToRemoteEndPoint
		{
			get { return ((m_sock[REMOTE_IPV4] != null) ? (IPEndPoint)m_sock[REMOTE_IPV4].LocalEndPoint : m_endpDefault); }
		}

		public override Socket LocalToClientSocket
		{
			get { return m_sock[CLIENT]; }
		}

		public override Socket LocalToRemoteSocket
		{
			get { return m_sock[REMOTE_IPV4]; }
		}

		// methods
		public override bool BeginConnect(Socks5Session session)
		{
			m_sockUdpAssociate= session.ClientSocket;
			m_sock[REMOTE_IPV4]= null;
			m_sock[REMOTE_IPV6]= null;
			m_stateRemote= Socks5ConnectorState.Connecting;
			bool result = false;

			try
			{
				IPAddress ipaListen= Socks5Server.GetListenAddress(m_sockUdpAssociate.AddressFamily);
				IPAddress ipaLocalToClient;
				if (ipaListen == null)
				{
					m_result= Socks5Result.SocksServerFailure;
					return false;
				}

				if ((ipaListen.Equals(IPAddress.Any)) || (ipaListen.Equals(IPAddress.IPv6Any)))
					ipaLocalToClient= ((IPEndPoint)m_sockUdpAssociate.LocalEndPoint).Address;
				else
					ipaLocalToClient= ipaListen;
				Socket sockClient= new Socket(ipaListen.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
				sockClient.Bind(new IPEndPoint(ipaListen, 0x0000));
				try
				{
					// optional : keep alive, if supported
					sockClient.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
				}
				catch (SocketException) { }
				m_sock[CLIENT]= sockClient;
				m_endpLocalToClient= new IPEndPoint(ipaLocalToClient, ((IPEndPoint)sockClient.LocalEndPoint).Port);
				m_result= Socks5Result.Succeeded;
				result = true;
				Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5ConnectorUdp.Connect - Client UDP port: " + ((IPEndPoint)m_sock[CLIENT].LocalEndPoint).Port);

				// NOTE: we ignore m_endpRemote (bind address in UDPAssociate). The endpoint may be the wanted UDP port to send packets. Most of Socks5 client implementations send the default '0.0.0.0:0'.
				// In ReadFromClient() we match the UDP port used by the client. This is better to support P2P protocols.
			}
			catch (SocketException e)
			{
				Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5ConnectorUdp.Connect - Failed to open local UDP");
				Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5ConnectorUdp.Connect - Error: " + e.SocketErrorCode + " \"" + e.Message + "\"");
				m_result= Socks5Result.SocksServerFailure;
			}
			m_stateClient= Socks5ConnectorUdpClientState.Connecting;
			return result;
		}

		public override Socks5Result GetConnectResult()
		{
			return m_result;
		}

		public override bool EndConnect()
		{
			if ((m_sock[CLIENT] == null) || (m_sock[REMOTE_IPV4] != null) || (m_sock[REMOTE_IPV6] != null) || (m_result != Socks5Result.Succeeded))
				return false;

			m_stateRemote= Socks5ConnectorState.ReadWaiting;
			m_stateClient= Socks5ConnectorUdpClientState.Connected;

			return true;
		}

		public override void UpdateAwaitingReadSockets(Socks5SocketList list)
		{
			bool bWaitSockUdpAssociate= true;

			for (int i= 0; i < NB_SOCK; i++)
			{
				Socket sock= m_sock[i];
				bool bNotConnected= false;
				bool bReadWaiting= false;

				if (i == CLIENT)
				{
					// CLIENT
					bNotConnected= ((m_stateClient == Socks5ConnectorUdpClientState.None) || (m_stateClient == Socks5ConnectorUdpClientState.Connecting));
					bReadWaiting= ((sock != null) && (m_stateClient == Socks5ConnectorUdpClientState.Connected));
				}
				else
				{
					// REMOTE
					bNotConnected= ((m_stateRemote == Socks5ConnectorState.None) || (m_stateRemote == Socks5ConnectorState.Connecting));
					bReadWaiting= ((sock != null) && (m_stateRemote == Socks5ConnectorState.ReadWaiting));
				}

				if (bReadWaiting)
					list.Add(sock);

				if (bNotConnected)
					bWaitSockUdpAssociate= false;
			}

			if (bWaitSockUdpAssociate)
				list.Add(m_sockUdpAssociate);
		}

		public override void UpdateAwaitingWriteSockets(Socks5SocketList list)
		{
			if ((m_stateClient == Socks5ConnectorUdpClientState.Connecting) || (m_stateRemote == Socks5ConnectorState.Connecting))
				list.Add(m_sockUdpAssociate);

			if ((m_stateClient == Socks5ConnectorUdpClientState.Connected) && (m_dataClientReadyDns.Count > 0))
			{
				// data received from client to write to remote
				if (m_dataClientReadyDns.Peek().AddressFamily == AddressFamily.InterNetworkV6)
					list.Add(m_sock[REMOTE_IPV6]);
				else
					list.Add(m_sock[REMOTE_IPV4]);
			}

			if (m_stateRemote == Socks5ConnectorState.WriteWaiting)
				list.Add(m_sock[CLIENT]);		// data received from remote to write to client
		}

		public override bool Read(Socket sock)
		{
			if (sock == m_sockUdpAssociate)
			{
				if (sock.Available == 0)
					return false;		// closed

				sock.Receive(m_dataUdpAssociate);
				return true;
			}

			int s= Array.IndexOf(m_sock, sock);
			if (s < 0)
				return false;

			bool bResult= false;
			switch (s)
			{
				case CLIENT:
					bResult= ReadFromClient();
					break;

				case REMOTE_IPV4:
				case REMOTE_IPV6:
					bResult= ReadFromRemote(s);
					break;
			}
			return bResult;
		}

		public override bool Write(Socket sock)
		{
			int s= Array.IndexOf(m_sock, sock);
			if (s < 0)
				return false;

			bool bResult= false;
			switch (s)
			{
				case CLIENT:
					bResult= WriteToClient();
					break;

				case REMOTE_IPV4:
				case REMOTE_IPV6:
					bResult= WriteToRemote(s);
					break;
			}
			return bResult;
		}

		public override void Close()
		{
			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5ConnectorUdp.Close");

		//	m_sockUdpAssociate (== Socks5Session.m_sock[CLIENT]) closed by session

			for (int s= 0; s < NB_SOCK; s++)
			{
				Socket sock= m_sock[s];
				if (sock != null)
				{
					lock (sock)
					{
						try
						{
							sock.Close();
						}
						catch (SocketException /*e*/) { }
						m_sock[s]= null;
					}
				}
			}
		}

		// internal methods
		protected bool ReadFromClient()
		{
			DataUdp du= new DataUdp();

			EndPoint epFrom= new IPEndPoint((m_sock[CLIENT].AddressFamily == AddressFamily.InterNetworkV6) ? IPAddress.IPv6Any : IPAddress.Any, 0x0000);
			du.RawSize= m_sock[CLIENT].ReceiveFrom(du.Data, ref epFrom);

			// check coming from client, if not ignore the datagram
			if (!((IPEndPoint)epFrom).Address.Equals(((IPEndPoint)m_sockUdpAssociate.RemoteEndPoint).Address))
				return false;

			// bind UDPClient for incoming packet from remote (same UDP port from client)
			if (!BindRemote(((IPEndPoint)epFrom).Port))
				return false;

			// bind successful or already bound

			// unwrap data/request dns for destination endpoint
			if (!UnwrapDatagramFromClient(ref du))
				return false;

			lock (m_csDataClientDns)
			{
				if (du.WaitingDns)
					m_dataClientWaitingDns.AddLast(du);
				else
					m_dataClientReadyDns.Enqueue(du);
			}

			Trace.Debug("[{0}]Socks5SConnectorUdp.Read({1}); {2}({3}) bytes; {4} -> {5}", Thread.CurrentThread.GetHashCode(), GetSocketName(CLIENT), du.TotalSize, du.RawSize, epFrom, du.EndPoint);

			return (du.RawSize > 0);
		}

		protected bool ReadFromRemote(int s)
		{
			Debug.Assert((s == REMOTE_IPV4) || (s == REMOTE_IPV6));

			m_dataRemote.Reset(true);	// reserve size for future header in WriteToClient/WrapDatagramToClient

			EndPoint epFrom= new IPEndPoint((s == REMOTE_IPV6) ? IPAddress.IPv6Any : IPAddress.Any, 0x0000);
			m_dataRemote.RawSize= m_sock[s].ReceiveFrom(m_dataRemote.Data, m_dataRemote.RawStart, DATA_SIZE, SocketFlags.None, ref epFrom);
			m_dataRemote.EndPoint.Address= ((IPEndPoint)epFrom).Address;
			m_dataRemote.EndPoint.Port= ((IPEndPoint)epFrom).Port;
			m_stateRemote= Socks5ConnectorState.WriteWaiting;

			Trace.Debug("[{0}]Socks5SConnectorUdp.Read({1}); {2} bytes; {3} -> {4}", Thread.CurrentThread.GetHashCode(), GetSocketName(s), m_dataRemote.RawSize, epFrom, m_endpClientToLocal);

			return (m_dataRemote.RawSize > 0);
		}

		protected bool WriteToClient()
		{
			// write data from remote (m_dataRemote) to client

			if ( (m_dataRemote.RawSize == 0) ||
				 (m_dataRemote.EndPoint.Address.Equals(IPAddress.Any)) ||
				 (m_dataRemote.EndPoint.Address.Equals(IPAddress.IPv6Any)) ||
				 (m_dataRemote.EndPoint.Port == 0x0000) ||
				 ((m_sock[REMOTE_IPV4] == null)  && (m_sock[REMOTE_IPV6] == null))
			   )
				return false;

			WrapDatagramToClient(m_dataRemote);		// update data, start, size

			int nTotalSize= m_dataRemote.TotalSize;
			m_sock[CLIENT].SendTo(m_dataRemote.Data, m_dataRemote.HeaderStart, nTotalSize, SocketFlags.None, m_endpClientToLocal);
			m_stateRemote= Socks5ConnectorState.ReadWaiting;

			Trace.Debug("[{0}]Socks5SConnectorUdp.Write({1}); {2}({3}) bytes; {4} -> {5}", Thread.CurrentThread.GetHashCode(), GetSocketName(CLIENT), nTotalSize, m_dataRemote.RawSize, m_dataRemote.EndPoint, m_endpClientToLocal);

			m_dataRemote.Reset();

			return true;
		}

		protected bool WriteToRemote(int s)
		{
			// write all pending data from client (m_dataClientReadyDns) to remote

			Debug.Assert((s == REMOTE_IPV4) || (s == REMOTE_IPV6));

			DataUdp du= null;
			lock (m_csDataClientDns)
			{
				if (m_dataClientReadyDns.Count > 0)
					du= m_dataClientReadyDns.Dequeue();
			}

			if ( (du != null) && (du.RawSize != 0) &&
				 (du.AddressFamily == ((s == REMOTE_IPV6) ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork)) &&
				 (!IPFilterLocal.Check(du.EndPoint.Address))
			   )
			{
				m_sock[s].SendTo(du.Data, du.RawStart, du.RawSize, SocketFlags.None, du.EndPoint);
				Trace.Debug("[{0}]Socks5SConnectorUdp.Write({1}); {2} bytes; {3} -> {4}", Thread.CurrentThread.GetHashCode(), GetSocketName(s), du.RawSize, m_endpClientToLocal, du.EndPoint);
			}

			return true;
		}

		protected bool BindRemote(int nClientPort)
		{
			if ((nClientPort == 0x0000) || (m_sockUdpAssociate == null))
				return false;

			// init sock local->remote
			if (m_endpClientToLocal.Port == 0x0000)
			{
				m_endpClientToLocal.Address= ((IPEndPoint)m_sockUdpAssociate.RemoteEndPoint).Address;
				m_endpClientToLocal.Port= nClientPort;

				Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5ConnectorUdp.RecvCli - Remote UDP port: " + m_endpClientToLocal.Port);

				for (int s= REMOTE_IPV4; s <= REMOTE_IPV6; s++)
				{
					if (m_sock[s] == null)
					{
						AddressFamily af= (s == REMOTE_IPV6) ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork;
						IPAddress ip= Socks5Server.GetOutgoingIPAddress(af);
						if (ip != null)
						{
							try
							{
								// bind on same port as the client
								Socket sockRemote= new Socket(af, SocketType.Dgram, ProtocolType.Udp);
								sockRemote.Bind(new IPEndPoint(ip, m_endpClientToLocal.Port));
								m_sock[s]= sockRemote;
							}
							catch (SocketException /*e*/)
							{
								Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5ConnectorUdp.RecvCli - Failed to bind UDP port: " + m_endpClientToLocal.Port + " [" + ((af == AddressFamily.InterNetworkV6) ? "IPv6" : "IPv4") + "]");
								return false;
							}
						}
					}
				}
			}

			return true;
		}

		protected bool UnwrapDatagramFromClient(ref DataUdp du)
		{
			if (du.RawStart != 0)
			{
				du.RawSize= 0;
				return false;
			}

			// RECV: [0:2]0x0000; [2]Fragment
			if ((du.RawSize < 3) || (du.Data[0] != 0x00) || (du.Data[1] != 0x00))
			{
				du.RawSize= 0;
				return false;
			}

			// RECV: [4..]Address
			IPEndPoint ep;
			Socks5IPEndPointReader reader= new Socks5IPEndPointReader(new MemoryStream(du.Data, 3, du.RawSize - 3));
			if ((ep= reader.Read(DnsResponse, du)) == null)
				return false;

			du.EndPoint.Address= ep.Address;
			du.EndPoint.Port= ep.Port;
			int nHeaderSize= 3 + reader.ReadLength;
			du.HeaderSize= nHeaderSize;
			du.RawSize-= nHeaderSize;
			du.WaitingDns= (reader.Status == Socks5IPEndPointReader.StatusCode.WaitingDns);

			return true;
		}

		protected void WrapDatagramToClient(DataUdp du)
		{
			// build Socks5 UDP packet
			du.HeaderSize= (du.AddressFamily == AddressFamily.InterNetworkV6) ? HEADER_IPV6_SIZE : HEADER_IPV4_SIZE;
			int i= du.HeaderStart;

			// SEND: [0:2]0x0000; [2]Fragment
			du.Data[i++]= 0x00;	// RSV
			du.Data[i++]= 0x00;	// RSV
			du.Data[i++]= 0x00;	// FRAG

			Socks5IPEndPointWriter writer= new Socks5IPEndPointWriter(new MemoryStream(du.Data, du.HeaderStart + 3, du.HeaderSize - 3));
			int nAddrSize= writer.Write(du.EndPoint);
			int nHeaderSize= nAddrSize + 3;

			Debug.Assert(nHeaderSize == du.HeaderSize);
		}

		protected void DnsResponse(String strHost, IPAddress ip, Object objUserData)
		{
			Debug.Assert(objUserData is DataUdp);
			DataUdp du= (DataUdp)objUserData;

			lock (m_csDataClientDns)
			{
				if (m_dataClientWaitingDns.Contains(du))
					m_dataClientWaitingDns.Remove(du);

				if (ip != null)
				{
					du.EndPoint.Address= ip;
					m_dataClientReadyDns.Enqueue(du);
				}
			}

			// notify to group select()
			if ((m_session != null) && (m_session.Group != null))
				m_session.Group.NotifyDnsResponse();
		}

		protected String GetSocketName(int s)
		{
			if ((s < 0) || (s >= NB_SOCK))
				return "" + s;

			return SOCK_NAME[s];
		}
	}
}
