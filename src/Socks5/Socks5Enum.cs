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

namespace Sokgo.Socks5
{
	public enum Socks5SessionState
	{
		Unconnected,
		OpenWaiting,
		OpenReceived,
		OpenAck,
		RequestWaiting,
		RequestReceived,
		RequestWaitingDns,
		RequestConnect,
		RequestAck,
		Connected,
		TimeOut,
		CloseWaiting,
		Closed,
		Failed
	}

	public enum Socks5SessionDataState
	{
		None,
		ReadWait,
		ReadReceived,
		WriteAck
	}

	public enum Socks5ConnectorState
	{
		None,
		Connecting,
		ReadWaiting,
		WriteWaiting,
	}

	public enum Socks5ConnectorUdpClientState
	{
		None,
		Connecting,
		Connected,
	}

	public enum Socks5Command
	{
		Connect				= 0x01,
		Bind				= 0x02,
		UdpAssociate		= 0x03,
	}

	public enum Socks5AddressType
	{
		IPv4				= 0x01,
		DomainName			= 0x03,
		IPv6				= 0x04,
	}

	public enum Socks5Method
	{
		NoAuthRequired		= 0x00,
		GssApi				= 0x01,
		UsernamePassword	= 0x02,
		NotAcceptable		= 0xFF,
	}

	public enum Socks5Result
	{
		Succeeded				= 0x00,
		SocksServerFailure		= 0x01,
		NotAllowed				= 0x02,
		NetworkUnreachable		= 0x03,
		HostUnreachable			= 0x04,
		ConnectionRefused		= 0x05,
		TtlExpired				= 0x06,
		CommandNotSupported		= 0x07,
		AddressTypeNotSupported	= 0x08,
	}

	public enum Socks5ConnectorType
	{
		Tcp,
		Udp,
	}

	public enum Socks5ConnectorDataSource
	{
		None				= 0x00,
		FromClient			= 0x01,
		FromRemote			= 0x02,
	}

}
