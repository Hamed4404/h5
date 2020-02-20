using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.AspNetCore.WebUtilities
{
    public class MultipartReaderBenchmark
    {

        private const string Boundary = "9051914041544843365972754266";
        private const string OnePartBody =
    "--9051914041544843365972754266\r\n" +
    "Content-Disposition: form-data; name=\"text\"\r\n" +
    "\r\n" +
    "text default\r\n" +
    "--9051914041544843365972754266--\r\n";

        [Benchmark]
        public async Task ReadSmallMultipartAsyncStream()
        {
            var bytes = Encoding.UTF8.GetBytes(OnePartBody);
            var stream = new MemoryStream(bytes);

            for (var i = 0; i < 1000; i++)
            {
                var multipartReader = new MultipartReader(Boundary,stream);
                for (int j = 0; j < 2; j++)
                {
                    await multipartReader.ReadNextSectionAsync();
                }
                stream.Position = 0;
            }
        }

        [Benchmark]
        public async Task ReadSmallMultipartAsyncPipe()
        {
            var pipe = new Pipe();
            var bytes = Encoding.UTF8.GetBytes(OnePartBody);

            for (var i = 0; i < 1000; i++)
            {
                pipe.Writer.Write(bytes);
                pipe.Writer.Complete();
                var multipartReader = new MultipartPipeReader(Boundary,pipe.Reader);
                for (int j = 0; j < 2; j++)
                {
                    await multipartReader.ReadNextSectionAsync();
                }
                pipe.Reader.Complete();
                pipe.Reset();
            }
        }
    }
}
