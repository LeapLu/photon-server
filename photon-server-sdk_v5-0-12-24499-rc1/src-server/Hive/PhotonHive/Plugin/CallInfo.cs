﻿//#define USE_FINALIZER_FOR_DIAGNOSTIC

using System.Diagnostics;
using Photon.Common;
using Photon.SocketServer.Diagnostics;

namespace Photon.Hive.Plugin
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;

    using ExitGames.Logging;

    using Photon.Hive.Operations;
    using Photon.SocketServer;
    using Photon.Common.Plugins;

    public delegate bool RequestHandler();

    public class CallCounter
    {
        private int counter;

        public int Increment()
        {
            return Interlocked.Increment(ref this.counter);
        }

        public int Decrement()
        {
            return Interlocked.Decrement(ref this.counter);
        }

        public int Value
        {
            get
            {
                return this.counter;
            }
        }
    }

    public class CallEnv
    {
        public IGamePlugin Plugin;
        public string GameName;

        public CallEnv(IGamePlugin plugin, string gameName)
        {
            this.Plugin = plugin;
            this.GameName = gameName;
        }
    }

    /// <summary>
    /// Status of the ICallInfo argument passed to plugin callbacks.
    /// </summary>
    public static class CallStatus
    {
        /// <summary>
        /// The ICallInfo argument was not processed nor deferred.
        /// </summary>
        public const byte New = 0;

        public const byte Paused = 1;
        /// <summary>
        /// The ICallInfo argument is deferred (i.e. Defer method was called).
        /// </summary>
        public const byte Deferred = 2;
        /// <summary>
        /// The ICallInfo argument was successfully processed (i.e. Continue method was called).
        /// </summary>
        public const byte Succeeded = 3;
        /// <summary>
        /// The ICallInfo argument "has failed" (i.e. Fail method was called).
        /// </summary>
        public const byte Failed = 4;
        /// <summary>
        /// The ICallInfo argument was canceled (i.e. Cancel method was called).
        /// </summary>
        public const byte Cancelled = 5;
        public const byte Canceled = Cancelled;
    }

    public abstract class CallInfo : ICallInfo
    {
        #region Constants and Fields

#if USE_FINALIZER_FOR_DIAGNOSTIC
        private static readonly LogCountGuard logCountGuard = new LogCountGuard(new TimeSpan(0, 0, 10));
#endif

        private readonly ILogger logInstanceLogger;

        private readonly CallCounter counter;

        private readonly CallEnv callEnv;

        public Action<string, Dictionary<byte, object>> OnFail;

        #endregion

        #region Constructors and Destructors

        protected CallInfo(CallCounter counter, ILogger logger, CallEnv callEnv)
        {
            this.logInstanceLogger = logger;
            this.callEnv = callEnv;
            this.counter = counter;
            counter.Increment();
            this.ActorNr = -1;

            Debug.Assert(callEnv != null);
        }

#if USE_FINALIZER_FOR_DIAGNOSTIC
        ~CallInfo()
        {
            if (!this.IsProcessed)
            {
                this.logInstanceLogger.ErrorFormat(logCountGuard,
                    "Neither Continue nor Fail are called for ICallInfo, CallEnv: plugin='{0}', game='{1}', plugin module='{2}'",
                    this.callEnv.Plugin.GetType().FullName, this.callEnv.GameName, this.callEnv.Plugin.GetType().Module.Assembly);
            }
        }

#endif
        #endregion

        #region Properties

        public int ActorNr { get; set; }

        public RequestHandler Handler { get; set; }

        public IOperationRequest OperationRequest { get; set; }

        /// <summary>
        /// Call processing status. For possible values, <see cref="Photon.Hive.Plugin.CallStatus"/>.
        /// </summary>
        public byte Status { get; protected set; }

        public bool IsNew       { get { return this.Status == CallStatus.New; } }
        public bool IsPaused    { get { return this.Status == CallStatus.Paused; } }
        public bool IsDeferred  { get { return this.Status == CallStatus.Deferred; } }
        public bool IsSucceeded { get { return this.Status == CallStatus.Succeeded; } }
        public bool IsFailed    { get { return this.Status == CallStatus.Failed; } }
        public bool IsCanceled { get { return this.Status == CallStatus.Cancelled; } }
        public bool IsCancelled  { get { return this.Status == CallStatus.Cancelled; } }
        public bool IsProcessed { get { return (this.Status > CallStatus.Deferred); } }

        public HivePeer Peer { get; set; }

        public SocketServer.SendParameters SendParams { get; set; }

        #endregion

        #region Methods

        public void Reset()
        {
            if (this.IsProcessed)
            {
                throw new Exception("Reset can not be called for processed CallInfo");
            }
            this.Status = CallStatus.New;
        }

        public virtual void Pause()
        {
            this.SupportedPause();
        }

        public virtual void InternalDefer()
        {
            if (this.IsProcessed || this.IsPaused)
            {
                return;
            }

            this.Status = CallStatus.Deferred;
        }

        protected virtual bool StrictModeCheck(out string errorMsg)
        {
            return this.NoDeferringStrictModeCheck(out errorMsg);
        }

        protected bool NoDeferringStrictModeCheck(out string errorMsg)
        {
            if (this.IsNew || this.IsDeferred)
            {
                errorMsg = this.GetStrictModeErrorMsg();
                return false;
            }
            errorMsg = string.Empty;
            return true;
        }

        protected bool StrictModeCheckWithDefer(out string errorMsg)
        {
            errorMsg = string.Empty;
            if (!this.IsNew)
            {
                return true;
            }
            errorMsg = this.GetStrictModeErrorMsg();
            return false;
        }

        protected string GetStrictModeErrorMsg()
        {
            var infoTypeName = this.GetType().ToString();
            return string.Format("none of {0}'s method were called", infoTypeName);
        }

        protected void SupportedPause()
        {
            if (this.IsProcessed || this.IsDeferred)
            {
                return;
            }

            this.Status = CallStatus.Paused;
        }

        protected void NoPause()
        {
            throw new Exception(string.Format("{0} does not support game pausing", this.GetType().Name));
        }

        #endregion//Methods
        
        #region Implemented Interfaces

        #region ICallInfo

        public void Continue()
        {
            if (this.logInstanceLogger.IsDebugEnabled)
            {
                this.logInstanceLogger.Debug("Continue.");
            }

            if (this.IsProcessed)
            {
                throw new Exception("Already called Continue/Fail/Cancel.");
            }

            var result = this.counter.Decrement();
            if (result != 0)
            {
                //this.logInstanceLogger.WarnFormat("Continue() - Unexpected call counter value {0}", result);
            }

            this.Handler();

            this.Status = CallStatus.Succeeded;
        }

        public void Fail(string msg = null, Dictionary<byte, object> errorData = null)
        {
            this.logInstanceLogger.Debug("Fail: " + msg);

            if (this.IsProcessed)
            {
                throw new Exception("Already called Continue/Fail/Cancel.");
            }

            this.Status = CallStatus.Failed;

            var result = this.counter.Decrement();
            if (result != 0)
            {
                // this.logInstanceLogger.WarnFormat("Fail() - Unexpected call counter value {0}", result);
            }

            // TODO: fail isn't well handled jet, and "OnFail" is currently a hack for testing!
            if (this.OnFail != null)
            {
                this.OnFail(msg, errorData);
            }
            else
            {
                //TBD: keep this fix null ref during fail in on close?
                if (this.Peer != null)
                {
                    this.Peer.SendOperationResponse(
                        new OperationResponse
                            {
                                OperationCode = this.OperationRequest.OperationCode,
                                ReturnCode = (short)ErrorCode.PluginReportedError,
                                DebugMessage = msg,
                                Parameters = errorData,
                            },
                        this.SendParams);
                }
            }
        }

        bool ICallInfo.StrictModeCheck(out string errorMsg)
        {
            return this.StrictModeCheck(out errorMsg);
        }

        public void Cancel()
        {
            if (this.IsProcessed)
            {
                throw new Exception("Already called Continue/Fail/Cancel.");
            }

            this.Status = CallStatus.Cancelled;
        }

        [Obsolete]
        public void Defer()
        {
            if (this.IsProcessed || this.IsPaused)
            {
                throw new Exception("Already called Continue/Fail/Cancel/Pause.");
            }

            this.Status = CallStatus.Deferred;
        }

        #endregion

        #endregion

    }

    public abstract class TypedCallInfo<RequestType> : CallInfo, ITypedCallInfo<RequestType>
        where RequestType : IOperationRequest
    {
        protected static readonly LogCountGuard onFailLogCountGuard = new LogCountGuard(new TimeSpan(0, 0, 3));
        protected TypedCallInfo(CallCounter counter, ILogger logger, CallEnv env)
            : base(counter, logger, env) {}

        #region Properties

        public RequestType Request
        {
            get
            {
                return (RequestType)this.OperationRequest;
            }

            set
            {
                this.OperationRequest = value;
            }
        }

        public string UserId { get; set; }

        public string Nickname { get; set; }

#if PLUGINS_0_9
        [Obsolete("Use Nickname instead")]
        public string Username { get { return this.Nickname; } }
#endif

        [Obsolete("User AuthCookie instead")]
        public object AuthResultsToken { get { return this.Peer != null ? this.Peer.AuthCookie : null; } }

        public Dictionary<string, object> AuthCookie { get { return this.Peer != null ? this.Peer.AuthCookie : null; } }

        #endregion
    }

    public class LeaveGameCallInfo : TypedCallInfo<ILeaveGameRequest>, ILeaveGameCallInfo
    {
        private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        public LeaveGameCallInfo(CallCounter counter, CallEnv env)
            : base(counter, Log, env)
        {
            this.OnFail = (errorMsg, objects) =>
            {
                Log.ErrorFormat(onFailLogCountGuard, "OnLeave call finished with Fail. Details: '{0}'", errorMsg);
                if (this.Handler != null)
                {
                    this.Handler();
                }
            };
        }

        #region Properties

        public string Details { get; set; }

        public int Reason { get; set; }

        public bool IsInactive { get; set; }

        #endregion
    }

    public class CreateGameCallInfo : TypedCallInfo<IJoinGameRequest>, ICreateGameCallInfo
    {
        private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        public CreateGameCallInfo(CallCounter counter, CallEnv env)
            : base(counter, Log, env)
        {            
        }

        #region Properties

        public bool IsJoin { get; set; }

        public bool CreateIfNotExists
        {
            get
            {
                return (this.IsJoin && this.Request.JoinMode == JoinModes.CreateIfNotExists);
            }
        }

        public Dictionary<string,object> CreateOptions { get; set; }

        public Hashtable ActorProperties { get { return this.Request.ActorProperties; } }

        #endregion

        protected override bool StrictModeCheck(out string errorMsg)
        {
            if (!base.StrictModeCheck(out errorMsg))
            {
                return false;
            }

            return this.NoDeferringStrictModeCheck(out errorMsg);
        }
    }

    public class JoinGameCallInfo : TypedCallInfo<IJoinGameRequest>, IJoinGameCallInfo
    {
        private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        public JoinGameCallInfo(CallCounter counter, CallEnv env)
            : base(counter, Log, env)
        {
        }

        #region Properties

        public int ActorCount { get; set; }

        public ProcessJoinParams JoinParams { get; set; }

        public Hashtable ActorProperties { get { return this.Request.ActorProperties; } }

        #endregion
    }

    public class CloseGameCallInfo : TypedCallInfo<ICloseRequest>, ICloseGameCallInfo
    {
        private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        public CloseGameCallInfo(CallCounter counter, CallEnv env)
            : base(counter, Log, env)
        {
            this.OnFail = (errorMsg, objects) =>
            {
                Log.ErrorFormat(onFailLogCountGuard, "OnClose call finished with Fail. Details: '{0}'", errorMsg);
                if (this.Handler != null)
                {
                    this.Handler();
                }
            };
        }

        #region Properties

        public int ActorCount { get; set; }

        public bool FailedOnCreate { get; set; }
        #endregion

        protected override bool StrictModeCheck(out string errorMsg)
        {
            return this.StrictModeCheckWithDefer(out errorMsg);
        }
    }

    public class BeforeCloseGameCallInfo : TypedCallInfo<ICloseRequest>, IBeforeCloseGameCallInfo
    {
        private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        public BeforeCloseGameCallInfo(CallCounter counter, CallEnv env)
            : base(counter, Log, env)
        {
            this.OnFail = (errorMsg, objects) =>
            {
                Log.ErrorFormat(onFailLogCountGuard, "BeforeCloseGame call finished with Fail. Details: '{0}'", errorMsg);
                if (this.Handler != null)
                {
                    this.Handler();
                }
            };
        }

        public bool FailedOnCreate { get; set; }
    }

    public class BeforeJoinGameCallInfo : TypedCallInfo<IJoinGameRequest>, IBeforeJoinGameCallInfo
    {                      
        private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        public BeforeJoinGameCallInfo(CallCounter counter, CallEnv env)
            : base(counter, Log, env)
        {
        }
    }

    public class RaiseEventCallInfo : TypedCallInfo<IRaiseEventRequest>, IRaiseEventCallInfo
    {
        private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        public RaiseEventCallInfo(CallCounter counter, CallEnv env)
            : base(counter, Log, env)
        {
        }

        #region Properties

        /// <summary>
        ///   Gets or sets the actors which should receive the event.
        ///   If set to null or an empty array the event will be sent
        ///   to all actors in the room.
        /// </summary>
        /// <remarks>
        ///   Optional request parameter.
        /// </remarks>
        public int[] Actors
        {
            get { return this.Request.Actors; }
            set { this.Request.Actors = value; } 
        }

        /// <summary>
        ///   Gets or sets a value indicating how to use the <see cref = "EventCache" />.
        /// </summary>
        /// <remarks>
        ///   Optional request parameter.
        ///   Ignored if the event is sent to individual actors (submitted <see cref = "Actors" /> or <see cref = "Photon.Hive.Operations.ReceiverGroup.MasterClient" />).
        /// </remarks>
        public byte Cache
        {
            get { return this.Request.Cache; }
            set { this.Request.Cache = value; }
        }

        /// <summary>
        ///   Gets or sets the hashtable containing the data to send.
        /// </summary>
        /// <remarks>
        ///   Optional request parameter.
        /// </remarks>
        public object Data
        {
            get { return this.Request.Data; }
            set { this.Request.Data = value; }
        }

        /// <summary>
        ///   Gets or sets a byte containing the Code to send.
        /// </summary>
        /// <remarks>
        ///   Optional request parameter.
        /// </remarks>
        public byte EvCode
        {
            get { return this.Request.EvCode; }
            set { this.Request.EvCode = value; }
        }

        public bool IsCacheOpRemoveFromCache { get { return this.Cache == (byte)CacheOperation.RemoveFromRoomCache; } }

        public bool IsCacheOpRemoveFromCacheForActorsLeft { get { return this.Cache == (byte)CacheOperation.RemoveFromCacheForActorsLeft; } }

        public bool IsCacheSliceIndexOperation { get { return this.Cache >= (byte)CacheOperation.SliceIncreaseIndex; } }

        public bool IsCacheOnlyOperation
        {
            get
            {
                return this.IsCacheSliceIndexOperation
                    || this.IsCacheOpRemoveFromCache
                    || this.IsCacheOpRemoveFromCacheForActorsLeft;
            }
        }

        public bool IsBroadcastOperation { get { return !this.IsCacheOnlyOperation; } }
        #endregion

        protected override bool StrictModeCheck(out string errorMsg)
        {
            return this.StrictModeCheckWithDefer(out errorMsg);
        }
    }

    public class BeforeSetPropertiesCallInfo : TypedCallInfo<ISetPropertiesRequest>, IBeforeSetPropertiesCallInfo
    {
        private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        public BeforeSetPropertiesCallInfo(CallCounter counter, CallEnv env)
            : base(counter, Log, env)
        {
        }

        protected override bool StrictModeCheck(out string errorMsg)
        {
            return this.StrictModeCheckWithDefer(out errorMsg);
        }
    }

    public class SetPropertiesCallInfo : TypedCallInfo<ISetPropertiesRequest>, ISetPropertiesCallInfo
    {
        private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        public SetPropertiesCallInfo(CallCounter counter, CallEnv env)
            : base(counter, Log, env)
        {
        }

        #region Properties
        //        public byte OperationStatus { get; set; }
        #endregion
    }

    public class SetPropertiesFailedCallInfo : TypedCallInfo<ISetPropertiesRequest>, ISetPropertiesFailedCallInfo
    {
        private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        public SetPropertiesFailedCallInfo(CallCounter counter, CallEnv env)
            : base(counter, Log, env)
        {
        }
    }

    public class DisconnectCallInfo : TypedCallInfo<IOperationRequest>, IDisconnectCallInfo
    {
        private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        public DisconnectCallInfo(CallCounter counter, CallEnv env)
            : base(counter, Log, env)
        {
        }
    }
}