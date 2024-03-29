﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.18047
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System.Runtime;

using Photon.SocketServer;
using Photon.SocketServer.Annotations;

namespace Photon.LoadBalancing.Common
{
    [SettingsMarker("Photon:LoadBalancing")]
    public class CommonSettings 
    {
        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static CommonSettings()
        {}
        public static CommonSettings Default { get; } = ApplicationBase.GetConfigSectionAndValidate<CommonSettings>("Photon:LoadBalancing");

        public bool EnablePerformanceCounters { get; set; } = true;

        public bool UseLoadPrediction { get; set; } = false;

        public System.Runtime.GCLatencyMode GCLatencyMode { get; set; } = GCLatencyMode.Interactive;

        public bool RequireSecureConnection { get; set; } = false;

        public int MinDisconnectTime { get; set; } = 3000;

        public int MaxDisconnectTime { get; set; } = 10000;

        public bool AllowDebugGameOperation { get; set; } = false;
    }
}
