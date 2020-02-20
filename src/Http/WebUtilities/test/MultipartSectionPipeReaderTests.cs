// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.AspNetCore.WebUtilities
{
    public class MultipartSectionPipeReaderTest
    {
        private const string Boundary = "9051914041544843365972754266";
        // Note that CRLF (\r\n) is required. You can't use multi-line C# strings here because the line breaks on Linux are just LF.
        private const string Text = "text default";
        private const string TextAndBoundary =
Text +
"\r\n--9051914041544843365972754266--\r\n";
        private const string HtmlWithNewLines = "<!DOCTYPE html>\r\n<title>Content of a.html.</title>\r\n";
        private const string HtmlWithNewLinesAndBoundary =
HtmlWithNewLines +
"\r\n--9051914041544843365972754266--\r\n";
        private const string TextWithPartialBoundaryMatch = "text default\r\n--90519140415448433-MoreData";
        private const string TextWithPartialBoundaryMatchAndBoundary =
TextWithPartialBoundaryMatch +
"\r\n--9051914041544843365972754266--\r\n";

        private static PipeReader MakeReader(string text)
        {
            return PipeReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(text)));
        }

        private static string GetString(ReadOnlySequence<byte> buffer)
        {
            return Encoding.ASCII.GetString(buffer);
        }

        [Theory]
        [InlineData(TextAndBoundary, Text)]
        [InlineData(HtmlWithNewLinesAndBoundary, HtmlWithNewLines)]
        [InlineData(TextWithPartialBoundaryMatchAndBoundary, TextWithPartialBoundaryMatch)]
        public async Task MultipartSectionPipeReader_ValidBody_Success(string input, string expected)
        {
            var pipeReader = MakeReader(input);
            var sectionReader = new MultipartSectionPipeReader(pipeReader, new MultipartBoundary(Boundary));

            var result = await sectionReader.ReadAsync();
            Assert.False(result.IsCompleted);
            Assert.False(result.IsCanceled);
            Assert.False(result.Buffer.IsEmpty);

            var actual = GetString(result.Buffer);
            Assert.Equal(expected, actual);

            sectionReader.AdvanceTo(result.Buffer.End);
            result = await sectionReader.ReadAsync();
            Assert.True(result.IsCompleted);
            Assert.False(result.IsCanceled);
            Assert.True(result.Buffer.IsEmpty);
        }
    }
}
