﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace TriageBuildFailures.TeamCity
{
    [XmlRoot("tags")]
    public class TeamCityTags
    {
        [XmlElement("tag")]
        public List<TeamCityTag> TagList { get; set; }
    }
}
