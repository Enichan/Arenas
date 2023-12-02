using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Arenas {
    [DebuggerTypeProxy(typeof(UnmanagedRefDebugView<>))]
    [DebuggerDisplay("{HasValue ? ToString() : null}")]
    [StructLayout(LayoutKind.Sequential)]
    unsafe readonly public struct UnsafeRef<T> : IUnmanagedRef, IEquatable<UnsafeRef<T>> where T : unmanaged {
        public readonly UnsafeRef Reference;

        public UnsafeRef(T* pointer, RefVersion version) {
            Reference = new UnsafeRef((IntPtr)pointer, version);
        }

        public UnsafeRef(UnsafeRef reference) {
            Reference = reference;
        }

        public bool TryGetValue(out T* ptr) {
            ptr = Value;
            return ptr != null;
        }

        public UnmanagedRef<T> ToUnmanaged() {
            var arena = Arena;
            if (arena is null) {
                throw new InvalidOperationException($"Cannot convert UnsafeRef<{typeof(T)}> to UnmanagedRef<{typeof(T)}>: not a valid reference (arena was null)");
            }
            return arena.UnmanagedRefFromPtr((T*)Reference.RawUnsafePointer);
        }

        UnmanagedRef IUnmanagedRef.ToUnmanaged() {
            var arena = Arena;
            if (arena is null) {
                throw new InvalidOperationException($"Cannot convert UnsafeRef<{typeof(T)}> to UnmanagedRef<{typeof(T)}>: not a valid reference (arena was null)");
            }
            return arena.UnmanagedRefFromPtr(Reference.RawUnsafePointer);
        }

        public override string ToString() {
            var ptr = Value;
            return ptr == null ? string.Empty : (*ptr).ToString();
        }

        #region Equality
        public override bool Equals(object obj) {
            return obj is UnsafeRef<T> @ref &&
                   Reference.Equals(@ref.Reference);
        }

        public bool Equals(UnsafeRef<T> other) {
            return Reference.Equals(other.Reference);
        }

        public override int GetHashCode() {
            return Reference.GetHashCode();
        }

        public bool Equals(UnsafeRef other) {
            return Reference.Equals(other);
        }

        public static bool operator ==(UnsafeRef<T> left, UnsafeRef<T> right) {
            return left.Equals(right);
        }

        public static bool operator !=(UnsafeRef<T> left, UnsafeRef<T> right) {
            return !(left == right);
        }
        #endregion

        public static explicit operator IntPtr(UnsafeRef<T> uref) {
            return uref.Reference.RawUnsafePointer;
        }

        public static explicit operator UnsafeRef(UnsafeRef<T> uref) {
            return uref.Reference;
        }

        public static explicit operator UnsafeRef<T>(UnsafeRef uref) {
            return new UnsafeRef<T>((T*)uref.RawUnsafePointer, uref.Version);
        }

        public Arena Arena { get { return Reference.Arena; } }
        public T* Value { get { return (T*)Reference.Value; } }
        public bool HasValue { get { return Reference.HasValue; } }
        public RefVersion Version { get { return Reference.Version; } }
        UnsafeRef IUnmanagedRef.Reference { get { return Reference; } }
    }

    [DebuggerTypeProxy(typeof(UnmanagedRefDebugView))]
    [DebuggerDisplay("{HasValue ? ToString() : null}")]
    [StructLayout(LayoutKind.Sequential)]
    unsafe readonly public struct UnsafeRef : IUnmanagedRef, IEquatable<UnsafeRef> {
        private readonly IntPtr pointer;
        private readonly RefVersion version;

        public UnsafeRef(IntPtr pointer, RefVersion version) {
            this.pointer = pointer;
            this.version = version;
        }

        public bool TryGetValue(out IntPtr ptr) {
            ptr = Value;
            return ptr != IntPtr.Zero;
        }

        public UnmanagedRef ToUnmanaged() {
            var arena = Arena;
            if (arena is null) {
                throw new InvalidOperationException($"Cannot convert UnsafeRef to UnmanagedRef: not a valid reference (arena was null)");
            }
            return arena.UnmanagedRefFromPtr(pointer);
        }

        public UnmanagedRef<T> ToUnmanaged<T>() where T : unmanaged {
            var arena = Arena;
            if (arena is null) {
                throw new InvalidOperationException($"Cannot convert UnsafeRef to UnmanagedRef<{typeof(T)}>: not a valid reference (arena was null)");
            }
            return arena.UnmanagedRefFromPtr<T>(pointer);
        }

        public override string ToString() {
            var ptr = Value;
            if (ptr == IntPtr.Zero) {
                return string.Empty;
            }

            var type = Type;
            if (type is null) {
                return string.Empty;
            }

            var inst = Marshal.PtrToStructure(Value, Type);
            return inst.ToString();
        }

        public static explicit operator IntPtr(UnsafeRef uref) {
            return uref.pointer;
        }

        public T* As<T>() where T : unmanaged {
            if (Type != typeof(T)) {
                return null;
            }
            return (T*)Value;
        }

        #region Equality
        public override bool Equals(object obj) {
            return obj is UnsafeRef @ref &&
                EqualityComparer<IntPtr>.Default.Equals(pointer, @ref.pointer) &&
                version.Equals(@ref.version);
        }

        public bool Equals(UnsafeRef other) {
            return
                EqualityComparer<IntPtr>.Default.Equals(pointer, other.pointer) &&
                version.Equals(other.version);
        }

        public override int GetHashCode() {
            int hashCode = 598475582;
            hashCode = hashCode * -1521134295 + pointer.GetHashCode();
            hashCode = hashCode * -1521134295 + version.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(UnsafeRef left, UnsafeRef right) {
            return left.Equals(right);
        }

        public static bool operator !=(UnsafeRef left, UnsafeRef right) {
            return !(left == right);
        }
        #endregion

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
                return Arena.Get(version.Arena);
            } 
        }

        public IntPtr Value { 
            get {
                var arena = Arena;
                if (arena is null) {
                    return IntPtr.Zero;
                }
                return pointer != IntPtr.Zero && !arena.VersionsMatch(version, pointer) ? IntPtr.Zero : pointer; 
            } 
        }

        public bool HasValue { 
            get {
                var arena = Arena;
                if (arena is null) {
                    return false;
                }
                return pointer != IntPtr.Zero && arena.VersionsMatch(version, pointer); 
            } 
        }

        public RefVersion Version { get { return version; } }
        public IntPtr RawUnsafePointer { get { return pointer; } }
        UnsafeRef IUnmanagedRef.Reference { get { return this; } }
    }
}
