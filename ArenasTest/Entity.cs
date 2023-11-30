﻿using System;
using System.Runtime.InteropServices;

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
            ArenaID = ArenaID.Empty;
        }

        void IArenaContents.Free() {
            // free all managed pointers and release any unmanaged children that require freeing
            Name = null;
        }

        public override string ToString() {
            return $"{GetType().Name}({Name ?? "<unnamed>"}:{X},{Y},{Z})";
        }

        // managed references are routed through the arena using ManagedRef
        public string Name {
            get { return name.Get<string>(); }
            set { name = name.Set(Arena.Get(ArenaID), value); }
        }

        // yucky boilerplate :(
        public ArenaID ArenaID { get; private set; }
        void IArenaContents.SetArenaID(ArenaID value) { ArenaID = value; }
    }
}
