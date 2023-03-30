// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Components.WebAssembly.Services;

internal interface IInternalJSImportMethods
{
    string GetPersistedState();

    string GetApplicationEnvironment();

    byte[]? GetConfig(string configFile);

    void NavigationManager_EnableNavigationInterception();

    string NavigationManager_GetLocationHref();

    string NavigationManager_GetBaseUri();

    void NavigationManager_SetHasLocationChangingListeners(bool value);

    int RegisteredComponents_GetRegisteredComponentsCount();

    int RegisteredComponents_GetId(int index);

    string RegisteredComponents_GetAssembly(int id);

    string RegisteredComponents_GetTypeName(int id);

    string RegisteredComponents_GetParameterDefinitions(int id);

    string RegisteredComponents_GetParameterValues(int id);
}
