﻿#region License (GPLv3)
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
	class Socks5ConnectorFactory
	{
		public static Socks5Connector Create(Socks5Command cmd, Socks5Session session)
		{
			switch (cmd)
			{
				case Socks5Command.Connect:
					return new Socks5ConnectorTcp(session);

				case Socks5Command.UdpAssociate:
					return new Socks5ConnectorUdp(session);
			}

			return null;
		}
	}
}
