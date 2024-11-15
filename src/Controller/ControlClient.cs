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

namespace Sokgo.Controller
{
	class ControlClient
	{
		// consts
		public const int PORT				= ControlServer.PORT;
		protected const int DATA_SIZE		= 4 * 1024;
		protected const int SEND_TIMEOUT	= 1000;	// ms
		protected static readonly Version VERSION_ZERO	= new Version(0, 0, 0, 0);
		protected static readonly Version VERSION_MIN	= new Version(0, 6, 0, 0);			// v0.6

		// data members
		protected static Socket m_sock;
		protected static byte[] m_data= new byte[DATA_SIZE];

		// methods
		public static bool ServerIsRunning()
		{
			Version ver;
			ControlReturn nRet= ServerVersion(out ver);
			return (nRet != ControlReturn.NoServer);
		}

		public static ControlReturn ServerVersion(out Version ver)
		{
			InitControl();

			ver= VERSION_ZERO;

			if (m_sock == null)
				return ControlReturn.NoServer;

			bool bResult= false;
			try
			{
				m_data[0]= (byte)ControlCommand.Version;
				m_sock.Send(m_data, (int)ControlRequestVersion.Size, SocketFlags.None);
				int nSize= m_sock.Receive(m_data);
				if ((nSize >= (int)ControlResultVersion.Size) && (m_data[(int)ControlResultVersion.Code] == (byte)ControlResultCode.OK))
				{
					int nMajor	= m_data[(int)ControlResultVersion.Major];
					int nMinor	= m_data[(int)ControlResultVersion.Minor];
					int nSvnRev	= (m_data[(int)ControlResultVersion.SvnRev] << 8) | (m_data[(int)ControlResultVersion.SvnRev + 1]);
					ver= new Version(nMajor, nMinor, nSvnRev);
					bResult= true;
				}
			}
			catch (SocketException /*e*/) { }

			return (bResult) ? ControlReturn.OK : ControlReturn.Failed;
		}

		public static ControlReturn ServerStop()
		{
			InitControl();

			if (m_sock == null)
				return ControlReturn.NoServer;

			if (!CheckVersion())
				return ControlReturn.Failed;

			bool bResult= false;
			try
			{
				m_data[0]= (byte)ControlCommand.Stop;
				m_sock.Send(m_data, 1, SocketFlags.None);
				int nSize= m_sock.Receive(m_data);
				bResult= ((nSize >= (int)ControlResultStop.Size) && (m_data[(int)ControlResultStop.Code] == (byte)ControlResultCode.OK));
			}
			catch (SocketException /*e*/) { }

			return (bResult) ? ControlReturn.OK : ControlReturn.Failed;
		}

		// internal methods
		protected static void InitControl()
		{
			if ((m_sock != null) && (m_sock.Connected))
				return;

			// when server is running, socket will initialize successfully
			m_sock= new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			try
			{
				m_sock.Connect(IPAddress.Loopback, PORT);
				m_sock.SendTimeout= SEND_TIMEOUT;
			}
			catch (Exception /*e*/)
			{
				m_sock= null;
			}
		}

		protected static bool CheckVersion()
		{
			Version ver;
			ServerVersion(out ver);

			Trace.Debug("[" + Thread.CurrentThread.GetHashCode() + "]Control.Client - Server version \"" + ver.ToString(3) + "\"");
			bool bVerOK= ((ver != null) && (ver >= VERSION_MIN));

			if (!bVerOK)
				Trace.Console("ERROR: incompatible server version {0} (min: {1})", ver.ToString(3), VERSION_MIN.ToString(3));

			return bVerOK;
		}
	}
}
