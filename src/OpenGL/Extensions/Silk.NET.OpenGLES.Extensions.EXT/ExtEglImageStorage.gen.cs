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
using Silk.NET.OpenGLES;
using Extension = Silk.NET.Core.Attributes.ExtensionAttribute;

#pragma warning disable 1591

namespace Silk.NET.OpenGLES.Extensions.EXT
{
    [Extension("EXT_EGL_image_storage")]
    public unsafe partial class ExtEglImageStorage : NativeExtension<GL>
    {
        public const string ExtensionName = "EXT_EGL_image_storage";
        [NativeApi(EntryPoint = "glEGLImageTargetTexStorageEXT")]
        public unsafe partial void EglimageTargetTexStorage([Flow(FlowDirection.In)] EXT target, [Flow(FlowDirection.In)] nint image, [Flow(FlowDirection.In)] int* attrib_list);

        [NativeApi(EntryPoint = "glEGLImageTargetTexStorageEXT")]
        public partial void EglimageTargetTexStorage([Flow(FlowDirection.In)] EXT target, [Flow(FlowDirection.In)] nint image, [Flow(FlowDirection.In)] in int attrib_list);

        [NativeApi(EntryPoint = "glEGLImageTargetTextureStorageEXT")]
        public unsafe partial void EglimageTargetTextureStorage([Flow(FlowDirection.In)] uint texture, [Flow(FlowDirection.In)] nint image, [Flow(FlowDirection.In)] int* attrib_list);

        [NativeApi(EntryPoint = "glEGLImageTargetTextureStorageEXT")]
        public partial void EglimageTargetTextureStorage([Flow(FlowDirection.In)] uint texture, [Flow(FlowDirection.In)] nint image, [Flow(FlowDirection.In)] in int attrib_list);

        public ExtEglImageStorage(INativeContext ctx)
            : base(ctx)
        {
        }
    }
}

