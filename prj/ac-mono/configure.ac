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

dnl Check for Mono
PKG_CHECK_MODULES(MONO_DEPENDENCY, mono >= 1.0, has_mono=yes, has_mono=no)
PKG_CHECK_MODULES(MONO_DEPENDENCY, mono < 4.1, has_mono_lt_4_1=yes, has_mono_lt_4_1=no)
AC_PATH_PROGS(CSC, gmcs mcs, no)
AC_PATH_PROG(RUNTIME, mono, no)
TARGET_EXT=".exe"

if test "x$has_mono" = xno -o "x$RUNTIME" = xno -o "x$CSC" = xno; then
	AC_PATH_PROG(DOTNET, dotnet, no)
	if test "x$DOTNET" != xno; then
		if test `$DOTNET --list-sdks | wc -l` -gt 0; then
			AC_MSG_NOTICE([.NET SDK found. You may use 'autogen.sh' in 'prj/ac-dotnet' instead.])
		fi
	fi
fi

if test "x$RUNTIME" = xno; then
	AC_MSG_ERROR([Mono runtime not found. You need to install either mono-devel or .NET SDK.])
fi

CS="C#"
if test "x$CSC" = xno; then
	AC_MSG_ERROR([No $CS compiler found. You need to install either mono-devel or .NET SDK.])
fi

AC_SUBST(RUNTIME)
AC_SUBST(CSC)
AC_SUBST(TARGET_EXT)

dnl Debug
AC_ARG_ENABLE([debug],
	AS_HELP_STRING([--enable-debug], [Enable debug build]),
	[], [enable_debug=no])
AM_CONDITIONAL([DEBUG], [test "x$enable_debug" = xyes])

dnl Mono version < 4.1 : SocketOptionName.IPV6Only not defined
AM_CONDITIONAL([MONO_LT_4_1], [test "x$has_mono_lt_4_1" = xyes])

AC_CONFIG_FILES([
	Makefile
	src/Makefile
	init/Makefile
	get_IPV6_V6ONLY/Makefile
])

AC_OUTPUT

echo ""
echo "Configuration summary"
echo "---------------------"
echo ""
echo "   * Installation prefix: $prefix"
echo "   * Compiler: $CSC"
echo "   * Mono version < 4.1: $has_mono_lt_4_1"
echo "   * Debug: $enable_debug"
echo ""
