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
using System.Net.Sockets;
using System.Threading;
using System.Configuration;
using System.Reflection;
using Sokgo.Socks5;
using Sokgo.Controller;
using CommandLine.Utility;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Sokgo
{
	class Sokgo
	{
		// consts

		protected const String EXEC_UNIX_MONO			= "mono";
		protected const String EXEC_UNIX_DOTNET			= "dotnet";
		protected const String RUNTIME_ENV_MONO			= "mono";
		protected const String RUNTIME_ENV_MONO_LT_4_1	= RUNTIME_ENV_MONO + "-lt4.1";
		protected const String RUNTIME_ENV_DOTNET		= "dotnet";
		protected const String PARAM_START				= "start";
		protected const String PARAM_SPAWN				= "daemon";
		protected const String PARAM_STOP				= "stop";
		protected const String PARAM_QUIET				= "quiet";
		protected const String PARAM_VERSION			= "version";
		protected const String PARAM_HELP				= "help";
		protected const String PIDFILE					= "/var/run/sokgo.pid";

		// data members

		protected static StreamWriter m_swPid= null;

		// methods

		static void Main(string[] _args)
		{
			Trace.UseSyslog(Socks5Server.Config.TraceLogToSyslog);

			Arguments args= new Arguments(_args);

			bool bStart= (args[PARAM_START] != null);
			bool bDaemon= (args[PARAM_SPAWN] != null);		// param for internal use (running as a daemon)
			bool bStop= (args[PARAM_STOP] != null);
			bool bQuiet= (args[PARAM_QUIET] != null);
			bool bVersion= (args[PARAM_VERSION] != null);
			bool bHelp= (args[PARAM_HELP] != null);

			if (bHelp)
			{
				Help();
				return;
			}

			if (bVersion)
			{
				Trace.Console(GetFullAppName());
				return;
			}

			if (bStop)
			{
				ControlReturn bStopResult= ControlClient.ServerStop();
				if (!bQuiet)
				{
					switch (bStopResult)
					{
						case ControlReturn.OK:
							Trace.Console("Daemon stopped...");
							break;

						case ControlReturn.Failed:
							Trace.Console("ERROR: Failed to stop daemon...");
							break;

						case ControlReturn.NoServer:
							Trace.Console("ERROR: No daemon running...");
							break;
					}
				}
				return;
			}

			if ((!bStart) && (!bDaemon))
			{
				Trace.Console(GetFullAppName());
				Help();
				return;
			}

			if (ControlClient.ServerIsRunning())
			{
				if (!bQuiet)
					Trace.Console("ERROR: Daemon is already running...");
				return;
			}

			if ((bStart) && (!bQuiet))
				Trace.Console("Starting daemon...");
			if (!bDaemon)
			{
				// try to spawn a daemon process
				Daemonize();
				return;
			}

			// running as a daemon
			Trace.Log("starting {0}", GetFullAppName());
			CreatePidFile();

			Socks5Server server= new Socks5Server();
			server.Start();

			ControlServer ctrl= new ControlServer(server);
			ctrl.Start();

			while (server.Running)
				Thread.Sleep(1000);

			RemovePidFile();

			Trace.Log("exit");
			Process.GetCurrentProcess().Kill();
		}

		// spawn a daemon process
		protected static bool Daemonize()
		{
			String strExe= Assembly.GetExecutingAssembly().Location;

			try
			{
				switch (Environment.OSVersion.Platform)
				{
					case PlatformID.Win32NT:
						SokgoWin32.FreeConsole();
						SokgoWin32.AttachConsole(-1);

						// run from command line: spawn
						String strWin32Arg= String.Format("--{0}", PARAM_SPAWN);
						ProcessStartInfo psi= new ProcessStartInfo(strExe, strWin32Arg);
						psi.WindowStyle= ProcessWindowStyle.Hidden;
						Process.Start(psi);
						break;

					case PlatformID.Unix:
						// spawn
						/*
						if (SokgoUnix.fork() != 0)
							return false;

						SokgoUnix.setsid();
						*/

						String strLinuxArg= String.Format("{0} {1} --{2}", GetRunUnixDotnet(), strExe, PARAM_SPAWN);
						Process.Start("setsid", strLinuxArg);
						break;

				//	case PlatformID.MacOSX:
				//		break;

					default:
						throw new Exception("System \"" + Environment.OSVersion.Platform + "\" not supported.");
				}
			}
			catch (Exception e)
			{
				Trace.Console("ERROR: failed to daemonize.");
				Trace.Console(e.ToString());
				return false;
			}

			return true;
		}

		static bool CreatePidFile()
		{
			if (Environment.OSVersion.Platform != PlatformID.Unix)
				return false;

			long nPid= SokgoUnix.getpid();

			bool bResult= false;
			try
			{
				FileStream fs= new FileStream(PIDFILE, FileMode.Create, FileAccess.Write, FileShare.Read);
				m_swPid= new StreamWriter(fs);
				m_swPid.WriteLine(Convert.ToString(nPid));
				m_swPid.Flush();
				bResult= true;
			}
			catch (Exception e)
			{
				Trace.Error("ERROR: failed to create pid file.");
				Trace.Error(e.ToString());
			}

			return bResult;
		}

		static bool RemovePidFile()
		{
			if ((Environment.OSVersion.Platform != PlatformID.Unix) || (m_swPid == null))
				return false;

			m_swPid.Close();

			bool bResult= false;
			try
			{
				File.Delete(PIDFILE);
				bResult= true;
			}
			catch (Exception /*e*/) { }

			return bResult;
		}

		static String GetRunUnixDotnet()
		{
			ProcessModule pm= Process.GetCurrentProcess().MainModule;
			// ex: /usr/lib/dotnet/dotnet, /usr/share/dotnet/dotnet, /usr/bin/mono-sgen
			String strLinuxDotnetExe= pm?.FileName ?? "";

			#if MONO
			String strLinuxDefaultExe= EXEC_UNIX_MONO;
			#else	// !MONO
			String strLinuxDefaultExe= EXEC_UNIX_DOTNET;
			#endif	// !MONO

			return (strLinuxDotnetExe.Contains(strLinuxDefaultExe)) ? strLinuxDotnetExe : strLinuxDefaultExe;
		}

		static string GetFullAppName()
		{
			StringBuilder sb= new StringBuilder(100);
			Assembly asmb= Assembly.GetExecutingAssembly();
			AssemblyTitleAttribute[] asmbTitles= (AssemblyTitleAttribute[])asmb.GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
			AssemblyName asmbName= asmb.GetName();
			string buildId = BuildId.ShortTag;

			sb.Append((asmbTitles.Length > 0) ? asmbTitles[0].Title : asmbName.Name)
				.Append(" v")
				.Append(asmbName.Version.ToString(3));

			if (!string.IsNullOrEmpty(buildId))
				sb.Append("-").Append(buildId);

			#if MONO && MONO_LT_4_1
			String strRuntimeEnv= RUNTIME_ENV_MONO_LT_4_1;
			#elif MONO
			String strRuntimeEnv= RUNTIME_ENV_MONO;
			#else	// !MONO
			String strRuntimeEnv= RUNTIME_ENV_DOTNET;
			#endif	// !MONO
			sb.Append("-").Append(strRuntimeEnv);

			#if DEBUG
			sb.Append(" [dbg]");
			#endif	// DEBUG

			return sb.ToString();
		}

		static void Help()
		{
			Trace.Console("Usage: sokgo [--start | --stop] [--quiet] [--help]");
			Trace.Console("  --start   : start daemon");
			Trace.Console("  --stop    : stop daemon");
			Trace.Console("  --quiet   : no output to the console");
			Trace.Console("  --version : print version");
			Trace.Console("  --help    : this help");
		}
	}
}
