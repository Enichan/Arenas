using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static Arenas.TypeHandle;
using static Arenas.TypeInfo;

namespace Arenas {
    [DebuggerTypeProxy(typeof(UnmanagedRefDebugView<>))]
    [DebuggerDisplay("{HasValue ? ToString() : null}")]
    [StructLayout(LayoutKind.Sequential)]
    unsafe readonly public struct UnmanagedRef<T> : IUnmanagedRef, IEquatable<UnmanagedRef<T>> where T : unmanaged {
        public readonly UnmanagedRef Reference;

        public UnmanagedRef(UnmanagedRef reference) {
            Reference = reference;
        }

        public UnmanagedRef(T* pointer, RefVersion version, int elementCount) {
            Reference = new UnmanagedRef((IntPtr)pointer, version, elementCount);
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

        public void CopyTo(UnmanagedRef<T> dest) {
            var elementCount = ElementCount;
            Reference.CopyTo(dest.Value, dest.ElementCount, 0, 0, elementCount, elementCount);
        }

        public void CopyTo(UnmanagedRef<T> dest, int destIndex) {
            var elementCount = ElementCount;
            Reference.CopyTo(dest.Value, dest.ElementCount, destIndex, 0, elementCount, elementCount);
        }

        public void CopyTo(UnmanagedRef<T> dest, int destIndex, int sourceIndex, int count) {
            var elementCount = ElementCount;
            Reference.CopyTo(dest.Value, dest.ElementCount, destIndex, sourceIndex, count, -1);
        }

        public void CopyTo(T* dest, int destLength) {
            var elementCount = ElementCount;
            Reference.CopyTo(dest, destLength, 0, 0, elementCount, elementCount);
        }

        public void CopyTo(T* dest, int destLength, int destIndex) {
            var elementCount = ElementCount;
            Reference.CopyTo(dest, destLength, destIndex, 0, elementCount, elementCount);
        }

        public void CopyTo(T* dest, int destLength, int destIndex, int sourceIndex, int count) {
            Reference.CopyTo(dest, destLength, destIndex, sourceIndex, count, -1);
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

        public override int GetHashCode() {
            return Reference.GetHashCode();
        }

        public bool Equals(UnmanagedRef other) {
            return Reference.Equals(other);
        }

        public static bool operator ==(UnmanagedRef<T> left, UnmanagedRef<T> right) {
            return left.Equals(right);
        }

        public static bool operator !=(UnmanagedRef<T> left, UnmanagedRef<T> right) {
            return !(left == right);
        }
        #endregion

        public static explicit operator IntPtr(UnmanagedRef<T> uref) {
            return (IntPtr)uref.Reference;
        }

        public static explicit operator UnmanagedRef(UnmanagedRef<T> uref) {
            return uref.Reference;
        }

        public static explicit operator UnmanagedRef<T>(UnmanagedRef uref) {
            return new UnmanagedRef<T>(uref);
        }

        public T* this[int index] {
            get {
                if (index < 0 || index >= ElementCount) {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
                return Value + index;
            }
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

                    // this is the slow path
                    return Size / sizeof(T);
                }
                return packedValue;
            }
        }

        public Arena Arena { get { return Reference.Arena; } }
        public T* Value { get { return (T*)Reference.Value; } }
        public bool HasValue { get { return Reference.HasValue; } }
        public RefVersion Version { get { return Reference.Version; } }
        public int Size { get { return Reference.Size; } }
        UnmanagedRef IUnmanagedRef.Reference { get { return Reference; } }
    }

    [DebuggerTypeProxy(typeof(UnmanagedRefDebugView))]
    [DebuggerDisplay("{HasValue ? ToString() : null}")]
    [StructLayout(LayoutKind.Sequential)]
    unsafe readonly public struct UnmanagedRef : IUnmanagedRef, IEquatable<UnmanagedRef> {
        private readonly BitpackedPtr pointer;
        private readonly RefVersion version;

        public UnmanagedRef(IntPtr pointer, RefVersion version, int elementCount) {
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
        public void CopyTo<T>(T[] dest) where T : unmanaged {
            var elementCount = ElementCount;
            CopyTo(dest, 0, 0, elementCount, elementCount);
        }

        public void CopyTo<T>(T[] dest, int destIndex) where T : unmanaged {
            var elementCount = ElementCount;
            CopyTo(dest, destIndex, 0, elementCount, elementCount);
        }

        public void CopyTo<T>(T[] dest, int destIndex, int sourceIndex, int count) where T : unmanaged {
            CopyTo(dest, destIndex, sourceIndex, count, -1);
        }

        public void CopyTo<T>(UnmanagedRef<T> dest) where T : unmanaged {
            var elementCount = ElementCount;
            CopyTo(dest.Value, dest.ElementCount, 0, 0, elementCount, elementCount);
        }

        public void CopyTo<T>(UnmanagedRef<T> dest, int destIndex) where T : unmanaged {
            var elementCount = ElementCount;
            CopyTo(dest.Value, dest.ElementCount, destIndex, 0, elementCount, elementCount);
        }

        public void CopyTo<T>(UnmanagedRef<T> dest, int destIndex, int sourceIndex, int count) where T : unmanaged {
            CopyTo(dest.Value, dest.ElementCount, destIndex, sourceIndex, count, -1);
        }

        public void CopyTo<T>(T* dest, int destLength) where T : unmanaged {
            var elementCount = ElementCount;
            CopyTo(dest, destLength, 0, 0, elementCount, elementCount);
        }

        public void CopyTo<T>(T* dest, int destLength, int destIndex) where T : unmanaged {
            var elementCount = ElementCount;
            CopyTo(dest, destLength, destIndex, 0, elementCount, elementCount);
        }

        public void CopyTo<T>(T* dest, int destLength, int destIndex, int sourceIndex, int count) where T : unmanaged {
            CopyTo(dest, destLength, destIndex, sourceIndex, count, -1);
        }

        internal void CopyTo<T>(T[] dest, int destIndex, int sourceIndex, int count, int elementCount, Type type = null) where T : unmanaged {
            if (dest is null) {
                throw new ArgumentNullException(nameof(dest));
            }
            fixed (T* destPtr = dest) {
                CopyTo(destPtr, dest.Length, destIndex, sourceIndex, count, elementCount, type);
            }
        }

        internal void CopyTo<T>(T* dest, int destLength, int destIndex, int sourceIndex, int count, int elementCount, Type type = null) where T : unmanaged {
            if (!HasValue) {
                throw new NullReferenceException($"Error in UnmanagedRef.CopyTo: HasValue was false");
            }
            if (destLength < 0) {
                throw new ArgumentOutOfRangeException(nameof(destLength));
            }
            if (count < 0) {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            if (destIndex < 0 || destIndex + count > destLength) {
                throw new ArgumentOutOfRangeException(nameof(destIndex));
            }

            if (elementCount < 0) {
                elementCount = ElementCount;
            }

            if (sourceIndex < 0 || sourceIndex + count > elementCount) {
                throw new ArgumentOutOfRangeException(nameof(sourceIndex));
            }

            if (count == 0) {
                return;
            }

            if (dest == null) {
                throw new ArgumentNullException(nameof(dest));
            }

            type = type ?? Type;

            var info = GetTypeInfo(type);
            var elementSize = info.Size;
            var cur = RawUnsafePointer + elementSize * sourceIndex;

            for (int i = 0; i < count; i++) {
                dest[destIndex + i] = *(T*)cur;
                cur += elementSize;
            }
        }

        public T[] ToArray<T>() where T : unmanaged {
            if (!HasValue) {
                throw new NullReferenceException($"Error in UnmanagedRef.ToArray: HasValue was false");
            }

            var elementCount = ElementCount;
            var items = new T[elementCount];
            CopyTo(items, 0, 0, elementCount, elementCount);
            return items;
        }

        public object[] ToArray() {
            if (!HasValue) {
                throw new NullReferenceException($"Error in UnmanagedRef.ToArray: HasValue was false");
            }

            var elementCount = ElementCount;
            var items = new object[elementCount];

            var info = GetTypeInfo(TypeHandle);
            var elementSize = info.Size;
            var cur = RawUnsafePointer;

            for (int i = 0; i < elementCount; i++) {
                items[i] = info.PtrToStruct(cur);
                cur += elementSize;
            }

            return items;
        }
        #endregion

        public override string ToString() {
            var elementCount = ElementCount;
            if (elementCount > 1) {
                return $"UnmanagedRef(Type={Type}, ElementCount={elementCount})";
            }

            var ptr = Value;
            if (ptr == IntPtr.Zero) {
                return string.Empty;
            }

            TypeInfo info;
            if (!TryGetTypeInfo(TypeHandle, out info)) {
                return $"UnmanagedRef(Type=<invalid>, ElementCount={elementCount})";
            }

            return info.ToString(ptr);
        }

        public static explicit operator IntPtr(UnmanagedRef uref) {
            return uref.Value;
        }

        public T* As<T>() where T : unmanaged {
            if (Type != typeof(T)) {
                return null;
            }
            return (T*)Value;
        }

        #region Equality
        public override bool Equals(object obj) {
            return obj is UnmanagedRef @ref &&
                EqualityComparer<BitpackedPtr>.Default.Equals(pointer, @ref.pointer) &&
                version.Equals(@ref.version);
        }

        public bool Equals(UnmanagedRef other) {
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

        public static bool operator ==(UnmanagedRef left, UnmanagedRef right) {
            return left.Equals(right);
        }

        public static bool operator !=(UnmanagedRef left, UnmanagedRef right) {
            return !(left == right);
        }
        #endregion

        //public IntPtr this[int index] {
        //    get {
        //        if (index < 0 || index >= ElementCount) {
        //            throw new ArgumentOutOfRangeException(nameof(index));
        //        }
        //        return Value + index;
        //    }
        //}

        public Type Type { 
            get {
                if (!HasValue) {
                    return null;
                }
                Type type;
                if (!TryGetTypeFromHandle(Arena.ItemHeader.GetTypeHandle(pointer.Value), out type)) {
                    return null;
                }
                return type; 
            } 
        }

        public TypeHandle TypeHandle {
            get {
                if (!HasValue) {
                    return TypeHandle.None;
                }
                return Arena.ItemHeader.GetTypeHandle(pointer.Value);
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

                var packedValue = pointer.PackedValue;
                if (packedValue == 0) {
                    if (version.Item.HasElementCount) {
                        return version.Item.ElementCount;
                    }

                    // this is the slow path
                    var handle = TypeHandle;
                    TypeInfo info;
                    
                    if (!TryGetTypeInfo(handle, out info)) {
                        return 0;
                    }

                    return Size / info.Size;
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

        public RefVersion Version { get { return version; } }
        public IntPtr RawUnsafePointer { get { return pointer.Value; } }

        internal int PointerPackedValue { get { return pointer.PackedValue; } }
        UnmanagedRef IUnmanagedRef.Reference { get { return this; } }
    }
}
