﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AuthenticateRequest.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the AuthenticateRequest type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using Photon.Common.Authentication;
using Photon.Hive.Operations;
using Photon.SocketServer;
using Photon.SocketServer.Rpc;

namespace Photon.LoadBalancing.Operations
{
    public class AuthenticateRequest : Operation, IAuthenticateRequest
    {
        #region Constructors and Destructors

        public AuthenticateRequest(IRpcProtocol protocol, OperationRequest operationRequest)
            : base(protocol, operationRequest)
        {
            this.ApplicationId = this.ApplicationId?.Trim();
            this.ApplicationVersion = this.ApplicationVersion?.Trim();
        }

        public AuthenticateRequest()
        {
        }

        #endregion

        #region Serialized Properties

        [DataMember(Code = (byte)ParameterKey.ApplicationId, IsOptional = true)]
        public string ApplicationId { get; set; }

        [DataMember(Code = (byte)ParameterKey.AppVersion, IsOptional = true)]
        public string ApplicationVersion { get; set; }

        [DataMember(Code = (byte)ParameterKey.Token, IsOptional = true)]
        public string Token { get; set; }

        [DataMember(Code = (byte)ParameterKey.UserId, IsOptional = true)]
        public string UserId { get; set; }

        [DataMember(Code = (byte)ParameterKey.ClientAuthenticationType, IsOptional = true)]
        public byte ClientAuthenticationType { get; set; }

        [DataMember(Code = (byte)ParameterKey.ClientAuthenticationParams, IsOptional = true)]
        public string ClientAuthenticationParams { get; set; }

        [DataMember(Code = (byte)ParameterKey.ClientAuthenticationData, IsOptional = true)]
        public object ClientAuthenticationData { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the client wants to receive lobby statistics.
        /// </summary>
        [DataMember(Code = (byte)ParameterKey.LobbyStats, IsOptional = true)]
        public bool ReceiveLobbyStatistics { get; set; }
        
        [DataMember(Code = (byte)ParameterKey.Region, IsOptional = true)]
        public virtual string Region { get; set; }

        [DataMember(Code = (byte)ParameterKey.Flags, IsOptional = true)]
        public int Flags { get; set; }

        [DataMember(Code = (byte)ParameterKey.ExpectedProtocol, IsOptional = true)]
        public byte? ExpectedProtocol { get; set; }

        #endregion

        #region Helper Properties

        public bool IsTokenAuthUsed => this.ClientAuthenticationType == 255 || !string.IsNullOrEmpty(this.Token);

        #endregion
    }
}