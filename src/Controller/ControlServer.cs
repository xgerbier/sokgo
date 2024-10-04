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
using System.Net.Sockets;
using System.Net;
using System.Threading;
using Sokgo.Socks5;
using System.Reflection;

namespace Sokgo.Controller
{
	class ControlServer
	{
		// consts
		public const int PORT				= 20944;
		protected const int DATA_SIZE		= 4 * 1024;
		protected const int SEND_TIMEOUT	= 500;	// ms

		// data members
		protected Socks5Server m_socks5;
		protected Socket m_sockListen;
		protected List<Socket> m_sockClients= new List<Socket>();
		protected bool m_bInitialized	= false;
		protected bool m_bRunning		= false;
		protected byte[] m_data			= new byte[DATA_SIZE];
		protected Thread m_thread;

		// constuctor(s)
		public ControlServer(Socks5Server socks5)
		{
			m_socks5= socks5;
		}

		// methods
		public bool Start()
		{
			if (m_bInitialized)
				return true;

			try
			{
				IPEndPoint ip= new IPEndPoint(IPAddress.Loopback, PORT);
				m_sockListen= new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				m_sockListen.Bind(ip);
				m_sockListen.Listen(4);
			}
			catch (Exception /*e*/)
			{
				return false;
			}

			m_thread= new Thread(Run);
			m_thread.Start();

			m_bInitialized= true;
			return true;
		}

		public void Run()
		{
			if (m_sockListen == null)
				return;

			List<Socket> sockSelectReads= new List<Socket>();
			List<Socket> sockSelectErrors= new List<Socket>();

			while (true)
			{
				sockSelectReads.Clear();
				sockSelectErrors.Clear();

				sockSelectReads.Add(m_sockListen);
				sockSelectErrors.Add(m_sockListen);
				sockSelectReads.AddRange(m_sockClients);
				sockSelectErrors.AddRange(m_sockClients);

				try
				{
					Socket.Select(sockSelectReads, null, sockSelectErrors, Int32.MaxValue);
				}
				catch (Exception e)
				{
					Trace.Debug("[{0}]ControlServer.Run - Select exception: {1}", Thread.CurrentThread.GetHashCode(), e.Message);
				}

				foreach (Socket sock in sockSelectReads)
				{
					if (sock == m_sockListen)
					{
						// new Client
						Socket sockClient= m_sockListen.Accept();
						sockClient.SendTimeout= SEND_TIMEOUT;
						m_sockClients.Add(sockClient);
					}
					else if (m_sockClients.Contains(sock))
					{
						bool bClose= false;
						int nSize= 0;
						try
						{
							if ((nSize= sock.Receive(m_data)) == 0)
								bClose= true;
						}
						catch (SocketException /*e*/)
						{
							bClose= true;
						}


						if (nSize != 0)
						{
							Trace.Debug("[{0}]ControlServer.Run - Client data ({1}): size={2}", Thread.CurrentThread.GetHashCode(), sock.RemoteEndPoint, nSize);

							if (!ExecCommand(sock, m_data, nSize))
								bClose= true;
						}

						if (bClose)
						{
							Trace.Debug("[{0}]ControlServer.Run - Client close ({1})", Thread.CurrentThread.GetHashCode(), sock.RemoteEndPoint);

							// socket Client closed
							sock.Close();
							m_sockClients.Remove(sock);
						}
					}
				}

				foreach (Socket sock in sockSelectErrors)
				{
					if (m_sockClients.Contains(sock))
					{
						sock.Close();
						m_sockClients.Remove(sock);
					}
				}

			}
		}

		// internal methods
		protected bool ExecCommand(Socket sock, byte[] data, int nSize)
		{
			if (nSize < 1)
			{
				ResultFailed(sock);
				return false;
			}

			bool bResult= false;
			ControlCommand cmd= (ControlCommand)m_data[0];
			switch (cmd)
			{
				case ControlCommand.Version:
					bResult= CmdVersion(sock);
					break;

				case ControlCommand.Stop:
					bResult= CmdStop(sock);
					break;

				case ControlCommand.ListSessions:
					break;

				case ControlCommand.CloseSession:
					break;

				default:
					ResultFailed(sock);
					bResult= false;
					break;
			}

			return bResult;
		}

		protected static bool SendResult(Socket sock, byte[] data, int nSize)
		{
			bool bResult= false;
			try
			{
				sock.Send(data, nSize, SocketFlags.None);
				bResult= true;
			}
			catch (Exception /*e*/) { }

			return bResult;
		}

		protected bool ResultFailed(Socket sock)
		{
			m_data[(int)ControlResultFailed.Code]= (byte)ControlResultCode.Failed;
			return SendResult(sock, m_data, (int)ControlResultFailed.Size);
		}

		protected bool CmdVersion(Socket sock)
		{
			Assembly asmb= Assembly.GetExecutingAssembly();
			Version ver= asmb.GetName().Version;
			m_data[(int)ControlResultVersion.Code]			= (byte)((Socks5IsRunning()) ? ControlResultCode.OK : ControlResultCode.NotOK);
			m_data[(int)ControlResultVersion.Major]			= (byte)ver.Major;
			m_data[(int)ControlResultVersion.Minor]			= (byte)ver.Minor;
			m_data[(int)ControlResultVersion.SvnRev]		= (byte)(ver.Build >> 8);			// little endian: lower byte
			m_data[(int)ControlResultVersion.SvnRev + 1]	= (byte)(ver.Build & 0xFF);			//				  higher byte
			return SendResult(sock, m_data, (int)ControlResultVersion.Size);
		}

		protected bool CmdStop(Socket sock)
		{
			if (!Socks5IsRunning())
			{
				ResultFailed(sock);
				return false;
			}

			m_data[(int)ControlResultStop.Code]= (byte)ControlResultCode.OK;
			bool bResult= SendResult(sock, m_data, (int)ControlResultStop.Size);
			m_socks5.Stop();
			return bResult;
		}

		protected bool Socks5IsRunning()
		{
			return ((m_socks5 != null) && (m_socks5.Running));
		}
	}
}
