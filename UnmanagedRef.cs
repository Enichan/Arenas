using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    [DebuggerTypeProxy(typeof(UnmanagedRefDebugView<>))]
    [DebuggerDisplay("{HasValue ? ToString() : null}")]
    unsafe readonly public struct UnmanagedRef<T> : IUnmanagedRef, IEquatable<UnmanagedRef<T>> where T : unmanaged {
        public readonly UnsafeRef Reference;
        private readonly Arena arena;
        private readonly int elementCount;

        public UnmanagedRef(T* pointer, Arena arena, RefVersion version, int elementCount) {
            this.Reference = new UnsafeRef((IntPtr)pointer, version);
            this.arena = arena ?? throw new ArgumentNullException(nameof(arena));
            this.elementCount = elementCount;
        }

        public bool TryGetValue(out T* ptr) {
            ptr = Value;
            return ptr != null;
        }

        public void CopyTo(T[] dest) {
            CopyTo(dest, 0, 0, elementCount);
        }

        public void CopyTo(T[] dest, int destIndex) {
            CopyTo(dest, destIndex, 0, elementCount);
        }

        public void CopyTo(T[] dest, int destIndex, int sourceIndex, int count) {
            if (dest is null) {
                throw new ArgumentNullException(nameof(dest));
            }
            if (!HasValue) {
                throw new NullReferenceException($"Error in UnmanagedRef<{GetType().GenericTypeArguments[0].Name}>.CopyTo: HasValue was false");
            }
            if (destIndex < 0 || destIndex + count > dest.Length) {
                throw new ArgumentOutOfRangeException(nameof(destIndex));
            }
            if (sourceIndex < 0 || sourceIndex + count > elementCount) {
                throw new ArgumentOutOfRangeException(nameof(sourceIndex));
            }

            var pointer = (T*)Reference.RawUnsafePointer;
            for (int i = 0; i < count; i++) {
                dest[destIndex + i] = pointer[sourceIndex + i];
            }
        }

        public T[] ToArray() {
            if (!HasValue) {
                throw new NullReferenceException($"Error in UnmanagedRef<{GetType().GenericTypeArguments[0].Name}>.ToArray: HasValue was false");
            }

            var items = new T[elementCount];
            CopyTo(items, 0, 0, elementCount);
            return items;
        }

        public override string ToString() {
            if (elementCount > 1) {
                return $"UnmanagedRef<{GetType().GenericTypeArguments[0].Name}>(ElementCount={elementCount})";
            }
            var ptr = Value;
            return ptr == null ? string.Empty : (*ptr).ToString();
        }

        #region Equality
        public override bool Equals(object obj) {
            return obj is UnmanagedRef<T> @ref &&
                   Reference.Equals(@ref.Reference);
        }

        public bool Equals(UnmanagedRef<T> other) {
            return Reference.Equals(other.Reference);
        }

        public bool Equals(UnsafeRef other) {
            return Reference.Equals(other);
        }

        public override int GetHashCode() {
            return Reference.GetHashCode();
        }

        public static bool operator ==(UnmanagedRef<T> left, UnmanagedRef<T> right) {
            return left.Equals(right);
        }

        public static bool operator !=(UnmanagedRef<T> left, UnmanagedRef<T> right) {
            return !(left == right);
        }
        #endregion

        public static explicit operator IntPtr(UnmanagedRef<T> uref) {
            return uref.Reference.RawUnsafePointer;
        }

        public static explicit operator UnmanagedRef(UnmanagedRef<T> uref) {
            return new UnmanagedRef(typeof(T), uref.Reference.RawUnsafePointer, uref.arena, uref.Reference.Version, uref.elementCount);
        }

        public static explicit operator UnmanagedRef<T>(UnmanagedRef uref) {
            return new UnmanagedRef<T>((T*)(IntPtr)uref, uref.Arena, uref.Version, uref.ElementCount);
        }

        public static explicit operator UnsafeRef<T>(UnmanagedRef<T> uref) {
            return new UnsafeRef<T>(uref.Reference);
        }

        public static explicit operator UnsafeRef(UnmanagedRef<T> uref) {
            return uref.Reference;
        }

        public T* this[int index] {
            get {
                if (index < 0 || index >= ElementCount) {
                    throw new IndexOutOfRangeException();
                }
                return Value + index;
            }
        }

        public Arena Arena { get { return arena; } }
        public T* Value { get { return Reference.RawUnsafePointer != null && !arena.VersionsMatch(Reference.Version, Reference.RawUnsafePointer) ? null : (T*)Reference.RawUnsafePointer; } }
        public bool HasValue { get { return Reference.RawUnsafePointer != null && arena.VersionsMatch(Reference.Version, Reference.RawUnsafePointer); } }
        public RefVersion Version { get { return Reference.Version; } }
        public int ElementCount { get { return elementCount; } }
        public int Size { get { return elementCount * sizeof(T); } }
        UnsafeRef IUnmanagedRef.Reference { get { return Reference; } }
    }

    [DebuggerTypeProxy(typeof(UnmanagedRefDebugView))]
    [DebuggerDisplay("{HasValue ? ToString() : null}")]
    unsafe readonly public struct UnmanagedRef : IUnmanagedRef, IEquatable<UnmanagedRef> {
        private readonly Type type;
        public readonly UnsafeRef Reference;
        private readonly Arena arena;
        private readonly int elementCount;

        public UnmanagedRef(Type type, IntPtr pointer, Arena arena, RefVersion version, int elementCount) {
            this.type = type ?? throw new ArgumentNullException(nameof(type));
            this.Reference = new UnsafeRef(pointer, version);
            this.arena = arena ?? throw new ArgumentNullException(nameof(arena));
            this.elementCount = elementCount;
        }

        public bool TryGetValue(out IntPtr ptr) {
            ptr = Value;
            return ptr != IntPtr.Zero;
        }

        public void CopyTo<T>(T[] dest) {
            CopyTo(dest, 0, 0, elementCount);
        }

        public void CopyTo<T>(T[] dest, int destIndex) {
            CopyTo(dest, destIndex, 0, elementCount);
        }

        public void CopyTo<T>(T[] dest, int destIndex, int sourceIndex, int count) {
            if (dest is null) {
                throw new ArgumentNullException(nameof(dest));
            }
            if (!HasValue) {
                throw new NullReferenceException($"Error in UnmanagedRef.CopyTo: HasValue was false");
            }
            if (destIndex < 0 || destIndex + count > dest.Length) {
                throw new ArgumentOutOfRangeException(nameof(destIndex));
            }
            if (sourceIndex < 0 || sourceIndex + count > elementCount) {
                throw new ArgumentOutOfRangeException(nameof(sourceIndex));
            }

            var cur = Reference.RawUnsafePointer;
            var elementSize = Marshal.SizeOf(Type);

            for (int i = 0; i < elementCount; i++) {
                dest[i] = (T)Marshal.PtrToStructure(cur, Type);
                cur += elementSize;
            }
        }

        public T[] ToArray<T>() {
            if (!HasValue) {
                throw new NullReferenceException($"Error in UnmanagedRef.ToArray: HasValue was false");
            }

            var items = new T[elementCount];
            CopyTo(items, 0, 0, elementCount);
            return items;
        }

        public override string ToString() {
            if (elementCount > 1) {
                return $"UnmanagedRef(Type={Type}, ElementCount={elementCount})";
            }

            var ptr = Value;
            if (ptr == IntPtr.Zero) {
                return string.Empty;
            }

            var inst = Marshal.PtrToStructure(Value, Type);
            return inst.ToString();
        }

        #region Equality
        public override bool Equals(object obj) {
            return obj is UnmanagedRef @ref &&
                   Reference.Equals(@ref.Reference);
        }

        public bool Equals(UnmanagedRef other) {
            return Reference.Equals(other.Reference);
        }

        public bool Equals(UnsafeRef other) {
            return Reference.Equals(other);
        }

        public override int GetHashCode() {
            return Reference.GetHashCode();
        }

        public static bool operator ==(UnmanagedRef left, UnmanagedRef right) {
            return left.Equals(right);
        }

        public static bool operator !=(UnmanagedRef left, UnmanagedRef right) {
            return !(left == right);
        }
        #endregion

        public static explicit operator IntPtr(UnmanagedRef uref) {
            return uref.Reference.RawUnsafePointer;
        }

        public static explicit operator UnsafeRef(UnmanagedRef uref) {
            return uref.Reference;
        }

        public T* As<T>() where T : unmanaged {
            if (Type != typeof(T)) {
                return null;
            }
            return (T*)Value;
        }

        public IntPtr this[int index] {
            get {
                if (index < 0 || index >= ElementCount) {
                    throw new IndexOutOfRangeException();
                }
                return Value + index;
            }
        }

        public Type Type { get { return type; } }
        public Arena Arena { get { return arena; } }
        public IntPtr Value { get { return Reference.RawUnsafePointer != IntPtr.Zero && !arena.VersionsMatch(Reference.Version, Reference.RawUnsafePointer) ? IntPtr.Zero : Reference.RawUnsafePointer; } }
        public bool HasValue { get { return Reference.RawUnsafePointer != IntPtr.Zero && arena.VersionsMatch(Reference.Version, Reference.RawUnsafePointer); } }
        public RefVersion Version { get { return Reference.Version; } }
        public int ElementCount { get { return elementCount; } }
        public int Size { get { return elementCount * Marshal.SizeOf(type); } }
        UnsafeRef IUnmanagedRef.Reference { get { return Reference; } }
    }
}
