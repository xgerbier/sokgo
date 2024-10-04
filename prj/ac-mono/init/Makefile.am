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

initdir= $(sysconfdir)/init.d
deamon_initdir=/etc/init.d
init_SCRIPTS= sokgo
EXTRA_DIST= sokgo.in
CLEANFILES= $(init_SCRIPTS)

do_subst= $(SED) -e 's,[@]bindir[@],$(bindir),g'			\
				 -e 's,[@]RUNTIME[@],$(RUNTIME),g'			\
				 -e 's,[@]PACKAGE[@],$(PACKAGE),g'

sokgo : sokgo.in Makefile
	$(do_subst) < $< > $@
	chmod +x $@

install-data-hook :
	test "$(initdir)" == "$(deamon_initdir)" || $(LN_S) -f $(initdir)/sokgo $(deamon_initdir)/sokgo
	update-rc.d sokgo defaults

uninstall-hook :
	update-rc.d sokgo remove
	rm -f $(deamon_initdir)/sokgo
