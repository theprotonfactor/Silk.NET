// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Core.Attributes;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Loader;

#pragma warning disable 1591

namespace Silk.NET.OpenGLES.Extensions.EXT
{
    public static class ExtMultiDrawArraysOverloads
    {
        public static unsafe void MultiDrawArrays(this ExtMultiDrawArrays thisApi, [Flow(FlowDirection.In)] EXT mode, [Count(Computed = "primcount"), Flow(FlowDirection.In)] int* first, [Count(Computed = "primcount"), Flow(FlowDirection.In)] ReadOnlySpan<uint> count, [Flow(FlowDirection.In)] uint primcount)
        {
            // SpanOverloader
            thisApi.MultiDrawArrays(mode, first, in count.GetPinnableReference(), primcount);
        }

        public static unsafe void MultiDrawArrays(this ExtMultiDrawArrays thisApi, [Flow(FlowDirection.In)] EXT mode, [Count(Computed = "primcount"), Flow(FlowDirection.In)] ReadOnlySpan<int> first, [Count(Computed = "primcount"), Flow(FlowDirection.In)] uint* count, [Flow(FlowDirection.In)] uint primcount)
        {
            // SpanOverloader
            thisApi.MultiDrawArrays(mode, in first.GetPinnableReference(), count, primcount);
        }

        public static unsafe void MultiDrawArrays(this ExtMultiDrawArrays thisApi, [Flow(FlowDirection.In)] EXT mode, [Count(Computed = "primcount"), Flow(FlowDirection.In)] ReadOnlySpan<int> first, [Count(Computed = "primcount"), Flow(FlowDirection.In)] ReadOnlySpan<uint> count, [Flow(FlowDirection.In)] uint primcount)
        {
            // SpanOverloader
            thisApi.MultiDrawArrays(mode, in first.GetPinnableReference(), in count.GetPinnableReference(), primcount);
        }

        public static unsafe void MultiDrawArrays(this ExtMultiDrawArrays thisApi, [Flow(FlowDirection.In)] PrimitiveType mode, [Count(Computed = "primcount"), Flow(FlowDirection.In)] int* first, [Count(Computed = "primcount"), Flow(FlowDirection.In)] ReadOnlySpan<uint> count, [Flow(FlowDirection.In)] uint primcount)
        {
            // SpanOverloader
            thisApi.MultiDrawArrays(mode, first, in count.GetPinnableReference(), primcount);
        }

        public static unsafe void MultiDrawArrays(this ExtMultiDrawArrays thisApi, [Flow(FlowDirection.In)] PrimitiveType mode, [Count(Computed = "primcount"), Flow(FlowDirection.In)] ReadOnlySpan<int> first, [Count(Computed = "primcount"), Flow(FlowDirection.In)] uint* count, [Flow(FlowDirection.In)] uint primcount)
        {
            // SpanOverloader
            thisApi.MultiDrawArrays(mode, in first.GetPinnableReference(), count, primcount);
        }

        public static unsafe void MultiDrawArrays(this ExtMultiDrawArrays thisApi, [Flow(FlowDirection.In)] PrimitiveType mode, [Count(Computed = "primcount"), Flow(FlowDirection.In)] ReadOnlySpan<int> first, [Count(Computed = "primcount"), Flow(FlowDirection.In)] ReadOnlySpan<uint> count, [Flow(FlowDirection.In)] uint primcount)
        {
            // SpanOverloader
            thisApi.MultiDrawArrays(mode, in first.GetPinnableReference(), in count.GetPinnableReference(), primcount);
        }

        public static unsafe void MultiDrawElements(this ExtMultiDrawArrays thisApi, [Flow(FlowDirection.In)] EXT mode, [Count(Computed = "primcount"), Flow(FlowDirection.In)] ReadOnlySpan<uint> count, [Flow(FlowDirection.In)] EXT type, [Count(Computed = "primcount"), Flow(FlowDirection.In)] void** indices, [Flow(FlowDirection.In)] uint primcount)
        {
            // SpanOverloader
            thisApi.MultiDrawElements(mode, in count.GetPinnableReference(), type, indices, primcount);
        }

        public static unsafe void MultiDrawElements(this ExtMultiDrawArrays thisApi, [Flow(FlowDirection.In)] EXT mode, [Count(Computed = "primcount"), Flow(FlowDirection.In)] ReadOnlySpan<uint> count, [Flow(FlowDirection.In)] EXT type, [Count(Computed = "primcount"), Flow(FlowDirection.In)] in void* indices, [Flow(FlowDirection.In)] uint primcount)
        {
            // SpanOverloader
            thisApi.MultiDrawElements(mode, in count.GetPinnableReference(), type, in indices, primcount);
        }

        public static unsafe void MultiDrawElements(this ExtMultiDrawArrays thisApi, [Flow(FlowDirection.In)] EXT mode, [Count(Computed = "primcount"), Flow(FlowDirection.In)] ReadOnlySpan<uint> count, [Flow(FlowDirection.In)] DrawElementsType type, [Count(Computed = "primcount"), Flow(FlowDirection.In)] void** indices, [Flow(FlowDirection.In)] uint primcount)
        {
            // SpanOverloader
            thisApi.MultiDrawElements(mode, in count.GetPinnableReference(), type, indices, primcount);
        }

        public static unsafe void MultiDrawElements(this ExtMultiDrawArrays thisApi, [Flow(FlowDirection.In)] EXT mode, [Count(Computed = "primcount"), Flow(FlowDirection.In)] ReadOnlySpan<uint> count, [Flow(FlowDirection.In)] DrawElementsType type, [Count(Computed = "primcount"), Flow(FlowDirection.In)] in void* indices, [Flow(FlowDirection.In)] uint primcount)
        {
            // SpanOverloader
            thisApi.MultiDrawElements(mode, in count.GetPinnableReference(), type, in indices, primcount);
        }

        public static unsafe void MultiDrawElements(this ExtMultiDrawArrays thisApi, [Flow(FlowDirection.In)] PrimitiveType mode, [Count(Computed = "primcount"), Flow(FlowDirection.In)] ReadOnlySpan<uint> count, [Flow(FlowDirection.In)] EXT type, [Count(Computed = "primcount"), Flow(FlowDirection.In)] void** indices, [Flow(FlowDirection.In)] uint primcount)
        {
            // SpanOverloader
            thisApi.MultiDrawElements(mode, in count.GetPinnableReference(), type, indices, primcount);
        }

        public static unsafe void MultiDrawElements(this ExtMultiDrawArrays thisApi, [Flow(FlowDirection.In)] PrimitiveType mode, [Count(Computed = "primcount"), Flow(FlowDirection.In)] ReadOnlySpan<uint> count, [Flow(FlowDirection.In)] EXT type, [Count(Computed = "primcount"), Flow(FlowDirection.In)] in void* indices, [Flow(FlowDirection.In)] uint primcount)
        {
            // SpanOverloader
            thisApi.MultiDrawElements(mode, in count.GetPinnableReference(), type, in indices, primcount);
        }

        public static unsafe void MultiDrawElements(this ExtMultiDrawArrays thisApi, [Flow(FlowDirection.In)] PrimitiveType mode, [Count(Computed = "primcount"), Flow(FlowDirection.In)] ReadOnlySpan<uint> count, [Flow(FlowDirection.In)] DrawElementsType type, [Count(Computed = "primcount"), Flow(FlowDirection.In)] void** indices, [Flow(FlowDirection.In)] uint primcount)
        {
            // SpanOverloader
            thisApi.MultiDrawElements(mode, in count.GetPinnableReference(), type, indices, primcount);
        }

        public static unsafe void MultiDrawElements(this ExtMultiDrawArrays thisApi, [Flow(FlowDirection.In)] PrimitiveType mode, [Count(Computed = "primcount"), Flow(FlowDirection.In)] ReadOnlySpan<uint> count, [Flow(FlowDirection.In)] DrawElementsType type, [Count(Computed = "primcount"), Flow(FlowDirection.In)] in void* indices, [Flow(FlowDirection.In)] uint primcount)
        {
            // SpanOverloader
            thisApi.MultiDrawElements(mode, in count.GetPinnableReference(), type, in indices, primcount);
        }

    }
}

