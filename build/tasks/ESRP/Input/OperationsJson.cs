﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.Build.OOB.ESRP
{
    public class OperationsJson
    {
        private string _opusInfo;
        private string _opusName;
        private Dictionary<string, Operations[]> _operationsJson;

        public static readonly string DefaultOpusName = "Microsoft";
        public static readonly string DefaultOpusInfo = "https://www.microsoft.com/";

        public Operations[] this[string key]
        {
            get
            {
                if (_operationsJson == null)
                {
                    InitOperations();
                }

                Operations[] value = null;
                _operationsJson.TryGetValue(key, out value);

                return value;
            }
        }

        public OperationsJson() : this(DefaultOpusInfo, DefaultOpusName)
        {
        }

        public OperationsJson(string opusInfo, string opusName)
        {
            _opusInfo = String.IsNullOrEmpty(opusInfo) ? DefaultOpusInfo : opusInfo;
            _opusName = String.IsNullOrEmpty(opusName) ? DefaultOpusName : opusName;
            InitOperations();
        }

        public bool ContainsKey(string key)
        {
            return _operationsJson.ContainsKey(key);
        }

        private void InitOperations()
        {
            // Classic ID: 25, 67
            var _microsoftSN = new Operations[]
            {
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_235845_SN,
                    OperationCode = ESRP.OperationCode.StrongNameSign
                },
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_235845_SN,
                    OperationCode = ESRP.OperationCode.StrongNameVerify,
                }
            };

            // Classic ID: 72
            var _microsoftSharedLibrariesSN = new Operations[]
            {
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_233863_SN,
                    OperationCode = OperationCode.StrongNameSign
                },
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_233863_SN,
                    OperationCode = OperationCode.StrongNameVerify
                }
            };

            // Classic ID: 400
            var _microsoftAuthentiCodeSha2 = new Operations[]
            {
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_230012,
                    OperationCode = OperationCode.SigntoolSign,
                    Parameters = new JObject
                    {
                        { "OpusName", _opusName },
                        { "OpusInfo", _opusInfo },
                        { "FileDigest", @"/fd ""SHA256""" },
                        { "PageHash", "/NPH" },
                        { "TimeStamp", @"/tr ""http://rfc3161.gtm.corp.microsoft.com/TSS/HttpTspServer"" /td sha256" }
                    },
                },
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_230012,
                    OperationCode = OperationCode.SigntoolVerify
                }
            };

            // Classic ID: 401
            var _microsoftAuthentiCodeSha1Sha2 = new Operations[]
            {
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_229803,
                    OperationCode = OperationCode.SigntoolSign,
                    Parameters = new JObject
                    {
                        { "OpusName", "Microsoft" },
                        { "OpusInfo", "http://www.microsoft.com" },
                        { "PageHash", "/NPH" },
                        { "TimeStamp", @"/t ""http://ts4096.gtm.microsoft.com/TSS/AuthenticodeTS""" }
                    },
                },
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_230012,
                    OperationCode = OperationCode.SigntoolSign,
                    Parameters = new JObject
                    {
                        { "OpusName", "Microsoft" },
                        { "OpusInfo", "http://www.microsoft.com" },
                        { "Append", "/as" },
                        { "FileDigest", @"/fd ""SHA256""" },
                        { "PageHash", "/NPH" },
                        { "TimeStamp", @"/tr ""http://rfc3161.gtm.corp.microsoft.com/TSS/HttpTspServer"" /td sha256" }
                    }
                },
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_230012,
                    OperationCode = OperationCode.SigntoolVerify,
                    Parameters = new JObject
                    {
                        { "VerifyAll", "/all" }
                    }
                }
            };

            // Classic ID: 402
            var _microsoftAuthentiCodeSha2HashSha1 = new Operations[]
            {
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_230012,
                    OperationCode = OperationCode.SigntoolSign,
                    Parameters = new JObject
                    {
                        { "OpusName", _opusName },
                        { "OpusInfo", _opusInfo },
                        { "PageHash", "/NPH" },
                        { "TimeStamp", @"/t ""http://ts4096.gtm.microsoft.com/TSS/AuthenticodeTS""" }
                    }
                },
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_230012,
                    OperationCode = OperationCode.SigntoolVerify
                }
            };

            // Classic ID: N/A
            var _microsoftNuGet = new Operations[]
            {
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_401405,
                    OperationCode = OperationCode.NuGetSign
                },
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_401405,
                    OperationCode = OperationCode.NuGetVerify
                }
            };

            // Classic ID: 160
            var _microsoftOpc = new Operations[]
            {
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_299834,
                    OperationCode = OperationCode.OpcSign
                },
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_299834,
                    OperationCode = OperationCode.OpcVerify
                }
            };

            // Classic ID: 100040160
            var _microsoftOpc2 = new Operations[]
            {
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_233016,
                    OperationCode = OperationCode.OpcSign,
                    Parameters = new JObject
                    {
                        { "FileDigest", "/fd SHA256" }
                    }
                },
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_233016,
                    OperationCode = OperationCode.OpcVerify
                }
            };

            // Classic ID: 135020002
            var _microsoft3rdPartyAppComponent = new Operations[]
            {
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_231522,
                    OperationCode = OperationCode.SigntoolSign,
                    Parameters = new JObject
                    {
                        { "OpusName", _opusName },
                        { "OpusInfo", _opusInfo },
                        { "Append", "/as" },
                        { "FileDigest", @"/fd ""SHA256""" },
                        { "PageHash", "/NPH" },
                        { "TimeStamp", @"/tr ""http://rfc3161.gtm.corp.microsoft.com/TSS/HttpTspServer""" }
                    }
                },
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_231522,
                    OperationCode = OperationCode.SigntoolVerify,
                }
            };

            // Classic ID: 135020001
            var _microsoft3rdPartyAppComponentDual = new Operations[]
            {
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_231522,
                    OperationCode = OperationCode.SigntoolSign,
                    Parameters = new JObject
                    {
                        { "OpusName", _opusName },
                        { "OpusInfo", _opusInfo },
                        { "Append", "/as" },
                        { "PageHash", "/NPH" },
                        { "TimeStamp", @"/tr ""http://rfc3161.gtm.corp.microsoft.com/TSS/HttpTspServer""" }
                    }
                },
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_231514,
                    OperationCode = OperationCode.SigntoolSign,
                    Parameters = new JObject
                    {
                        { "OpusName", _opusName },
                        { "OpusInfo", _opusInfo },
                        { "Append", "/as" },
                        { "FileDigest", @"/fd ""SHA256""" },
                        { "PageHash", "/NPH" },
                        { "TimeStamp", @"/tr ""http://rfc3161.gtm.corp.microsoft.com/TSS/HttpTspServer"" /td sha256" }
                    }
                },
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_231514,
                    OperationCode = OperationCode.SigntoolVerify,
                    Parameters = new JObject
                    {
                        { "VerifyAll", "/all" }
                    }
                }
            };

            // Classic ID: 170
            var _microsoftJava = new Operations[]
            {
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_232612_Java,
                    OperationCode = OperationCode.JavaSign,
                    Parameters = new JObject
                    {
                        { "SigAlg", "SHA256withRSA" },
                        { "TimeStamp", "-tsa http://sha256timestamp.ws.symantec.com/sha256/timestamp"}        
                    }
                },
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_232612_Java,
                    OperationCode = OperationCode.JavaVerify
                }
            };

            // Classic ID: 8003
            var _microsoftMacAppDeveloper = new Operations[]
            {
                new Operations
                {
                    KeyCode = ESRP.KeyCode.CP_401337_Apple,
                    OperationCode = OperationCode.MacAppDeveloperSign,
                    ToolName = ToolName.Sign
                }
            };

            _operationsJson = new Dictionary<string, Operations[]> {
                { "MicrosoftSN", _microsoftSN },
                { "MicrosoftSharedLibrariesSN", _microsoftSharedLibrariesSN },
                { "MicrosoftAuthentiCodeSha2", _microsoftAuthentiCodeSha2 },
                { "MicrosoftAuthentiCodeSha1Sha2", _microsoftAuthentiCodeSha1Sha2 },
                { "MicrosoftAuthentiCodeSha2HashSha1", _microsoftAuthentiCodeSha2HashSha1 },
                { "MicrosoftNuGet", _microsoftNuGet },
                { "MicrosoftOpc", _microsoftOpc },
                { "MicrosoftOpc2", _microsoftOpc2 },
                { "Microsoft3rdPartyAppComponent", _microsoft3rdPartyAppComponent },
                { "Microsoft3rdPartyAppComponentDual", _microsoft3rdPartyAppComponentDual },
                { "MicrosoftJava", _microsoftJava },
                { "MicrosoftMacAppDeveloper", _microsoftMacAppDeveloper }
            };
        }
    }
}
