﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PropertyChangedEventArgs.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;

namespace Photon.Plugins.Common
{
    /// <summary>
    /// The property changed event args.
    /// </summary>
    /// <typeparam name="TKey">
    /// The property key type.
    /// </typeparam>
    public class PropertyChangedEventArgs<TKey> : EventArgs
    {
        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyChangedEventArgs{TKey}"/> class. 
        /// </summary>
        /// <param name="key">
        /// The property key.
        /// </param>
        /// <param name="value">
        /// The property value.
        /// </param>
        public PropertyChangedEventArgs(TKey key, object value)
        {
            this.Key = key;
            this.Value = value;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the key of the changed property.
        /// </summary>
        public TKey Key { get; private set; }

        /// <summary>
        /// Gets the value of the changed property.
        /// </summary>
        public object Value { get; private set; }

        #endregion
    }
}