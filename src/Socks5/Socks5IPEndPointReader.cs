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
using System.Diagnostics;
using System.IO;

namespace Sokgo.Socks5
{
	class Socks5IPEndPointReader
	{
		// enum
		public enum StatusCode
		{
			OK,
			WaitingDns,
			InvalidIPv4,
			InvalidIPv6,
			InvalidPort,
			InvalidAddressType,
			InvalidDomain,
		}

		// data members
		protected Stream m_stream;
		protected int m_nReadLength= 0;
		protected StatusCode m_status= StatusCode.OK;
		protected IPEndPoint m_endPoint= null;
		protected string m_domainName= null;

		//constructor(s)
		public Socks5IPEndPointReader(Stream stream)
		{
			if (stream == null)
				throw new ArgumentException();
			m_stream= stream;
		}

		// properties
		public int ReadLength
		{
			get { return m_nReadLength; }
		}

		public StatusCode Status
		{
			get { return m_status; }
		}

		// methods
		public IPEndPoint Read(Socks5DnsResponseNotify cbDnsNotify)
		{
			return Read(cbDnsNotify, null);
		}

		public IPEndPoint Read(Socks5DnsResponseNotify cbDnsNotify, Object objUserNotify)
		{
			Debug.Assert(m_stream != null);

			m_status= StatusCode.OK;

			// Address type
			int nReadAddressType= m_stream.ReadByte();
			if (nReadAddressType >= 0)
				m_nReadLength++;
			Socks5AddressType nAddressType= (Socks5AddressType)nReadAddressType;

			// IP
			IPAddress ip= null;
			switch (nAddressType)
			{
				case Socks5AddressType.IPv4:
					if ((ip= ReadIPv4()) == null)
						m_status= StatusCode.InvalidIPv4;
					break;

				case Socks5AddressType.IPv6:
					if ((ip= ReadIPv6()) == null)
						m_status= StatusCode.InvalidIPv6;
					break;

				case Socks5AddressType.DomainName:
					if (ReadDomainName(cbDnsNotify, objUserNotify))
					{
						ip= IPAddress.Any;
						m_status= StatusCode.WaitingDns;
					}
					else
					{
						m_status= StatusCode.InvalidDomain;
					}
					break;

				default:
					m_status= StatusCode.InvalidAddressType;
					break;
			}

			if (ip == null)
				return null;

			// Port
			int nPort= ReadPort();
			if (nPort < 0)
			{
				m_status= StatusCode.InvalidPort;
				return null;
			}

			m_endPoint = new IPEndPoint(ip, nPort);
			return m_endPoint;
		}

		public override string ToString()
		{
			if (m_endPoint == null)
			{
				return "";
			}

			string s = "";
			if (m_endPoint != null)
			{
				if (m_endPoint.Address != IPAddress.Any)
				{
					s = m_endPoint.ToString();
				}
				else if (!string.IsNullOrEmpty(m_domainName))
				{
					s = string.Format("{0}:{1}", m_domainName, m_endPoint.Port);
				}
			}
			return s;
		}

		// internal methods
		protected bool ReadDomainName(Socks5DnsResponseNotify cbDnsNotify, Object objUserNotify)
		{
			Debug.Assert(m_stream != null);

			// Read string len
			int nLen= m_stream.ReadByte();
			if (nLen <= 0)
				return false;
			m_nReadLength++;

			// Read string
			byte[] rawDomainName= new byte[nLen];
			int nRead= m_stream.Read(rawDomainName, 0, nLen);
			m_nReadLength+= nRead;
			if (nRead != nLen)
				return false;
			String strDomainName;
			try
			{
				strDomainName= Encoding.ASCII.GetString(rawDomainName);
			}
			catch (DecoderFallbackException /*e*/)
			{
				return false;
			}

			m_domainName= strDomainName;
			return Socks5Dns.ResolveHost(strDomainName, cbDnsNotify, objUserNotify);
		}

		protected IPAddress ReadIPv4()
		{
			return ReadIP(4);
		}

		protected IPAddress ReadIPv6()
		{
			return ReadIP(16);
		}

		protected IPAddress ReadIP(int nSize)
		{
			Debug.Assert(m_stream != null);
			Debug.Assert((nSize == 4) || (nSize == 16));

			byte[] rawIp= new byte[nSize];
			int nRead= m_stream.Read(rawIp, 0, nSize);
			m_nReadLength+= nRead;
			if (nRead != nSize)
				return null;

			return new IPAddress(rawIp);
		}

		protected int ReadPort()
		{
			Debug.Assert(m_stream != null);

			byte[] rawPort= new byte[2];
			if (m_stream.Read(rawPort, 0, 2) != 2)
				return -1;
			m_nReadLength+= 2;

			// Big endian
			return ((rawPort[0] << 8) | (rawPort[1]));
		}

	}
}
