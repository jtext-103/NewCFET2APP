using Jtext103.CFET2.Core;
using Jtext103.CFET2.Core.Attributes;
using System;

namespace Jtext103.CFET2.CFET2App.ExampleThings
{

    public class PcMonitorThing:Thing
    {
        //status
        [Cfet2Status("ram")]
        public float GetRam()
        {
            // 获取当前操作系统的内存状态（或容器的内存限制状态）
            var gcInfo = GC.GetGCMemoryInfo();

            // TotalAvailableMemoryBytes = 系统/容器的总物理内存
            // MemoryLoadBytes = 当前系统/容器已被使用的总物理内存
            long availableBytes = gcInfo.TotalAvailableMemoryBytes - gcInfo.MemoryLoadBytes;

            // 将 Byte 转换为 MByte 返回
            return availableBytes / (1024f * 1024f);
        }

        [Cfet2Status("defaultStatus")]
        public string GetDefaultStatus()
        {
            return this.Path + @"/defaultStatus";
        }
    }
}
