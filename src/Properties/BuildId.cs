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

// GENERATED FILE - DO NOT EDIT
// BuildId.cs
// Sun, 15 Sep 2024 16:33:25 +0200

public class BuildId
{
	protected const string GIT_HASH 		= "";	// DO NOT MODIFY THIS LINE
	protected const string SVN_REV 			= "";											// DO NOT MODIFY THIS LINE
	protected const int SHORT_GIT_HASH_LEN	= 8;

	public static string GitHash
	{
		get => GIT_HASH.ToLower();
	}
	public static string SvnRev
	{
		get => (!string.IsNullOrEmpty(SVN_REV)) ? "r" + SVN_REV : "";
	}

	public static string Tag
	{
		get
		{
			string githash= GitHash;
			return (!string.IsNullOrEmpty(githash)) ? githash : SvnRev;
		}
	}
	public static string ShortTag
	{
		get
		{
			string githash= GitHash;
			if (!string.IsNullOrEmpty(githash))
				return (githash.Length > SHORT_GIT_HASH_LEN) ? githash.Substring(0, SHORT_GIT_HASH_LEN) : githash;
			return SvnRev;
		}
	}
}
