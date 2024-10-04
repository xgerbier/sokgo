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

TARGET=$(PACKAGE)$(TARGET_EXT)
CONFIG=$(PACKAGE)$(TARGET_EXT).config
COMMON_CONFIG=$(PACKAGE).config
RUNTIME_CONFIG=$(PACKAGE).runtimeconfig.json

PROJECT_IN=Sokgo
PROJECT=$(PACKAGE)

DOTNET_REFS= System.Configuration.dll
DOTNET_FLAGS=
if DEBUG
# NOTE : no indentation here
DOTNET_FLAGS += -c Debug
else
DOTNET_FLAGS += -c Release
endif

bin_dir_cs= ../bin
tmp_dir_cs= ../tmp
dll_dir_cs= $(tmp_dir_cs)/bin
prj_dir_cs= ../prj
vs_dir_cs= vs17
prj_vs_dir_in= $(prj_dir_cs)/$(vs_dir_cs)
prj_vs_dir= $(tmp_dir_cs)/$(vs_dir_cs)
src_dir_cs= .
src_cs_wildcards=		\
	*.cs				\
	Arguments/*.cs		\
	Controller/*.cs		\
	IPFilter/*.cs		\
	Properties/AssemblyInfo.cs	\
	Socks5/*.cs
src_dist_wildcards=		\
	*.cs				\
	app.config			\
	sokgo.in			\
	Arguments/*			\
	Controller/*		\
	IPFilter/*			\
	Properties/*		\
	Socks5/*

tmp_dir_dummy= $(tmp_dir_cs)/.dummy
prj_vs_dir_dummy= $(prj_vs_dir)/.dummy
bin_dir_dummy= $(bin_dir_cs)/.dummy
dll_dir_dummy= $(dll_dir_cs)/.dummy

prj_in= $(prj_vs_dir_in)/$(PROJECT_IN).csproj
prj= $(prj_vs_dir)/$(PROJECT).csproj

# function find_src_files dirs wildcard
find_src_files= $(wildcard $(addsuffix /$(strip $(2)), $(foreach dir, $(1), $(addprefix $(src_dir_cs)/, $(dir)))))

# function find_files wildcards
find_files= $(wildcard $(foreach dir, $(1), $(addprefix $(src_dir_cs)/, $(dir))))

relativepath= $(shell realpath --relative-to=$(strip $(1)) $(strip $(2)))

# retrieve all cs files
all_src_cs= $(call find_files, $(src_cs_wildcards))
all_dist= $(call find_files, $(src_dist_wildcards))
# rewrite sokgo.dll.config only if it does not exist in dest dir (pkgdatadir)
all_dll_target= $(addprefix $(dll_dir_cs)/, $(TARGET) $(if $(wildcard $(pkgdatadir)/$(CONFIG)), , $(CONFIG)) $(RUNTIME_CONFIG))
all_bin_target= $(addprefix $(bin_dir_cs)/, $(TARGET) $(if $(wildcard $(pkgdatadir)/$(CONFIG)), , $(CONFIG)) $(RUNTIME_CONFIG))
dll_system_dlls= $(dll_dir_cs)/System.*.dll
bin_system_dlls= $(bin_dir_cs)/System.*.dll

update_build_id= $(src_dir_cs)/Properties/BuildId-update.sh
src_build_id_cs= $(src_dir_cs)/Properties/BuildId.cs
src_build_id_cs_in= $(src_dir_cs)/Properties/BuildId.cs.in

bin_SCRIPTS= $(bin_dir_cs)/$(PACKAGE)
EXTRA_SCRIPTS=

pkgdata_DATA= $(all_bin_target) $(wildcard $(bin_system_dlls))

EXTRA_DIST= $(all_dist)

CLEANFILES= $(pkgdata_DATA) $(bin_SCRIPTS)

do_subst= $(SED) -e 's,[@]pkgdatadir[@],$(pkgdatadir),g'		\
				 -e 's,[@]RUNTIME[@],$(RUNTIME),g'				\
				 -e 's,[@]PACKAGE[@],$(PACKAGE),g'				\
				 -e 's,[@]TARGET_EXT[@],$(TARGET_EXT),g'

define mkdir-dummy
	$(MKDIR_P) $(@D)
	@echo -n > $@
endef

$(tmp_dir_dummy) :
	$(mkdir-dummy)

$(bin_dir_dummy) :
	$(mkdir-dummy)

$(dll_dir_dummy) :
	$(mkdir-dummy)

$(prj_vs_dir_dummy) :
	$(mkdir-dummy)

$(prj) : $(prj_in) | $(prj_vs_dir_dummy)
	$(LN_S) -f $(call relativepath, $(@D), $<) $@

$(bin_dir_cs)/$(PACKAGE) : $(src_dir_cs)/$(PACKAGE).in Makefile | $(bin_dir_dummy)
	$(do_subst) < $< > $@
	chmod +x $@

$(all_dll_target) $(dll_system_dlls) &: $(all_src_cs) $(src_dir_cs)/app.config $(update_build_id) $(src_build_id_cs_in) $(prj) Makefile | $(dll_dir_dummy)
#	$(SHELL) $(update_build_id)
	$(DOTNET) build $(prj) $(DOTNET_FLAGS) -o $(dll_dir_cs)
	touch -c $(all_dll_target) $(dll_system_dlls)

$(all_bin_target) $(bin_system_dlls) &: $(all_dll_target) $(dll_system_dlls) | $(bin_dir_dummy)
	for f in $^; do							\
		cp -fupv $$f $(bin_dir_cs)/;		\
	done

install-data-hook :
	test -d "$(sysconfdir)" || $(MKDIR_P) "$(sysconfdir)"
	$(LN_S) -f $(pkgdatadir)/$(CONFIG) $(sysconfdir)/$(COMMON_CONFIG)
	chmod u+x $(pkgdatadir)/*.dll

uninstall-hook :
	rm -f $(sysconfdir)/$(COMMON_CONFIG)
