// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.JSInterop.Infrastructure;
using Moq;
using Xunit;

namespace Microsoft.JSInterop
{
    public class JSInProcessObjectReferenceExtensionsTest
    {
        [Fact]
        public void InvokeVoid_Works()
        {
            // Arrange
            var method = "someMethod";
            var args = new[] { "a", "b" };
            var jsInProcessObjectReference = new Mock<IJSInProcessObjectReference>(MockBehavior.Strict);
            jsRuntime.Setup(s => s.Invoke<IJSVoidResult>(method, args)).Returns(Mock.Of<IJSVoidResult>());

            // Act
            jsInProcessObjectReference.Object.InvokeVoid(method, args);

            jsInProcessObjectReference.Verify();
        }
    }
}
