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
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Sokgo.Port
{

	class PortRange : IEnumerable<ushort>
	{

		// consts
		protected const ushort DEFAULT_PORT_RANGE_MIN	= 32768;
		protected const ushort DEFAULT_PORT_RANGE_MAX	= 65535;
		protected const int GENERATE_MAX_DURATION		= 24*3600;		// seconds

		// inner class(es)/struct(s)
		protected class Enumerator : IEnumerator<ushort>
		{

			// data members
			protected PortRange m_range;
			protected int m_current= -1;
			protected int m_first= 0;
			protected int m_count= 0;

			// constructors
			public Enumerator(PortRange range, int firstIndex, int count)
			{
				m_range= range;
				m_first= firstIndex;
				m_count= count;
				Reset();
			}

			// IEnumerator<ushort> implementation
			public ushort Current => GetCurrent();

			object IEnumerator.Current => GetCurrent();

			public void Dispose()
			{
			}

			public bool MoveNext()
			{
				return ((++m_current) < m_count);
			}

			public void Reset()
			{
				m_current= -1;
			}

			// internal methods
			protected ushort GetCurrent()
			{
				return m_range.At(m_first + m_current);
			}
		}

		// data members
		protected ushort m_portRangeMin 	= DEFAULT_PORT_RANGE_MIN;
		protected ushort m_portRangeMax 	= DEFAULT_PORT_RANGE_MAX;
		protected ushort[] m_ports;
		protected DateTime m_dtLastGenerate;
		protected Random m_random= new Random();
		protected IList<WeakReference<Enumerator>> m_enumerators= new List<WeakReference<Enumerator>>();

		// constructor(s)
		public PortRange()
		{
			Generate();
		}

		public PortRange(ushort portRangeMin, ushort portRangeMax)
		{
			m_portRangeMin= portRangeMin;
			m_portRangeMax= portRangeMax;
			Generate();
		}

		// properties
		public int Count
		{
			get
			{
				lock (this)
				{
					return (!CS_IsEmpty()) ? m_ports.Length : 0;
				}
			}
		}

		// method(s)
		public bool IsEmpty()
		{
			lock (this)
			{
				return CS_IsEmpty();
			}
		}

		public ushort At(int index)
		{
			lock (this)
			{
				if (CS_IsEmpty() || (index < 0))
					return 0x0000;

				return m_ports[index % m_ports.Length];
			}
		}

		// IEnumerator<ushort> implementation
		public IEnumerator<ushort> GetEnumerator()
		{
			return CreateEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return CreateEnumerator();
		}

		// internal method(s)
		protected IEnumerator<ushort> CreateEnumerator()
		{
			lock (this)
			{
				if (!CS_HasEnumerators())
				{
					TimeSpan dt= DateTime.Now - m_dtLastGenerate;
					if (dt.TotalSeconds > GENERATE_MAX_DURATION)
						CS_Generate();
				}

				Enumerator e= new Enumerator(this, m_random.Next(Count), Count);
				m_enumerators.Add(new WeakReference<Enumerator>(e));
				return e;
			}
		}

		protected void Generate()
		{
			lock (this)
			{
				CS_Generate();
			}
		}

		protected int CountEnumerators()
		{
			lock (this)
			{
				return CS_CountEnumerators();
			}
		}

		protected bool HasEnumerators()
		{
			lock (this)
			{
				return (CS_CountEnumerators() != 0);
			}
		}

		// always called inside the critical section : lock(this)
		protected void CS_Generate()
		{
			int portCount = (int)m_portRangeMax - m_portRangeMin + 1;
			IList<ushort> ports= new List<ushort>(portCount);
			for (int i= 0; i < portCount; i++)
			{
				ports.Add((ushort)(m_portRangeMin + i));
			}

			// shuffle
			for (int i= 0; i < portCount; i++)
			{
				int j= m_random.Next(portCount);
				if (i != j)
				{
					ushort tmp = ports[i];
					ports[i] = ports[j];
					ports[j] = tmp;
				}
			}

			m_ports= ports.ToArray();
			m_dtLastGenerate= DateTime.Now;
		}

		// always called inside the critical section : lock(this)
		protected int CS_CountEnumerators()
		{
			if (m_enumerators.Count == 0)
				return 0;

			// strip all discarded enumerators
			IList<WeakReference<Enumerator>> stripEnumerators= new List<WeakReference<Enumerator>>(m_enumerators.Count);
			foreach (WeakReference<Enumerator> we in m_enumerators)
			{
				Enumerator e;
				if (we.TryGetTarget(out e))
				{
					stripEnumerators.Add(we);
				}
			}
			m_enumerators= stripEnumerators;
			return m_enumerators.Count;
		}

		// always called inside the critical section : lock(this)
		protected bool CS_HasEnumerators()
		{
			return (CS_CountEnumerators() != 0);
		}

		// always called inside the critical section : lock(this)
		protected bool CS_IsEmpty()
		{
			return ((m_ports == null) || (m_ports.Length == 0));
		}

	}
}