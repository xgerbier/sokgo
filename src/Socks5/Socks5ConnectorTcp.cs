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

namespace Sokgo.Socks5
{
	class Socks5ConnectorTcp : Socks5Connector
	{
		// consts
		protected const int CLIENT		= 0;	// client <-> local
		protected const int REMOTE		= 1;	// local <-> remote
		protected const int NB_SOCK		= 2;
		protected static readonly String[] SOCK_NAME	= { "CLIENT", "REMOTE" };

		// data members
		protected Socket[] m_sock					= new Socket[NB_SOCK];		// [0]: client -> local_client; [1]: local_remote -> remote
		protected Socks5Result? m_connectResult		= null;
		protected Socks5ConnectorState[] m_state	= { Socks5ConnectorState.None, Socks5ConnectorState.None } ;
		protected byte[][] m_data					= { new byte[DATA_SIZE], new byte[DATA_SIZE] };
		protected int[] m_nDataSize					= new int[NB_SOCK];
		protected IPEndPoint[] m_endp				= new IPEndPoint[NB_SOCK];

		// constructor(s)
		public Socks5ConnectorTcp(Socks5Session session) : base(session)
		{
		}

		// properties
		public override Socks5ConnectorType Type
		{
			get { return Socks5ConnectorType.Tcp; }
		}

		public override IPEndPoint LocalToClientEndPoint
		{
			get { return ((m_endp[CLIENT] != null) ? m_endp[CLIENT] : m_endpDefault); }
		}

		public override IPEndPoint LocalToRemoteEndPoint
		{
			get { return ((m_endp[REMOTE] != null) ? m_endp[REMOTE] : m_endpDefault); }
		}

		public override Socket LocalToClientSocket
		{
			get { return m_sock[CLIENT]; }
		}

		public override Socket LocalToRemoteSocket
		{
			get { return m_sock[REMOTE]; }
		}

		// methods
		public override bool BeginConnect(Socks5Session session)
		{
			Socket sockClient= (session.ClientSocket != null) ? session.ClientSocket : null;
			m_sock[CLIENT]= sockClient;
			m_endp[CLIENT]= null;
			if (sockClient != null)
			{
				IPAddress ipa= Socks5Server.GetListenAddress(sockClient.AddressFamily);
				if ((ipa == null) || (ipa.Equals(IPAddress.Any)) || (ipa.Equals(IPAddress.IPv6Any)))
					m_endp[CLIENT]= (IPEndPoint)sockClient.LocalEndPoint;
				else
					m_endp[CLIENT]= new IPEndPoint(ipa, ((IPEndPoint)sockClient.LocalEndPoint).Port);
			}
			m_state[CLIENT]= Socks5ConnectorState.Connecting;

			// open remote connection
			m_endp[REMOTE]= session.RemoteEndPoint;
			AddressFamily afRemote= m_endp[REMOTE].AddressFamily;
			Socket sockRemote= new Socket(afRemote, SocketType.Stream, ProtocolType.Tcp);
			sockRemote.Bind(new IPEndPoint(Socks5Server.GetOutgoingIPAddress(afRemote), 0x0000));
			m_sock[REMOTE]= sockRemote;
			sockRemote.Blocking= false;
			try
			{
				sockRemote.Connect(m_endp[REMOTE].Address, m_endp[REMOTE].Port);		// non-blocking
			}
			catch (SocketException e)
			{
				Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5ConnectorTcp.BeginConnect() - " + e.Message + " (" + e.SocketErrorCode + ")");
				if (e.SocketErrorCode != SocketError.WouldBlock)
				{
					m_connectResult= ToSocks5ErrorResult(e.SocketErrorCode);
					return false;
				}
			}
			m_connectResult= null;
			m_state[REMOTE]= Socks5ConnectorState.Connecting;
			return true;
		}

		public override Socks5Result GetConnectResult()
		{
			Socket sock= m_sock[REMOTE];
			// sock.Connected might be 'true' from the last socket operation, but the socket is not actually connected (ex : after a network failure, IPv6 not available); we also check m_connectResult
			if (sock.Connected && ((m_connectResult == null) || (m_connectResult == Socks5Result.Succeeded)))
				return Socks5Result.Succeeded;

			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5ConnectorTcp.Connect - Failed to open remote: " + m_endp[REMOTE]);

			Socks5Result result= Socks5Result.SocksServerFailure;
			if (m_connectResult == null)
			{
				try
				{
					int nLastError= (int)sock.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error);
					SocketError err=  (nLastError != 0) ? (SocketError)nLastError : SocketError.HostUnreachable;
					result= ToSocks5ErrorResult(err);
					Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5ConnectorTcp.Connect - Error: " + err);
				}
				catch (Exception /*e*/) {}
			}
			else
			{
				result= m_connectResult.Value;
			}

			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5ConnectorTcp.Connect - Socks5 error result: " + result);
			return result;
		}

		public override bool EndConnect()
		{
			for (int i= 0; i < NB_SOCK; i++)
			{
				Socket sock= m_sock[i];

				if ((sock == null) || (m_state[i] != Socks5ConnectorState.Connecting) || (!sock.Connected))
					return false;

				m_state[i]= Socks5ConnectorState.ReadWaiting;
			}

			return true;
		}

		public override void UpdateAwaitingReadSockets(Socks5SocketList list)
		{
			for (int i= 0; i < NB_SOCK; i++)
			{
				Socket sock= m_sock[i];
				if ((sock != null) && (m_state[i] == Socks5ConnectorState.ReadWaiting))
					list.Add(sock);
			}
		}

		public override void UpdateAwaitingWriteSockets(Socks5SocketList list)
		{
			for (int i= 0; i < NB_SOCK; i++)
			{
				Socket sock= m_sock[i];
				if (sock != null)
				{
					switch (m_state[i])
					{
						case Socks5ConnectorState.Connecting:
							if (i == REMOTE)
								list.Add(sock);
							break;

						case Socks5ConnectorState.WriteWaiting:
							list.Add((i == CLIENT) ? m_sock[REMOTE] : m_sock[CLIENT]);
							break;
					}
				}
			}
		}

		public override bool Read(Socket sock)
		{
			int s= Array.IndexOf(m_sock, sock);
			if (s < 0)
				return false;

			return Read(s);
		}

		public override bool Write(Socket sock)
		{
			int s= Array.IndexOf(m_sock, sock);
			if (s < 0)
				return false;

			return Write(s);
		}

		public override void Close()
		{
			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5ConnectorTcp.Close");

		//	m_sock[LOCAL_TO_CLIENT] (== Socks5Session.ClientSocket) closed by session

			Socket sockRemote= m_sock[REMOTE];
			if (sockRemote != null)
			{
				lock (sockRemote)
				{
					sockRemote.Close();
					m_sock[REMOTE]= null;
				}
			}
		}

		// internal methods
		protected bool Read(int s)
		{
			Debug.Assert((s == CLIENT) || (s == REMOTE));

			if (m_sock[s].Available == 0)
				return false;

			m_nDataSize[s]= m_sock[s].Receive(m_data[s]);
			m_state[s]= Socks5ConnectorState.WriteWaiting;

			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5SConnectorTcp.Read(" + GetSocketName(s) + ") " + m_nDataSize[s] + " bytes; " + m_sock[s].RemoteEndPoint);
			return (m_nDataSize[s] > 0);
		}

		protected bool Write(int s)
		{
			Debug.Assert((s == CLIENT) || (s == REMOTE));

			int sw= s;
			int sr= (s == CLIENT) ? REMOTE : CLIENT;

			m_sock[sw].Send(m_data[sr], m_nDataSize[sr], SocketFlags.None);
			m_state[sr]= Socks5ConnectorState.ReadWaiting;

			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Socks5SConnectorTcp.Write(" + GetSocketName(s) + ") " + m_nDataSize[sr] + " bytes; " + m_sock[s].RemoteEndPoint);
			m_nDataSize[sr]= 0;
			return true;
		}

		protected String GetSocketName(int s)
		{
			if ((s != CLIENT) && (s != REMOTE))
				return "" + s;

			return SOCK_NAME[s];
		}
	}
}
