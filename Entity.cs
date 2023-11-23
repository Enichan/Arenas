﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    [StructLayout(LayoutKind.Sequential)]
    unsafe public struct Entity : IArenaContents {
        public double X, Y, Z;

        private ManagedRef name;

        public Entity(double x, double y, double z) {
            X = x;
            Y = y;
            Z = z;
            name = new ManagedRef();
            ArenaID = Guid.Empty;
        }

        void IArenaContents.Free() {
            // free all managed pointers and release any unmanaged children that require freeing
            Name = null;
        }

        public override string ToString() {
            return $"{GetType().Name}({Name ?? "<unnamed>"}:{X},{Y},{Z})";
        }

        public string Name {
            get { return name.Get<string>(Arena.Get(ArenaID)); }
            set { name = name.Set(Arena.Get(ArenaID), value); }
        }

        public Guid ArenaID { get; private set; }
        Guid IArenaContents.ArenaID { get { return ArenaID; } set { ArenaID = value; } }
    }
}
