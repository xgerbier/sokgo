#!/bin/sh
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

ln_s()
{
	ln -sf $1 $2 2> /dev/null && return 0;
	cp $1 $2
}

ac_dir="`dirname $0`"
cd $ac_dir
top_dir=`realpath ../..`
ac_rel_dir=`realpath --relative-to=../.. .`

# sed -e 's,/\([^/]\+/[^/]\+\)$,\1,'

# copy ac files
cd $top_dir
ac_files="configure.ac Makefile.am AUTHORS ChangeLog COPYING NEWS README"
for f in $ac_files; do
	ln_s $ac_rel_dir/$f $f
done

# copy src/Makefile.am
mkdir -p $top_dir/src
cd $top_dir/src
src_files="Makefile.am sokgo.in"
for f in $src_files; do
	ln_s ../$ac_rel_dir/src/$f $f
done

# copy init files
mkdir -p $top_dir/init
cd $top_dir/init
init_files="Makefile.am sokgo.in"
for f in $init_files; do
	ln_s ../$ac_rel_dir/init/$f $f
done

# autoconf & configure
cd $top_dir
autoupdate
# autoreconf -i --force --warnings=none
autoreconf -i --force
./configure --enable-maintainer-mode $*
