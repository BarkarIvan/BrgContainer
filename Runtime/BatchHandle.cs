﻿namespace BrgContainer.Runtime
{
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.InteropServices;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Mathematics;
    using UnityEngine.Rendering;

    /// <summary>
    /// The handle of a batch.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct BatchHandle
    {
        private readonly BatchID m_BatchId;
        
        private readonly NativeArray<float4> m_Buffer;
        private readonly unsafe int* m_InstanceCount;
        private readonly BatchDescription m_Description;
        
        private readonly SetGPUDataDelegate<float4> m_UploadCallback;
        private readonly DestroyBatchDelegate m_DestroyCallback;
        private readonly IsAliveDelegate m_IsAliveCallback;

        public bool IsAlive => m_IsAliveCallback != null && m_IsAliveCallback.Invoke(m_BatchId);

        [ExcludeFromBurstCompatTesting("BatchHandle creating is unburstable")]
        internal unsafe BatchHandle(BatchID batchId, NativeArray<float4> buffer, int* instanceCount, ref BatchDescription description, 
            [NotNull]SetGPUDataDelegate<float4> uploadCallback, [NotNull]DestroyBatchDelegate destroyCallback, [NotNull]IsAliveDelegate isAliveCallback)
        {
            m_BatchId = batchId;
            
            m_Buffer = buffer;
            m_InstanceCount = instanceCount;
            m_Description = description;
            
            m_UploadCallback = uploadCallback;
            m_DestroyCallback = destroyCallback;
            m_IsAliveCallback = isAliveCallback;
        }

        /// <summary>
        /// Returns <see cref="BatchInstanceDataBuffer"/> instance that provides API for write instance data.
        /// </summary>
        /// <returns>Returns <see cref="BatchInstanceDataBuffer"/> instance.</returns>
        public unsafe BatchInstanceDataBuffer AsInstanceDataBuffer()
        {
            return new BatchInstanceDataBuffer(m_Buffer, m_Description.m_MetadataInfoMap,
                m_InstanceCount, m_Description.MaxInstanceCount, m_Description.MaxInstancePerWindow, m_Description.AlignedWindowSize / 16);
        }

        /// <summary>
        /// Upload current data to the GPU side.
        /// </summary>
        /// <param name="instanceCount"></param>
        [BurstDiscard]
        public unsafe void Upload(int instanceCount)
        {
            var completeWindows = instanceCount / m_Description.MaxInstancePerWindow;
            if (completeWindows > 0)
            {
                var size = completeWindows * m_Description.AlignedWindowSize / 16;
                m_UploadCallback?.Invoke(m_BatchId, m_Buffer, 0, 0, size);
            }

            var lastBatchId = completeWindows;
            var itemInLastBatch = instanceCount - m_Description.MaxInstancePerWindow * completeWindows;

            if (itemInLastBatch <= 0)
                return;
            
            var windowOffsetInFloat4 = (uint)(lastBatchId * m_Description.AlignedWindowSize / 16);

            var offset = 0;
            for (var i = 0; i < m_Description.Length; i++)
            {
                var metadataValue = m_Description[i];
                var metadataInfo = m_Description.GetMetadataInfo(metadataValue.NameID);
                var startIndex = (int) (windowOffsetInFloat4 + m_Description.MaxInstancePerWindow * offset);
                var sizeInFloat = metadataInfo.Size / 16;
                offset += sizeInFloat;

                m_UploadCallback?.Invoke(m_BatchId, m_Buffer, startIndex, startIndex,
                    itemInLastBatch * sizeInFloat);
            }

            *m_InstanceCount = instanceCount;
        }
        
        /// <summary>
        /// Upload current data to the GPU side.
        /// </summary>
        [BurstDiscard]
        public unsafe void Upload()
        {
            Upload(*m_InstanceCount);
        }

        /// <summary>
        /// Destroy the batch.
        /// </summary>
        [ExcludeFromBurstCompatTesting("BatchHandle destroying is unburstable")]
        public void Destroy()
        {
            m_DestroyCallback.Invoke(m_BatchId);
        }
    }
}