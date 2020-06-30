// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Components
{
    /// <summary>
    /// Default implementation of component activator.
    /// </summary>
    public class DefaultComponentActivator : IComponentActivator
    {
        /// <inheritdoc />
        public IComponent? CreateInstance(Type componentType)
        {
            return Activator.CreateInstance(componentType) as IComponent;
        }
    }
}
