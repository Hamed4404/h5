// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.AspNetCore.Components.WebAssembly.Authentication
{
    /// <summary>
    /// Represents options to pass down to configure the oidc-client.js library used when using a standard OIDC flow.
    /// </summary>
    public class OidcProviderOptions
    {
        /// <summary>
        /// Gets or sets the authority of the OIDC identity provider.
        /// </summary>
        public string Authority { get; set; }

        /// <summary>
        /// Gets or sets the metadata url of the oidc provider.
        /// </summary>
        public string MetadataUrl { get; set; }

        /// <summary>
        /// Gets or sets the client of the application.
        /// </summary>
        [JsonPropertyName("client_id")]
        public string ClientId { get; set; }

        /// <summary>
        /// Gets or sets the list of scopes to request when signing in.
        /// </summary>
        public IList<string> DefaultScopes { get; } = new List<string> { "openid", "profile" };

        /// <summary>
        /// Gets or sets the redirect uri for the application. The application will be redirected here after the user has completed the sign in
        /// process from the identity provider.
        /// </summary>
        [JsonPropertyName("redirect_uri")]
        public string RedirectUri { get; set; }

        /// <summary>
        /// Gets or sets the post logout redirect uri for the application. The application will be redirected here after the user has completed the sign out
        /// process from the identity provider.
        /// </summary>
        [JsonPropertyName("post_logout_redirect_uri")]
        public string PostLogoutRedirectUri { get; set; }

        /// <summary>
        /// Gets or sets the response type to use on the authorization flow. The valid values are specified by the identity provider metadata.
        /// </summary>
        [JsonPropertyName("response_type")]
        public string ResponseType { get; set; }

        /// <summary>
        /// Gets or sets the response mode to use in the authorization flow.
        /// </summary>
        [JsonPropertyName("response_mode")]
        public string ResponseMode { get; set; }

        /// <summary>
        /// Gets or sets the extra query parameters to use on the authorization flow. 
        /// </summary>
        public IDictionary<string, string> ExtraQueryParams { get; } = new Dictionary<string, string>();
    }
}
