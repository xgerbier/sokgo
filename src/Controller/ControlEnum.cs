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

namespace Sokgo.Controller
{

	public enum ControlCommand
	{
		Version			= 0x00,
		Stop			= 0x01,
		ListSessions	= 0x02,
		CloseSession	= 0x03,
	}

	public enum ControlResultCode
	{
		NotOK			= 0x00,
		OK				= 0x01,
		RawData			= 0x02,
		Failed			= 0xFF,
	}

	public enum ControlRequestStop
	{
		Command			= 0,	// byte offsets
		Size			= 1,
	}

	public enum ControlRequestVersion
	{
		Command			= 0,	// byte offsets
		Size			= 1,
	}

	public enum ControlReturn
	{
		NoServer,
		OK,
		Failed,
	}

	public enum ControlResultFailed
	{
		Code			= 0,	// byte offsets
		Size			= 1,
	}

	public enum ControlResultStop
	{
		Code			= 0,	// byte offsets
		Size			= 1,
	}

	public enum ControlResultVersion
	{
		Code			= 0,	// byte offsets
		Major			= 1,
		Minor			= 2,
		SvnRev			= 3,
		Size			= 5,
	}
}
