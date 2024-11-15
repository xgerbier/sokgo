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

		protected enum WriteOutput
		{
			Console		= 0x01,
			Debug		= 0x02,		// VS debug window
			Syslog		= 0x04,		// Unix only
		}

		// data members
		protected static bool m_bInitLog= false;
		protected static object m_csInitLog= new object();
		protected static bool m_bUseSyslog= true;

		// methods
		public static void UseSyslog(bool useSyslog)
		{
			m_bUseSyslog= useSyslog;
		}

		public static void Debug(string strFormat, params object[] objArgs)
		{
			#if DEBUG
			Write(WriteOutput.Console | WriteOutput.Debug, strFormat, objArgs);
			#endif	// DEBUG
		}

		public static void Console(string strFormat, params object[] objArgs)
		{
			Write(WriteOutput.Console | WriteOutput.Debug, strFormat, objArgs);
		}

		public static void Log(string strFormat, params object[] objArgs)
		{
			Write(WriteOutput.Syslog | WriteOutput.Debug, strFormat, objArgs);
		}

		public static void Error(string strFormat, params object[] objArgs)
		{
			Write(WriteOutput.Console | WriteOutput.Syslog | WriteOutput.Debug, strFormat, objArgs);
		}

		// internal methods
		protected static void Write(WriteOutput output, string strFormat, params object[] objArgs)
		{
			string strMsg= string.Format(strFormat, objArgs);

			switch (Environment.OSVersion.Platform)
			{
				case PlatformID.Win32NT:
					if ((output & WriteOutput.Debug) != 0)
						System.Diagnostics.Debug.WriteLine(strMsg);

					// check not detached console (AttachConsole(-1))
					if (((output & WriteOutput.Console) != 0) && (SokgoWin32.GetConsoleWindow() != IntPtr.Zero))
						System.Console.WriteLine(strMsg);
					break;

				case PlatformID.Unix:
				case PlatformID.MacOSX:
					bool bOutputSyslog= m_bUseSyslog && ((output & WriteOutput.Syslog) != 0);
					bool bOutputConsole= ((output & WriteOutput.Console) != 0) || ((!m_bUseSyslog) && ((output & WriteOutput.Syslog) != 0));	// fallback to console if syslog is explicitly disabled

					// Note : System.Console.WriteLine() will also write to syslog if this is a running service (systemd)
					if (bOutputConsole)
						System.Console.WriteLine(strMsg);

					if (bOutputSyslog)
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

						string[] strLines= strMsg.Split(NEWLINE_CHARS);
						foreach (string str in strLines)
							SokgoUnix.syslog(str);
					}
					break;
			}
		}

	}
}
