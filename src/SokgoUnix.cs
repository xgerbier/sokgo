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
	sealed class SokgoUnix
	{
		// IPV6_V6ONLY value from socket.h; automatically updated by configure script
		//	- Debian/Ubuntu	: 26
		//	- Cygwin		: 27
		public const int IPV6_V6ONLY= 26;

		private static IntPtr m_pszIdent= IntPtr.Zero;

		[DllImport("libc", EntryPoint= "getpid", CallingConvention= CallingConvention.Cdecl)]
		private static extern int _getpid();

		[Obsolete]
		[DllImport("libc", EntryPoint= "setsid", CallingConvention= CallingConvention.Cdecl)]
		private static extern int _setsid();

		[Obsolete]
		[DllImport("libc", EntryPoint= "fork", CallingConvention= CallingConvention.Cdecl)]
		private static extern int _fork();

		[Obsolete]
		[DllImport("libc", EntryPoint= "exit", CallingConvention= CallingConvention.Cdecl)]
		private static extern void _exit(int status);

		[DllImport("libc", EntryPoint= "setsockopt", CallingConvention= CallingConvention.Cdecl)]
		private static extern int _setsockopt(IntPtr sockfd, int level, int optname, byte[] optval, int optlen);

		[DllImport("libc", EntryPoint= "openlog", CallingConvention= CallingConvention.Cdecl)]
		private static extern void _openlog(IntPtr ident, int option, int facility);

		[DllImport("libc", EntryPoint= "syslog", CallingConvention= CallingConvention.Cdecl, CharSet= CharSet.Ansi)]
		private static extern void _syslog(int priority, string msg);

		[DllImport("libc", EntryPoint= "closelog", CallingConvention= CallingConvention.Cdecl)]
		private static extern void _closelog();

		public static long getpid()
		{
			if (Environment.OSVersion.Platform != PlatformID.Unix)
				return -1;

			return _getpid();
		}

		public static long setsid()
		{
			if (Environment.OSVersion.Platform != PlatformID.Unix)
				return -1;

		//	return Syscall.setsid();
			throw new NotImplementedException();
		}

		public static long fork()
		{
			if (Environment.OSVersion.Platform != PlatformID.Unix)
				return -1;

		//	return Mono.Posix.Syscall.fork();
			throw new NotImplementedException();
		}

		public static void exit(int status)
		{
			if (Environment.OSVersion.Platform != PlatformID.Unix)
				return;

		//	_exit(status);
			throw new NotImplementedException();
		}

		public static int setsockopt(System.Net.Sockets.Socket s, System.Net.Sockets.SocketOptionLevel level, int nOptName, bool bValue)
		{
			return setsockopt(s, level, nOptName, ((bValue) ? 1 : 0));
		}

		public static int setsockopt(System.Net.Sockets.Socket s, System.Net.Sockets.SocketOptionLevel level, int nOptName, int nValue)
		{
			// big endian order
			byte[] b= new byte[4];
			b[0]= (byte)((nValue) >> 24);
			b[1]= (byte)((nValue) >> 16);
			b[2]= (byte)((nValue) >> 8);
			b[3]= (byte)(nValue & 0xFF);

			return setsockopt(s, level, nOptName, b);
		}

		public static int setsockopt(System.Net.Sockets.Socket s, System.Net.Sockets.SocketOptionLevel level, int nOptName, byte[] value)
		{
			if (Environment.OSVersion.Platform != PlatformID.Unix)
				return -1;

			// Mono 2.8 (< 4.1) : Socket.SetSocketOption() implementation doesn't allow to force a not defined 'optname' in SocketOptionName.
			// cf. :
			//	- C# Socket.SetSocketOption() :
			//		https://github.com/mono/mono/blob/2.8/mcs/class/System/System.Net.Sockets/Socket.cs#L3045
			//	- C# Socket.SetSocketOption_internal() :
			//		https://github.com/mono/mono/blob/2.8/mcs/class/System/System.Net.Sockets/Socket_2_1.cs#L701
			//	- C ves_icall_System_Net_Sockets_Socket_SetSocketOption_internal() & convert_sockopt_level_and_name() :
			//		https://github.com/mono/mono/blob/2.8/mono/metadata/socket-io.c#L1981
			//		https://github.com/mono/mono/blob/2.8/mono/metadata/socket-io.c#L352
			//	- C enum SocketOptionName_..., SocketOptionName_IPV6Only not defined :
			//		https://github.com/mono/mono/blob/2.8/mono/metadata/socket-io.h#L93
			// We use interop with native libc to access directly setsockopt().

			return _setsockopt(s.Handle, (int)level, nOptName, value, value.Length);
		}

		public static void openlog(string strIdent)
		{
			if (Environment.OSVersion.Platform != PlatformID.Unix)
				return;

			if (m_pszIdent != IntPtr.Zero)
			{
				_closelog();
				Marshal.FreeHGlobal(m_pszIdent);
			}

			// WARNING: 'ident' must be persistent to be use in later call to syslog()
			m_pszIdent= Marshal.StringToHGlobalAnsi(strIdent);
			_openlog(m_pszIdent, 0x01 /* LOG_PID */, 3 << 3 /* LOG_DAEMON */);
		}

		public static void syslog(string strMsg)
		{
			if (Environment.OSVersion.Platform != PlatformID.Unix)
				return;

			_syslog(6 /* LOG_INFO */, strMsg);
		}
	}
}
