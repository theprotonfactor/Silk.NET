// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Silk.NET.Core.Loader;

namespace Silk.NET.Core.Native
{
    /// <summary>
    /// A helper class that marshals between native and managed types.
    /// </summary>
    public static class SilkMarshal
    {
        /// <summary>
        /// Allocate a new BStr pointer.
        /// </summary>
        /// <param name="length">The length of the string pointer, in bytes.</param>
        /// <returns>A pointer to the created string.</returns>
        public static nint AllocBStr(int length) => Marshal.StringToBSTR(new('\0', length));

        /// <summary>
        /// Free a BStr pointer
        /// </summary>
        /// <param name="ptr">The pointer to be freed.</param>
        public static void FreeBStr(nint ptr) => Marshal.FreeBSTR(ptr);

        // Store the GlobalMemory instances so that on .NET 5 the pinned object heap isn't prematurely garbage collected
        // This means that the GlobalMemory is only freed when the user calls Free.
        private static readonly ConcurrentDictionary<nint, GlobalMemory> _marshalledMemory = new();

        // In addition, we should keep track of the memory we allocate dedicated to string arrays. If we don't, we won't
        // know to free the individual strings allocated within memory.
        private static readonly ConcurrentDictionary<GlobalMemory, int> _stringArrays = new();

        // Other kinds of GCHandle-pinned pointers may be passed into Free, like delegate pointers for example which
        // must have GCHandles allocated on older runtimes to avoid an ExecutionEngineException.
        // We should keep track of those.
        private static readonly ConcurrentDictionary<nint, GCHandle> _otherGCHandles = new();

        private static nint RegisterMemory(GlobalMemory memory) => (_marshalledMemory[memory.Handle] = memory).Handle;

        /// <summary>
        /// Allocates a block of global memory of the given size.
        /// </summary>
        /// <remarks>
        /// The allocated memory must be manually freed using <see cref="Free"/>.
        /// </remarks>
        /// <param name="length">The number of bytes to allocate.</param>
        /// <returns>The allocated bytes.</returns>
        public static nint Allocate(int length) => RegisterMemory(GlobalMemory.Allocate(length));

        /// <summary>
        /// Frees the unmanaged construct represented by this pointer.
        /// </summary>
        /// <param name="ptr">The item to free.</param>
        /// <returns>
        /// Whether the operation was successful or not. If false, the pointer likely didn't originate from a
        /// <see cref="SilkMarshal"/> method.
        /// </returns>
        public static bool Free(nint ptr)
        {
            var ret = _otherGCHandles.TryRemove(ptr, out var gcHandle);
            if (ret)
            {
                gcHandle.Free();
            }

            ret = _marshalledMemory.TryRemove(ptr, out var val);
            if (val is null)
            {
                return ret;
            }

            if (_stringArrays.TryRemove(val, out var numStrings))
            {
                var span = val.AsSpan<nint>();
                for (var i = 0; i < numStrings; i++)
                {
                    Free(span[i]);
                }
            }

            val.Dispose();
            return ret;
        }

        [DebuggerHidden]
        [DebuggerStepThrough]
        private static T ThrowInvalidEncoding<T>()
        {
            throw new ArgumentOutOfRangeException("encoding");
        }

        /// <summary>
        /// Gets a <see cref="GlobalMemory"/> object containing a copy of the input string marshalled per the specified
        /// native string encoding.
        /// </summary>
        /// <param name="input">The string to marshal.</param>
        /// <param name="encoding">The target native string encoding.</param>
        /// <returns>The <see cref="GlobalMemory"/> object containing the marshalled string array.</returns>
        public static GlobalMemory StringToMemory
        (
            string input,
            NativeStringEncoding encoding = NativeStringEncoding.Ansi
        )
        {
            return encoding switch
            {
                NativeStringEncoding.BStr => BStrToMemory(Marshal.StringToBSTR(input), input.Length),
                NativeStringEncoding.LPStr => AnsiToMemory(input),
                NativeStringEncoding.LPTStr => Utf8ToMemory(input),
                NativeStringEncoding.LPUTF8Str => Utf8ToMemory(input),
                NativeStringEncoding.LPWStr => WideToMemory(input),
                _ => ThrowInvalidEncoding<GlobalMemory>()
            };

            static unsafe GlobalMemory Utf8ToMemory(string input)
            {
                var memory = GlobalMemory.Allocate(Encoding.UTF8.GetMaxByteCount(input.Length) + 1);
                int convertedBytes;
                fixed (char* firstChar = input)
                {
                    fixed (byte* bytes = memory)
                    {
                        convertedBytes = Encoding.UTF8.GetBytes(firstChar, input.Length, bytes, memory.Length - 1);
                    }
                }

                memory[convertedBytes] = 0;
                return memory;
            }

            static unsafe GlobalMemory AnsiToMemory(string input)
            {
                var memory = GlobalMemory.Allocate((input.Length + 1) * Marshal.SystemMaxDBCSCharSize);
                int convertedBytes;

                fixed (char* firstChar = input)
                {
                    fixed (byte* bytes = memory)
                    {
                        convertedBytes = Encoding.UTF8.GetBytes(firstChar, input.Length, bytes, memory.Length);
                    }
                }

                memory[convertedBytes] = 0;
                return memory;
            }

            static unsafe GlobalMemory WideToMemory(string input)
            {
                var memory = GlobalMemory.Allocate((input.Length + 1) * 2);
                fixed (char* firstChar = input)
                {
                    Buffer.MemoryCopy(firstChar, (void*) memory.Handle, memory.Length, input.Length + 1);
                }

                return memory;
            }
        }

        /// <summary>
        /// Allocates a string pointer
        /// </summary>
        /// <param name="length">Length of the memory to be allocated</param>
        /// <param name="encoding">The encoding of the string stored in this memory region</param>
        /// <returns>The pointer to the allocated memory</returns>
        /// <seealso cref="FreeString"/>
        public static nint AllocateString(int length, NativeStringEncoding encoding = NativeStringEncoding.Ansi)
            => encoding switch
            {
                NativeStringEncoding.BStr => AllocBStr(length),
                NativeStringEncoding.LPStr => Allocate(length),
                NativeStringEncoding.LPTStr => Allocate(length),
                NativeStringEncoding.LPUTF8Str => Allocate(length),
                NativeStringEncoding.LPWStr => Allocate(length),
                _ => ThrowInvalidEncoding<nint>()
            };

        /// <summary>
        /// Free a string pointer
        /// </summary>
        /// <param name="ptr">The pointer to be freed</param>
        /// <param name="encoding">The encoding used to allocate this pointer</param>
        /// <seealso cref="AllocateString"/>
        public static void FreeString(nint ptr, NativeStringEncoding encoding = NativeStringEncoding.Ansi)
        {
            switch (encoding)
            {
                case NativeStringEncoding.BStr:
                    FreeBStr(ptr);
                    break;
                case NativeStringEncoding.LPStr:
                case NativeStringEncoding.LPTStr:
                case NativeStringEncoding.LPUTF8Str:
                case NativeStringEncoding.LPWStr:
                    Free(ptr);
                    break;
                default:
                    ThrowInvalidEncoding<nint>();
                    break;
            }
        }

#nullable enable

        /// <summary>
        /// Gets a pointer to memory containing a copy of the input string marshalled per the specified
        /// native string encoding.
        /// </summary>
        /// <remarks>
        /// The allocated memory must be manually freed using <see cref="Free"/>.
        /// </remarks>
        /// <param name="input">The string to marshal.</param>
        /// <param name="encoding">The target native string encoding.</param>
        /// <returns>A pointer to the memory containing the marshalled string array.</returns>
        public static nint StringToPtr(string? input, NativeStringEncoding encoding = NativeStringEncoding.Ansi)
            => input is null ? 0 : RegisterMemory(StringToMemory(input, encoding));

        /// <summary>
        /// Reads a null-terminated string from unmanaged memory, with the given native encoding.
        /// </summary>
        /// <param name="input">A pointer to memory containing a null-terminated string.</param>
        /// <param name="encoding">The encoding of the string in memory.</param>
        /// <returns>The string read from memory.</returns>
        public static string? PtrToString(nint input, NativeStringEncoding encoding = NativeStringEncoding.Ansi)
        {
            if (input == 0)
            {
                return null;
            }
            
            return encoding switch
            {
                NativeStringEncoding.BStr => BStrToString(input),
                NativeStringEncoding.LPStr => AnsiToString(input),
                NativeStringEncoding.LPTStr => Utf8PtrToString(input),
                NativeStringEncoding.LPUTF8Str => Utf8PtrToString(input),
                NativeStringEncoding.LPWStr => WideToString(input),
                _ => ThrowInvalidEncoding<string>()
            };

            static unsafe string BStrToString(nint ptr)
                => new string((char*) ptr, 0, (int) (*((uint*) ptr - 1) / sizeof(char)));

            static unsafe string AnsiToString(nint ptr) => new string((sbyte*) ptr);
            static unsafe string WideToString(nint ptr) => new string((char*) ptr);
        }

        /// <summary>
        /// Reads a null-terminated string from global memory, with the given native encoding.
        /// </summary>
        /// <param name="input">Global memory containing a null-terminated string.</param>
        /// <param name="e">The encoding of the string in memory.</param>
        /// <returns>The string read from memory.</returns>
        public static string MemoryToString(GlobalMemory input, NativeStringEncoding e = NativeStringEncoding.Ansi)
            => PtrToString(input.Handle, e)!; // TODO tolerate a GlobalMemory.Null if we introduce one in the future?

#nullable disable

        /// <summary>
        /// Returns a copy of the given string array in global memory, marshalled using the specified encoding.
        /// </summary>
        /// <param name="input">The input array.</param>
        /// <param name="e">The encoding of the resultant string array.</param>
        /// <returns>Global memory containing the marshalled string array.</returns>
        public static GlobalMemory StringArrayToMemory
        (
            IReadOnlyList<string> input,
            NativeStringEncoding e = NativeStringEncoding.Ansi
        )
        {
            var memory = GlobalMemory.Allocate(input.Count * IntPtr.Size);
            var span = memory.AsSpan<nint>();
            for (var i = 0; i < input.Count; i++)
            {
                span[i] = StringToPtr(input[i], e);
            }

            return memory;
        }

        /// <summary>
        /// Returns a copy of the given string array in global memory, marshalled using the specified custom marshaller.
        /// </summary>
        /// <param name="input">The input array.</param>
        /// <param name="customStringMarshaller">The custom string-to-pointer marshaller to use.</param>
        /// <returns>Global memory containing the marshalled string array.</returns>
        public static GlobalMemory StringArrayToMemory
        (
            IReadOnlyList<string> input,
            Func<string, nint> customStringMarshaller
        )
        {
            var memory = GlobalMemory.Allocate(input.Count * IntPtr.Size);
            var span = memory.AsSpan<nint>();
            for (var i = 0; i < input.Count; i++)
            {
                span[i] = customStringMarshaller(input[i]);
            }

            return memory;
        }

        /// <summary>
        /// Returns a copy of the given string array in memory, marshalled using the specified encoding.
        /// </summary>
        /// <param name="input">The input array.</param>
        /// <param name="encoding">The encoding of the resultant string array.</param>
        /// <returns>A pointer to memory containing the marshalled string array.</returns>
        public static nint StringArrayToPtr
        (
            IReadOnlyList<string> input,
            NativeStringEncoding encoding = NativeStringEncoding.Ansi
        )
        {
            var memory = StringArrayToMemory(input, encoding);
            _stringArrays.TryAdd(memory, input.Count);
            return RegisterMemory(memory);
        }

        /// <summary>
        /// Returns a copy of the given string array in memory, marshalled using the given custom string marshaller.
        /// </summary>
        /// <param name="input">The input array.</param>
        /// <param name="customStringMarshaller">The marshaller to use for the individual strings in the array.</param>
        /// <returns>A pointer to memory containing the marshalled string array.</returns>
        public static nint StringArrayToPtr
        (
            IReadOnlyList<string> input,
            Func<string, nint> customStringMarshaller
        )
        {
            var memory = StringArrayToMemory(input, customStringMarshaller);
            _stringArrays.TryAdd(memory, input.Count);
            return RegisterMemory(memory);
        }

        /// <summary>
        /// Converts & copies a pointer to an array of strings.
        /// </summary>
        /// <param name="ptr">The pointer to convert.</param>
        /// <param name="arr">The array to fill with strings.</param>
        /// <param name="encoding">The encoding of the string in memory</param>
        public static unsafe void CopyPtrToStringArray
        (
            nint ptr,
            string[] arr,
            NativeStringEncoding encoding = NativeStringEncoding.Ansi
        )
        {
            for (var i = 0; i < arr.Length; i++)
            {
                arr[i] = PtrToString(((nint*) ptr)![i]);
            }
        }

        /// <summary>
        /// Reads an array null-terminated string from unmanaged memory, with the given native encoding.
        /// </summary>
        /// <param name="input">A pointer to unmanaged memory containing a string array.</param>
        /// <param name="numStrings">The number of strings contained within the string array.</param>
        /// <param name="encoding">The encoding of the strings in memory.</param>
        /// <returns>The read string array.</returns>
        public static unsafe string[] PtrToStringArray
        (
            nint input,
            int numStrings,
            NativeStringEncoding encoding = NativeStringEncoding.Ansi
        )
        {
            var ret = new string[numStrings];
            var ptrs = (nint*) input;
            for (var i = 0; i < numStrings; i++)
            {
                ret[i] = PtrToString(ptrs![i]);
            }

            return ret;
        }

        /// <summary>
        /// Reads an array null-terminated string from unmanaged memory, with the given custom pointer-to-string
        /// marshaller.
        /// </summary>
        /// <param name="input">A pointer to unmanaged memory containing a string array.</param>
        /// <param name="numStrings">The number of strings contained within the string array.</param>
        /// <param name="customUnmarshaller">The pointer-to-string marshaller to use.</param>
        /// <returns>The read string array.</returns>
        public static unsafe string[] PtrToStringArray
        (
            nint input,
            int numStrings,
            Func<nint, string> customUnmarshaller
        )
        {
            var ret = new string[numStrings];
            var ptrs = (nint*) input;
            for (var i = 0; i < numStrings; i++)
            {
                ret[i] = customUnmarshaller(ptrs![i]);
            }

            return ret;
        }

        /// <summary>
        /// Reads an array null-terminated string from global memory, with the given native encoding.
        /// </summary>
        /// <param name="input">Global memory containing a string array.</param>
        /// <param name="encoding">The encoding of the strings in memory.</param>
        /// <returns>The read string array.</returns>
        public static string[] MemoryToStringArray
        (
            GlobalMemory input,
            NativeStringEncoding encoding = NativeStringEncoding.Ansi
        ) => PtrToStringArray(input, input.Length / IntPtr.Size, encoding);


        /// <summary>
        /// Reads an array null-terminated string from global memory, with the given pointer-to-string marshaller.
        /// </summary>
        /// <param name="input">Global memory containing a string array.</param>
        /// <param name="customUnmarshaller">The pointer-to-string marshaller to use.</param>
        /// <returns>The read string array.</returns>
        public static string[] MemoryToStringArray
        (
            GlobalMemory input,
            Func<nint, string> customUnmarshaller
        ) => PtrToStringArray(input, input.Length / IntPtr.Size, customUnmarshaller);

        private static unsafe string Utf8PtrToString(nint ptr)
        {
            var span = new Span<byte>((void*) ptr, int.MaxValue);
            span = span.Slice(0, span.IndexOf(default(byte)));
            fixed (byte* bytes = span)
            {
                return Encoding.UTF8.GetString(bytes, span.Length);
            }
        }

        // "Unsafe" methods
        /// <summary>
        /// Gets a <see cref="GlobalMemory"/> object representing this HGlobal.
        /// </summary>
        /// <param name="hGlobal">The HGlobal to wrap.</param>
        /// <param name="length">The length of this HGlobal in bytes.</param>
        /// <returns>An object representing this HGlobal.</returns>
        public static GlobalMemory HGlobalToMemory(nint hGlobal, int length)
            => GlobalMemory.FromHGlobal(hGlobal, length);

        /// <summary>
        /// Gets a <see cref="GlobalMemory"/> object representing this BStr.
        /// </summary>
        /// <param name="bStr">The BStr to wrap.</param>
        /// <param name="length">The length of this BStr in bytes.</param>
        /// <returns>An object representing this BStr.</returns>
        public static GlobalMemory BStrToMemory(nint bStr, int length)
            => GlobalMemory.FromBStr(bStr, length);

        /// <summary>
        /// Gets a <see cref="GlobalMemory"/> object representing this pointer.
        /// </summary>
        /// <param name="ptr">The pointer to wrap.</param>
        /// <param name="length">The length of this pointer in bytes.</param>
        /// <returns>An object representing this pointer.</returns>
        /// <remarks>
        /// This is not recommended for use as it may be implied that freeing occurs when this object goes out of scope,
        /// even though this is not the case. If the pointer is a HGlobal or a BStr, use one of the other methods;
        /// otherwise, this method should only be used for accessing <see cref="GlobalMemory"/>'s rich set of APIs and
        /// not to manage lifetime.
        /// </remarks>
        public static GlobalMemory PtrToMemory(nint ptr, int length)
            => GlobalMemory.FromHGlobal(ptr, length);

        /// <summary>
        /// Gets a function pointer for the given delegate.
        /// </summary>
        /// <param name="delegate">The delegate to get a function pointer to.</param>
        /// <param name="kind">
        /// The method by which SilkMarshal should retrieve the pointer. If <see cref="DelegatePointerKind.Stub"/>, the
        /// pointer is retrieved using <see cref="Marshal"/>'s <see cref="Marshal.GetFunctionPointerForDelegate"/> which
        /// will generate an unmanaged-to-managed transition runtime stub. This may have some overhead, but is critical
        /// if you want to pass this pointer to native code. If this pointer is not being sent to native code, and you
        /// just want to use this delegate as a managed function pointer, <see cref="DelegatePointerKind.Passthrough"/>
        /// may be used which will return a function pointer directly to the method instead of going through an
        /// unmanaged stub. The method represented by the delegate must be static, however.
        /// </param>
        /// <param name="pinned">
        /// Whether to pin the delegate such that the returned pointer remains valid for long periods of time.
        /// </param>
        /// <returns>A function pointer to the given delegate.</returns>
        public static nint DelegateToPtr
        (
            Delegate @delegate,
            DelegatePointerKind kind = DelegatePointerKind.Stub,
            bool pinned = true
        )
        {
            if (kind == DelegatePointerKind.Passthrough)
            {
                var method = @delegate.Method;
                if (!method.IsStatic)
                {
                    ThrowManagedNonStatic();
                }

                return method.MethodHandle.GetFunctionPointer();
            }

            if (pinned)
            {
                var gcHandle = GCHandle.Alloc(@delegate);
                var ret = Marshal.GetFunctionPointerForDelegate(@delegate);
                _otherGCHandles.TryAdd(ret, gcHandle);
                return ret;
            }

            return Marshal.GetFunctionPointerForDelegate(@delegate);

            static void ThrowManagedNonStatic()
                => throw new InvalidOperationException("Can't get a passthrough pointer to a non-static method group.");
        }

        private static void DelegateSafetyCheck(Delegate @delegate, CallingConvention conv)
        {
            var attr = @delegate.Method.GetCustomAttribute<UnmanagedFunctionPointerAttribute>();
            var callConv = attr?.CallingConvention ?? CallingConvention.Winapi;
            if (callConv == CallingConvention.Winapi && conv != CallingConvention.Winapi)
            {
                callConv = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? CallingConvention.Cdecl
                    : CallingConvention.StdCall;
            }

            if (callConv != conv)
            {
                Throw(nameof(conv), callConv, conv);
            }

            static void Throw(string nameof, CallingConvention delegateConv, CallingConvention desiredConv)
                => throw new ArgumentException
                (
                    $"Attempted to create a {desiredConv} function pointer from a " +
                    $"{delegateConv} delegate.", nameof
                );
        }

        public static unsafe delegate*<void> DelegateToManaged(Delegate @delegate, bool pinned = true)
            => (delegate*<void>) DelegateToPtr(@delegate, DelegatePointerKind.Passthrough);

        /// <summary>
        /// Gets a function pointer for the given delegate using the <c>__cdecl</c> calling convention.
        /// </summary>
        /// <param name="delegate">The delegate to get a function pointer to.</param>
        /// <param name="kind">
        /// The method by which SilkMarshal should retrieve the pointer. Should always be
        /// <see cref="DelegatePointerKind.Stub"/> except in very rare edge cases.
        /// </param>
        /// <param name="pinned">
        /// Whether to pin the delegate such that the returned pointer remains valid for long periods of time.
        /// </param>
        /// <param name="ignoreValidityChecks">
        /// Whether to skip validity checks, such as calling convention mismatch checks. 
        /// </param>
        /// <returns>A function pointer to the given delegate.</returns>
        public static unsafe delegate* unmanaged[Cdecl]<void> DelegateToCdecl
        (
            Delegate @delegate,
            DelegatePointerKind kind = DelegatePointerKind.Stub,
            bool pinned = true,
            bool ignoreValidityChecks = false
        )
        {
            if (!ignoreValidityChecks)
            {
                DelegateSafetyCheck(@delegate, CallingConvention.Cdecl);
            }

            return (delegate* unmanaged[Cdecl]<void>) DelegateToPtr(@delegate, kind, pinned);
        }

        /// <summary>
        /// Gets a function pointer for the given delegate using the <c>__stdcall</c> calling convention.
        /// </summary>
        /// <param name="delegate">The delegate to get a function pointer to.</param>
        /// <param name="kind">
        /// The method by which SilkMarshal should retrieve the pointer. Should always be
        /// <see cref="DelegatePointerKind.Stub"/> except in very rare edge cases.
        /// </param>
        /// <param name="pinned">
        /// Whether to pin the delegate such that the returned pointer remains valid for long periods of time.
        /// </param>
        /// <param name="ignoreValidityChecks">
        /// Whether to skip validity checks, such as calling convention mismatch checks. 
        /// </param>
        /// <returns>A function pointer to the given delegate.</returns>
        public static unsafe delegate* unmanaged[Stdcall]<void> DelegateToStdcall
        (
            Delegate @delegate,
            DelegatePointerKind kind = DelegatePointerKind.Stub,
            bool pinned = true,
            bool ignoreValidityChecks = false
        )
        {
            if (!ignoreValidityChecks)
            {
                DelegateSafetyCheck(@delegate, CallingConvention.StdCall);
            }

            return (delegate* unmanaged[Stdcall]<void>) DelegateToPtr(@delegate, kind, pinned);
        }

        /// <summary>
        /// Gets a function pointer for the given delegate using the <c>__fastcall</c> calling convention.
        /// </summary>
        /// <param name="delegate">The delegate to get a function pointer to.</param>
        /// <param name="kind">
        /// The method by which SilkMarshal should retrieve the pointer. Should always be
        /// <see cref="DelegatePointerKind.Stub"/> except in very rare edge cases.
        /// </param>
        /// <param name="pinned">
        /// Whether to pin the delegate such that the returned pointer remains valid for long periods of time.
        /// </param>
        /// <param name="ignoreValidityChecks">
        /// Whether to skip validity checks, such as calling convention mismatch checks. 
        /// </param>
        /// <returns>A function pointer to the given delegate.</returns>
        public static unsafe delegate* unmanaged[Fastcall]<void> DelegateToFastcall
        (
            Delegate @delegate,
            DelegatePointerKind kind = DelegatePointerKind.Stub,
            bool pinned = true,
            bool ignoreValidityChecks = false
        )
        {
            if (!ignoreValidityChecks)
            {
                DelegateSafetyCheck(@delegate, CallingConvention.FastCall);
            }

            return (delegate* unmanaged[Fastcall]<void>) DelegateToPtr(@delegate, kind, pinned);
        }

        /// <summary>
        /// Gets a function pointer for the given delegate using the <c>__thiscall</c> calling convention.
        /// </summary>
        /// <param name="delegate">The delegate to get a function pointer to.</param>
        /// <param name="kind">
        /// The method by which SilkMarshal should retrieve the pointer. Should always be
        /// <see cref="DelegatePointerKind.Stub"/> except in very rare edge cases.
        /// </param>
        /// <param name="pinned">
        /// Whether to pin the delegate such that the returned pointer remains valid for long periods of time.
        /// </param>
        /// <param name="ignoreValidityChecks">
        /// Whether to skip validity checks, such as calling convention mismatch checks. 
        /// </param>
        /// <returns>A function pointer to the given delegate.</returns>
        public static unsafe delegate* unmanaged[Thiscall]<void> DelegateToThiscall
        (
            Delegate @delegate,
            DelegatePointerKind kind = DelegatePointerKind.Stub,
            bool pinned = true,
            bool ignoreValidityChecks = false
        )
        {
            if (!ignoreValidityChecks)
            {
                DelegateSafetyCheck(@delegate, CallingConvention.ThisCall);
            }

            return (delegate* unmanaged[Thiscall]<void>) DelegateToPtr(@delegate, kind, pinned);
        }

        public static T PtrToDelegate<T>(nint p) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(p);

        [MethodImpl((MethodImplOptions) 768)]
        public static unsafe ref Guid GuidOf<T>() => ref *TypeGuid<T>.Riid;

        [MethodImpl((MethodImplOptions) 768)]
        public static unsafe Guid* GuidPtrOf<T>() => TypeGuid<T>.Riid;

        // Begin adapted TerraFX code
        // Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
        // See License.md in the repository root for more information.
        // Ported from shared/uuids.h in the Windows SDK for Windows 10.0.19041.0
        // Original source is Copyright © Microsoft. All rights reserved.

        private static unsafe class TypeGuid<T>
        {
            public static readonly Guid* Riid = CreateRiid();

            private static Guid* CreateRiid()
            {
#if NET5_0
                var p = (Guid*) RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(T), sizeof(Guid));
#else
                var p = (Guid*) Allocate(sizeof(Guid));
#endif

                *p = typeof(T).GUID;

                return p;
            }
        }

        // End adapted TerraFX code

        /// <summary>
        /// Converts the specified HRESULT error code to a corresponding <see cref="T:System.Exception"></see> object
        /// and throws if a match was found.
        /// </summary>
        /// <param name="hResult">The HRESULT to be converted.</param>
        public static void ThrowHResult(int hResult)
        {
            var ex = Marshal.GetExceptionForHR(hResult);
            if (ex is not null)
            {
                throw ex;
            }
        }

        [DllImport("kernel32", EntryPoint = "CreateEventW", ExactSpelling = true, SetLastError = true)]
        private static extern unsafe nint CoreCreateWinEvent
            (SecurityAttributes* lpEventAttributes, int bManualReset, int bInitialState, char* lpName);

        [DllImport("kernel32", EntryPoint = "WaitForSingleObjectEx", ExactSpelling = true,  SetLastError = true)]
        private static extern uint CoreWaitObjects(nint hHandle, uint dwMilliseconds, int bAlertable);

        [DllImport("kernel32", EntryPoint = "WaitForMultipleObjectsEx", ExactSpelling = true, SetLastError = true)]
        private static extern unsafe uint CoreWaitObjects
            (uint nCount, nint* lpHandles, int bWaitAll, uint dwMilliseconds, int bAlertable);
            
        [DllImport("kernel32", EntryPoint = "CloseHandle", ExactSpelling = true, SetLastError = true)]
        public static extern int CloseWindowsHandle(nint hObject);

        public static unsafe nint CreateWindowsEvent
            (SecurityAttributes* lpEventAttributes, bool bManualReset, bool bInitialState, char* lpName)
        {
            var ret = CoreCreateWinEvent(lpEventAttributes, bManualReset ? 1 : 0, bInitialState ? 1 : 0, lpName);
            if (ret == 0)
            {
                ThrowHResult(Marshal.GetHRForLastWin32Error());
            }

            return ret;
        }

        public static uint WaitWindowsObjects(nint @object, uint timeout = unchecked((uint) -1), bool alertable = false) => CoreWaitObjects(@object, timeout, alertable ? 1 : 0);

        public static unsafe uint WaitWindowsObjects
        (
            ReadOnlySpan<nint> objects,
            bool waitAll = true,
            uint timeout = unchecked((uint) -1),
            bool alertable = false
        )
        {
            if (objects.Length == 1)
            {
                return CoreWaitObjects(objects[0], timeout, alertable ? 1 : 0);
            }

            fixed (nint* handles = objects)
            {
                return CoreWaitObjects((uint) objects.Length, handles, waitAll ? 1 : 0, timeout, alertable ? 1 : 0);
            }
        }

        [MethodImpl((MethodImplOptions) 768)]
#if !NET5_0
        public static unsafe ref T NullRef<T>() => ref Unsafe.AsRef<T>((void*) 0);
#else
        public static ref T NullRef<T>() => ref Unsafe.NullRef<T>();
#endif
    }
}