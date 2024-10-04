#!/bin/bash

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

file_cs_dir=$(dirname $0)
svn_base_dir=$file_cs_dir/../..
git_base_dir=$file_cs_dir/../..
file_base=$file_cs_dir/BuildId.cs
file_cs=$file_base
file_in=$file_base.in

# get_cs_var()
# 1: .cs file
# 2: varname
function get_cs_var()
{
	if [[ -f $1 ]]; then
		value=`grep -e "$2\s*=\s*\"" $1 | sed -e "s|.*$2\s*=\s*\"\(\w*\)\".*$|\1|"`
		echo $value
	fi;
}

# has_x()
# 1: executable file
function has_x()
{
	which $1 >/dev/null 2>&1
}

function get_svn_rev()
{
	pushd $svn_base_dir >/dev/null 2>&1
	has_x svnversion && (svnversion | grep -o '[0-9]\+' )
	popd >/dev/null 2>&1
}

function get_git_hash()
{
	pushd $git_base_dir >/dev/null 2>&1
	has_x git && git log -1 --format=%H 2>/dev/null
	popd >/dev/null 2>&1
}

last_svn_rev=`get_cs_var $file_cs SVN_REV`
last_git_hash=`get_cs_var $file_cs GIT_HASH`
#echo last_svn_rev=$last_svn_rev
#echo last_git_hash=$last_git_hash

svn_rev=`get_svn_rev`
git_hash=`get_git_hash`
#echo svn_rev=$svn_rev
#echo git_hash=$git_hash

build_changed=0
if [[ ( -n $svn_rev && $svn_rev != $last_svn_rev ) || ( -n $git_hash && $git_hash != $last_git_hash ) ]]; then
	build_changed=1
fi
#echo build_changed=$build_changed

if [[ build_changed -eq 1 ]]; then
	sed -e "s|%svnrev%|$svn_rev|"				\
		-e "s|%githash%|$git_hash|"				\
		-e "s|%file%|`basename $file_cs`|"		\
		-e "s|%date%|`date -R`|"				\
		< $file_in > $file_cs
fi