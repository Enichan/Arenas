﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    [DebuggerTypeProxy(typeof(UnmanagedRefDebugView<>))]
    [DebuggerDisplay("{HasValue ? ToString() : null}")]
    unsafe readonly public struct UnmanagedRef<T> where T : unmanaged {
        private readonly T* pointer;
        private readonly RefVersion version;
        private readonly Arena arena;
        private readonly int elementCount;

        public UnmanagedRef(T* pointer, Arena arena, RefVersion version, int elementCount) {
            this.pointer = pointer;
            this.arena = arena ?? throw new ArgumentNullException(nameof(arena));
            this.version = version;
            this.elementCount = elementCount;
        }

        public bool TryGetValue(out T* ptr) {
            ptr = Value;
            return ptr != null;
        }

        public SlimUnsafeRef<T> ToSlim() {
            return new SlimUnsafeRef<T>(pointer, version);
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

        public static explicit operator IntPtr(UnmanagedRef<T> uref) {
            return (IntPtr)uref.pointer;
        }

        public static explicit operator UnmanagedRef(UnmanagedRef<T> uref) {
            return new UnmanagedRef(typeof(T), (IntPtr)uref.pointer, uref.arena, uref.version, uref.elementCount);
        }

        public static explicit operator UnmanagedRef<T>(UnmanagedRef uref) {
            return new UnmanagedRef<T>((T*)(IntPtr)uref, uref.Arena, uref.Version, uref.ElementCount);
        }

        public static explicit operator SlimUnsafeRef<T>(UnmanagedRef<T> uref) {
            return new SlimUnsafeRef<T>(uref.pointer, uref.version);
        }

        public static explicit operator SlimUnsafeRef(UnmanagedRef<T> uref) {
            return new SlimUnsafeRef((IntPtr)uref.pointer, uref.version);
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
        public T* Value { get { return pointer != null && !arena.VersionsMatch(version, (IntPtr)pointer) ? null : pointer; } }
        public bool HasValue { get { return pointer != null && arena.VersionsMatch(version, (IntPtr)pointer); } }
        public RefVersion Version { get { return version; } }
        public int ElementCount { get { return elementCount; } }
        public int Size { get { return elementCount * sizeof(T); } }
    }

    [DebuggerTypeProxy(typeof(UnmanagedRefDebugView))]
    [DebuggerDisplay("{HasValue ? ToString() : null}")]
    unsafe readonly public struct UnmanagedRef {
        private readonly Type type;
        private readonly IntPtr pointer;
        private readonly RefVersion version;
        private readonly Arena arena;
        private readonly int elementCount;

        public UnmanagedRef(Type type, IntPtr pointer, Arena arena, RefVersion version, int elementCount) {
            this.type = type ?? throw new ArgumentNullException(nameof(type));
            this.pointer = pointer;
            this.arena = arena ?? throw new ArgumentNullException(nameof(arena));
            this.version = version;
            this.elementCount = elementCount;
        }

        public bool TryGetValue(out IntPtr ptr) {
            ptr = Value;
            return ptr != IntPtr.Zero;
        }

        public SlimUnsafeRef ToSlim() {
            return new SlimUnsafeRef(pointer, version);
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

            var cur = pointer;
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

        public static explicit operator IntPtr(UnmanagedRef uref) {
            return uref.pointer;
        }

        public static explicit operator SlimUnsafeRef(UnmanagedRef uref) {
            return new SlimUnsafeRef(uref.pointer, uref.version);
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
        public IntPtr Value { get { return pointer != IntPtr.Zero && !arena.VersionsMatch(version, pointer) ? IntPtr.Zero : pointer; } }
        public bool HasValue { get { return pointer != IntPtr.Zero && arena.VersionsMatch(version, pointer); } }
        public RefVersion Version { get { return version; } }
        public int ElementCount { get { return elementCount; } }
        public int Size { get { return elementCount * Marshal.SizeOf(type); } }
    }
}
