// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Core;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Mvc.Infrastructure
{
    /// <summary>
    /// A <see cref="IActionResultExecutor{RedirectToRouteResult}"/> for <see cref="RedirectToRouteResult"/>.
    /// </summary>
    public class RedirectToRouteResultExecutor : IActionResultExecutor<RedirectToRouteResult>
    {
        private readonly ILogger _logger;
        private readonly IUrlHelperFactory _urlHelperFactory;

        /// <summary>
        /// Initializes a new instance of <see cref="RedirectToRouteResultExecutor"/>.
        /// </summary>
        /// <param name="loggerFactory">The factory used to create loggers.</param>
        /// <param name="urlHelperFactory">The factory used to create url helpers.</param>
        public RedirectToRouteResultExecutor(ILoggerFactory loggerFactory, IUrlHelperFactory urlHelperFactory)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            if (urlHelperFactory == null)
            {
                throw new ArgumentNullException(nameof(urlHelperFactory));
            }

            _logger = loggerFactory.CreateLogger<RedirectToRouteResult>();
            _urlHelperFactory = urlHelperFactory;
        }

        /// <inheritdoc />
        public virtual Task ExecuteAsync(ActionContext context, RedirectToRouteResult result)
        {
            var urlHelper = result.UrlHelper ?? _urlHelperFactory.GetUrlHelper(context);

            var destinationUrl = urlHelper.RouteUrl(
                result.RouteName,
                result.RouteValues,
                protocol: null,
                host: null,
                fragment: result.Fragment);
            if (string.IsNullOrEmpty(destinationUrl))
            {
                throw new InvalidOperationException(Resources.NoRoutesMatched);
            }

            _logger.RedirectToRouteResultExecuting(destinationUrl, result.RouteName);

            if (result.PreserveMethod)
            {
                context.HttpContext.Response.StatusCode = result.Permanent ?
                    StatusCodes.Status308PermanentRedirect : StatusCodes.Status307TemporaryRedirect;
                context.HttpContext.Response.Headers.Location = destinationUrl;
            }
            else
            {
                context.HttpContext.Response.Redirect(destinationUrl, result.Permanent);
            }

            return Task.CompletedTask;
        }
    }
}
