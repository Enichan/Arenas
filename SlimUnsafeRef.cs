using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Arenas {
    [DebuggerTypeProxy(typeof(UnmanagedRefDebugView<>))]
    [DebuggerDisplay("{HasValue ? ToString() : null}")]
    unsafe readonly public struct SlimUnsafeRef<T> where T : unmanaged {
        private readonly T* pointer;
        private readonly RefVersion version;

        public SlimUnsafeRef(T* pointer, RefVersion version) {
            this.pointer = pointer;
            this.version = version;
        }

        public bool TryGetValue(out T* ptr) {
            ptr = Value;
            return ptr != null;
        }

        public UnmanagedRef<T> ToUnmanaged() {
            var arena = Arena;
            if (arena == null) {
                throw new InvalidOperationException($"Cannot convert SlimUnsafeRef<{typeof(T)}> to UnmanagedRef<{typeof(T)}>: not a valid reference (arena was null)");
            }
            return arena.UnmanagedRefFromPtr<T>(pointer);
        }

        public override string ToString() {
            var ptr = Value;
            return ptr == null ? string.Empty : (*ptr).ToString();
        }

        public static explicit operator IntPtr(SlimUnsafeRef<T> uref) {
            return (IntPtr)uref.pointer;
        }

        public static explicit operator SlimUnsafeRef(SlimUnsafeRef<T> uref) {
            return new SlimUnsafeRef((IntPtr)uref.pointer, uref.version);
        }

        public static explicit operator SlimUnsafeRef<T>(SlimUnsafeRef uref) {
            return new SlimUnsafeRef<T>((T*)(IntPtr)uref, uref.Version);
        }

        public Arena Arena {
            get {
                return Arena.Get(Arena.ItemHeader.GetArenaID((IntPtr)pointer));
            }
        }

        public T* Value {
            get {
                var arena = Arena;
                if (arena == null) {
                    return null;
                }
                return !arena.VersionsMatch(version, (IntPtr)pointer) ? null : pointer;
            }
        }

        public bool HasValue {
            get {
                var arena = Arena;
                if (arena == null) {
                    return false;
                }
                return arena.VersionsMatch(version, (IntPtr)pointer) && pointer != null;
            }
        }

        public RefVersion Version { get { return version; } }
    }

    [DebuggerTypeProxy(typeof(UnmanagedRefDebugView))]
    [DebuggerDisplay("{HasValue ? ToString() : null}")]
    unsafe readonly public struct SlimUnsafeRef {
        private readonly IntPtr pointer;
        private readonly RefVersion version;

        public SlimUnsafeRef(IntPtr pointer, RefVersion version) {
            this.pointer = pointer;
            this.version = version;
        }

        public bool TryGetValue(out IntPtr ptr) {
            ptr = Value;
            return ptr != IntPtr.Zero;
        }

        public UnmanagedRef ToUnmanaged() {
            var arena = Arena;
            if (arena == null) {
                throw new InvalidOperationException($"Cannot convert SlimUnsafeRef to UnmanagedRef: not a valid reference (arena was null)");
            }
            return arena.UnmanagedRefFromPtr(pointer);
        }

        public UnmanagedRef<T> ToUnmanaged<T>() where T : unmanaged {
            var arena = Arena;
            if (arena == null) {
                throw new InvalidOperationException($"Cannot convert SlimUnsafeRef to UnmanagedRef<{typeof(T)}>: not a valid reference (arena was null)");
            }
            return arena.UnmanagedRefFromPtr<T>(pointer);
        }

        public override string ToString() {
            var ptr = Value;
            if (ptr == IntPtr.Zero) {
                return string.Empty;
            }

            var type = Type;
            if (type == null) {
                return string.Empty;
            }

            var inst = Marshal.PtrToStructure(Value, Type);
            return inst.ToString();
        }

        public static explicit operator IntPtr(SlimUnsafeRef uref) {
            return uref.pointer;
        }

        public Type Type { 
            get {
                Type type;
                if (!Arena.TryGetTypeFromHandle(Arena.ItemHeader.GetTypeHandle(pointer), out type)) {
                    return null;
                }
                return type; 
            } 
        }

        public Arena Arena { 
            get {
                return Arena.Get(Arena.ItemHeader.GetArenaID(pointer));
            } 
        }

        public IntPtr Value { 
            get {
                var arena = Arena;
                if (arena == null) {
                    return IntPtr.Zero;
                }
                return !arena.VersionsMatch(version, pointer) ? IntPtr.Zero : pointer; 
            } 
        }

        public bool HasValue { 
            get {
                var arena = Arena;
                if (arena == null) {
                    return false;
                }
                return arena.VersionsMatch(version, pointer) && pointer != null; 
            } 
        }

        public RefVersion Version { get { return version; } }
    }
}
