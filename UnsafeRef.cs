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

        public UnsafeRef(UnsafeRef reference) {
            Reference = reference;
        }

        public UnsafeRef(T* pointer, RefVersion version, int elementCount) {
            Reference = new UnsafeRef((IntPtr)pointer, version, elementCount);
        }

        public bool TryGetValue(out T* ptr) {
            ptr = Value;
            return ptr != null;
        }

        #region Copying
        public void CopyTo(T[] dest) {
            var elementCount = ElementCount;
            Reference.CopyTo(dest, 0, 0, elementCount, elementCount, typeof(T));
        }

        public void CopyTo(T[] dest, int destIndex) {
            var elementCount = ElementCount;
            Reference.CopyTo(dest, destIndex, 0, elementCount, elementCount, typeof(T));
        }

        public void CopyTo(T[] dest, int destIndex, int sourceIndex, int count) {
            var elementCount = ElementCount;
            Reference.CopyTo(dest, destIndex, sourceIndex, count, elementCount, typeof(T));
        }

        public T[] ToArray() {
            if (!HasValue) {
                throw new NullReferenceException($"Error in UnmanagedRef<{typeof(T)}>.ToArray: HasValue was false");
            }

            var elementCount = ElementCount;
            var items = new T[elementCount];
            Reference.CopyTo(items, 0, 0, elementCount, elementCount, typeof(T));
            return items;
        }
        #endregion

        public override string ToString() {
            var elementCount = ElementCount;
            if (elementCount > 1) {
                return $"UnsafeRef<{GetType().GenericTypeArguments[0].Name}>(ElementCount={elementCount})";
            }
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
            return new UnsafeRef<T>(uref);
        }

        public int ElementCount { 
            get {
                if (!HasValue) {
                    return 0;
                }

                var packedValue = Reference.PointerPackedValue;
                if (packedValue == 0) {
                    if (Reference.Version.Item.HasElementCount) {
                        return Reference.Version.Item.ElementCount;
                    }
                    return Size / sizeof(T);
                }
                return packedValue;
            }
        }

        public T* this[int index] { get { return (T*)Reference[index]; } }
        public Arena Arena { get { return Reference.Arena; } }
        public T* Value { get { return (T*)Reference.Value; } }
        public bool HasValue { get { return Reference.HasValue; } }
        public RefVersion Version { get { return Reference.Version; } }
        public int Size { get { return Reference.Size; } }
        UnsafeRef IUnmanagedRef.Reference { get { return Reference; } }
    }

    [DebuggerTypeProxy(typeof(UnmanagedRefDebugView))]
    [DebuggerDisplay("{HasValue ? ToString() : null}")]
    [StructLayout(LayoutKind.Sequential)]
    unsafe readonly public struct UnsafeRef : IUnmanagedRef, IEquatable<UnsafeRef> {
        private readonly BitpackedPtr pointer;
        private readonly RefVersion version;

        public UnsafeRef(IntPtr pointer, RefVersion version, int elementCount) {
            if (elementCount > 0 && elementCount <= BitpackedPtr.LowerMask) {
                this.pointer = new BitpackedPtr(pointer, elementCount);
            }
            else {
                this.pointer = new BitpackedPtr(pointer, 0);
            }
            this.version = version;
        }

        public bool TryGetValue(out IntPtr ptr) {
            ptr = Value;
            return ptr != IntPtr.Zero;
        }

        #region Copying
        public void CopyTo<T>(T[] dest) {
            var elementCount = ElementCount;
            CopyTo(dest, 0, 0, elementCount, elementCount);
        }

        public void CopyTo<T>(T[] dest, int destIndex) {
            var elementCount = ElementCount;
            CopyTo(dest, destIndex, 0, elementCount, elementCount);
        }

        public void CopyTo<T>(T[] dest, int destIndex, int sourceIndex, int count) {
            CopyTo(dest, destIndex, sourceIndex, count, -1);
        }

        internal void CopyTo<T>(T[] dest, int destIndex, int sourceIndex, int count, int elementCount, Type type = null) {
            if (dest is null) {
                throw new ArgumentNullException(nameof(dest));
            }
            if (!HasValue) {
                throw new NullReferenceException($"Error in UnmanagedRef.CopyTo: HasValue was false");
            }
            if (destIndex < 0 || destIndex + count > dest.Length) {
                throw new ArgumentOutOfRangeException(nameof(destIndex));
            }

            if (elementCount < 0) {
                elementCount = ElementCount;
            }

            if (sourceIndex < 0 || sourceIndex + count > elementCount) {
                throw new ArgumentOutOfRangeException(nameof(sourceIndex));
            }

            type = type ?? Type;
            var cur = RawUnsafePointer;
            var elementSize = Marshal.SizeOf(type);

            for (int i = 0; i < elementCount; i++) {
                dest[i] = (T)Marshal.PtrToStructure(cur, type);
                cur += elementSize;
            }
        }

        public T[] ToArray<T>() {
            if (!HasValue) {
                throw new NullReferenceException($"Error in UnmanagedRef.ToArray: HasValue was false");
            }

            var elementCount = ElementCount;
            var items = new T[elementCount];
            CopyTo(items, 0, 0, elementCount, elementCount);
            return items;
        }
        #endregion

        public override string ToString() {
            var elementCount = ElementCount;
            if (elementCount > 1) {
                return $"UnsafeRef(Type={Type}, ElementCount={elementCount})";
            }

            var ptr = Value;
            if (ptr == IntPtr.Zero) {
                return string.Empty;
            }

            var inst = Marshal.PtrToStructure(Value, Type);
            return inst.ToString();
        }

        public static explicit operator IntPtr(UnsafeRef uref) {
            return uref.pointer.Value;
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
                EqualityComparer<BitpackedPtr>.Default.Equals(pointer, @ref.pointer) &&
                version.Equals(@ref.version);
        }

        public bool Equals(UnsafeRef other) {
            return
                EqualityComparer<BitpackedPtr>.Default.Equals(pointer, other.pointer) &&
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

        public IntPtr this[int index] {
            get {
                if (index < 0 || index >= ElementCount) {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
                return Value + index;
            }
        }

        public Type Type { 
            get {
                if (!HasValue) {
                    return null;
                }
                Type type;
                if (!Arena.TryGetTypeFromHandle(Arena.ItemHeader.GetTypeHandle(pointer.Value), out type)) {
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
                var ptr = pointer.Value;
                return ptr != IntPtr.Zero && !arena.VersionsMatch(version, ptr) ? IntPtr.Zero : ptr; 
            } 
        }

        public bool HasValue { 
            get {
                var arena = Arena;
                if (arena is null) {
                    return false;
                }
                var ptr = pointer.Value;
                return ptr != IntPtr.Zero && arena.VersionsMatch(version, ptr); 
            } 
        }

        public int ElementCount {
            get {
                if (!HasValue) {
                    return 0;
                }

                var packedValue = PointerPackedValue;
                if (packedValue == 0) {
                    if (version.Item.HasElementCount) {
                        return version.Item.ElementCount;
                    }
                    return Size / Marshal.SizeOf(Type);
                }
                return packedValue;
            }
        }

        public int Size {
            get {
                var ptr = pointer.Value;
                var arena = Arena;
                if (arena is null || !arena.VersionsMatch(version, ptr)) {
                    return 0;
                }
                return Arena.ItemHeader.GetSize(ptr);
            }
        }

        internal int PointerPackedValue { get { return pointer.PackedValue; } }
        public RefVersion Version { get { return version; } }
        public IntPtr RawUnsafePointer { get { return pointer.Value; } }
        UnsafeRef IUnmanagedRef.Reference { get { return this; } }
    }
}
