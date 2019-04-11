﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using BasicWebSite;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.FunctionalTests
{
    public class ClientValidationOptionsTests : IClassFixture<MvcTestFixture<BasicWebSite.StartupClientValidationSettings>>
    {
        public ClientValidationOptionsTests(MvcTestFixture<StartupClientValidationSettings> fixture) => 
            Fixture = fixture;

        public MvcTestFixture<StartupClientValidationSettings> Fixture { get; }

        [Fact]
        public async Task DisablingClientValidation_DisablesItForPagesAndViews()
        {
            // Arrange
            var client = Fixture
                .WithWebHostBuilder(whb => whb.UseStartup<StartupClientValidationSettings>())
                .CreateClient();

            // Act
            var view = await client.GetStringAsync("Controller/ClientValidationDisabled");
            var page = await client.GetStringAsync("ClientvalidationDisabled");

            // Assert
            Assert.Equal("ClientValidationDisabled", view);
            Assert.Equal("ClientValidationDisabled", page);
        }
    }
}
