using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Arenas {
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct TypeHandle : IEquatable<TypeHandle> {
        public readonly int Value;

        public TypeHandle(int value) {
            Value = value;
        }

        #region Equality
        public override bool Equals(object obj) {
            return obj is TypeHandle handle &&
                    Value == handle.Value;
        }

        public bool Equals(TypeHandle other) {
            return Value == other.Value;
        }

        public override int GetHashCode() {
            return 1909215196 + Value.GetHashCode();
        }

        public static bool operator ==(TypeHandle left, TypeHandle right) {
            return left.Equals(right);
        }

        public static bool operator !=(TypeHandle left, TypeHandle right) {
            return !(left == right);
        }

        public override string ToString() {
            return GetTypeFromHandle(this).ToString();
        }
        #endregion

        public bool HasValue { get { return Value != 0; } }
        public static TypeHandle None { get { return default; } }

        #region Static
        private static Dictionary<Type, TypeHandle> typeToHandle;
        private static Dictionary<TypeHandle, Type> handleToType;
        private static object typeHandleLock;

        static TypeHandle() {
            typeToHandle = new Dictionary<Type, TypeHandle>();
            handleToType = new Dictionary<TypeHandle, Type>();
            typeHandleLock = new object();
        }

        public static TypeHandle GetTypeHandle(Type type) {
            if (type == typeof(Exception)) {
                throw new ArgumentException("Cannot pass typeof(Exception) to TypeHandle.GetTypeHandle", nameof(type));
            }

            TypeHandle handle;
            lock (typeHandleLock) {
                if (!typeToHandle.TryGetValue(type, out handle)) {
                    typeToHandle[type] = handle = new TypeHandle(typeToHandle.Count + 1);
                    if (!handle.HasValue) {
                        throw new OverflowException("Arena.TypeHandle value overflow: too many types");
                    }
                    handleToType[handle] = type;
                }
            }
            return handle;
        }

        /// <summary>
        /// This returns the Type instance for a TypeHandle or typeof(Exception) if no matching Type is found
        /// </summary>
        /// <param name="handle">Handle for a Type instance</param>
        /// <returns>The Type instance for this TypeHandle or typeof(Exception) if no matching Type is found</returns>
        public static Type GetTypeFromHandle(TypeHandle handle) {
            if (!handle.HasValue) {
                return typeof(Exception);
            }

            Type type;
            lock (typeHandleLock) {
                if (!handleToType.TryGetValue(handle, out type)) {
                    return typeof(Exception);
                }
            }
            return type;
        }

        public static bool TryGetTypeFromHandle(TypeHandle handle, out Type type) {
            type = null;
            if (!handle.HasValue) {
                return false;
            }

            lock (typeHandleLock) {
                if (!handleToType.TryGetValue(handle, out type)) {
                    return false;
                }
            }
            return true;
        }
        #endregion
    }
}
