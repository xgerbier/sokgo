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
using System.Runtime.InteropServices;

namespace Sokgo
{
	sealed class SokgoWin32
	{
		[DllImport("kernel32.dll", EntryPoint= "AllocConsole", SetLastError= true, CallingConvention= CallingConvention.Winapi)]
		private static extern bool _AllocConsole();

		[DllImport("kernel32.dll", EntryPoint= "FreeConsole", SetLastError=true, CallingConvention= CallingConvention.Winapi)]
		private static extern bool _FreeConsole();

		[DllImport("kernel32.dll", EntryPoint= "AttachConsole", SetLastError= true, CallingConvention= CallingConvention.Winapi)]
		private static extern bool _AttachConsole(int dwProcessId);

		[DllImport("kernel32.dll", EntryPoint= "GetConsoleWindow", SetLastError= true, CallingConvention= CallingConvention.Winapi)]
		private static extern IntPtr _GetConsoleWindow();

		public static bool AllocConsole()
		{
			if (Environment.OSVersion.Platform != PlatformID.Win32NT)
				return false;

			return _AllocConsole();
		}

		public static bool FreeConsole()
		{
			if (Environment.OSVersion.Platform != PlatformID.Win32NT)
				return false;

			return _FreeConsole();
		}

		public static bool AttachConsole(int dwProcessId)
		{
			if (Environment.OSVersion.Platform != PlatformID.Win32NT)
				return false;

			return _AttachConsole(dwProcessId);
		}

		public static IntPtr GetConsoleWindow()
		{
			if (Environment.OSVersion.Platform != PlatformID.Win32NT)
				return IntPtr.Zero;

			return _GetConsoleWindow();
		}

	}
}
