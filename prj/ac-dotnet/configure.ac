#
#	Copyright (C) 2011,2012,2013,2024 X.Gerbier
#
#	This file is part of Sokgo.
#
#	Sokgo is free software: you can redistribute it and/or modify
#	it under the terms of the GNU General Public License as published by
#	the Free Software Foundation, either version 3 of the License, or
#	(at your option) any later version.
#
#	Sokgo is distributed in the hope that it will be useful,
#	but WITHOUT ANY WARRANTY; without even the implied warranty of
#	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
#	GNU General Public License for more details.
#
#	You should have received a copy of the GNU General Public License
#	along with Sokgo.  If not, see <http://www.gnu.org/licenses/>.
#

AC_INIT([sokgo], [0.7])
AC_CONFIG_SRCDIR([src/Sokgo.cs])
AC_CANONICAL_SYSTEM
AM_INIT_AUTOMAKE([ --warnings=none ])
AM_MAINTAINER_MODE

AC_PROG_INSTALL
AC_PROG_CC
AC_PROG_LN_S
AC_PROG_MKDIR_P
AC_PROG_SED

dnl Check for .NET
AC_PATH_PROG(RUNTIME, dotnet, no)
AC_PATH_PROG(DOTNET, dotnet, no)
TARGET_EXT=".dll"
has_dotnet_sdk=no
if test "x$DOTNET" != xno; then
	if test `$DOTNET --list-sdks | wc -l` -gt 0; then has_dotnet_sdk=yes; fi
fi

if test "x$DOTNET" = xno -o "x$has_dotnet_sdk" = xno; then
	AC_PATH_PROG(MONO, mono, no)
	AC_PATH_PROGS(CSC, gmcs mcs, no)
	if test "x$MONO" != xno -a "x$CSC" != xno; then
		AC_MSG_NOTICE([Mono development found. You may use 'autogen.sh' in 'prj/ac-mono' instead.])
	fi
fi

if test "x$RUNTIME" = xno ; then
	AC_MSG_ERROR([.NET runtime not found. You need to install either .NET SDK or mono-devel.])
fi

if test "x$has_dotnet_sdk" = xno ; then
	AC_MSG_ERROR([.NET SDK not found. You need to install either .NET SDK or mono-devel.])
fi

AC_SUBST(RUNTIME)
AC_SUBST(DOTNET)
AC_SUBST(TARGET_EXT)

dnl Debug
AC_ARG_ENABLE([debug],
	AS_HELP_STRING([--enable-debug], [Enable debug build]),
	[], [enable_debug=no])
AM_CONDITIONAL([DEBUG], [test x$enable_debug = xyes])

AC_CONFIG_FILES([
	Makefile
	src/Makefile
	init/Makefile
])

AC_OUTPUT

echo ""
echo "Configuration summary"
echo "---------------------"
echo ""
echo "   * Installation prefix: $prefix"
echo "   * Dotnet: $DOTNET"
echo "   * Debug: $enable_debug"
echo ""
