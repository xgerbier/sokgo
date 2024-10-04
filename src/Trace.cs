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
using System.Diagnostics;
using System.Reflection;

namespace Sokgo
{
	public class Trace
	{
		// constants
		protected static readonly char[] NEWLINE_CHARS= { '\n' };

		// data members
		protected static bool m_bInitLog= false;
		protected static Object m_csInitLog= new Object();

		// methods
		public static void Debug(String strFormat, params object[] objArgs)
		{
			#if DEBUG
			Write(true, strFormat, objArgs);
			#endif
		}

		public static void Console(String strFormat, params object[] objArgs)
		{
			Write(true, strFormat, objArgs);
		}

		public static void Log(String strFormat, params object[] objArgs)
		{
			Write(false, strFormat, objArgs);
		}

		public static void Error(String strFormat, params object[] objArgs)
		{
			Write(false, strFormat, objArgs);
		}

		// internal methods
		protected static void Write(bool bForceConsole, String strFormat, params object[] objArgs)
		{
			String strMsg= String.Format(strFormat, objArgs);

			switch (Environment.OSVersion.Platform)
			{
				case PlatformID.Win32NT:
					System.Diagnostics.Debug.WriteLine(strMsg);
					if ((bForceConsole) && (SokgoWin32.GetConsoleWindow() != IntPtr.Zero))
						System.Console.WriteLine(strMsg);
					break;

				case PlatformID.Unix:
				case PlatformID.MacOSX:
					if (bForceConsole)
					{
						System.Console.WriteLine(strMsg);
					}
					else
					{
						lock (m_csInitLog)
						{
							if (!m_bInitLog)
							{
								Assembly asmb= Assembly.GetExecutingAssembly();
								SokgoUnix.openlog(asmb.GetName().Name);
								m_bInitLog= true;
							}
						}

						String[] strLines= strMsg.Split(NEWLINE_CHARS);
						foreach (String str in strLines)
							SokgoUnix.syslog(str);
					}
					break;
			}
		}

	}
}
