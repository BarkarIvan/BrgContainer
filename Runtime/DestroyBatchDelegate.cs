﻿namespace BrgContainer.Runtime
{
    using System.Runtime.InteropServices;
    using UnityEngine.Rendering;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void DestroyBatchDelegate(BatchID batchId);
}