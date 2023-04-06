﻿using IdeaStatiCa.BimApiLink.Identifiers;
using IdeaStatiCa.BimApi;
using System;
using System.Collections.Generic;

namespace IdeaStatiCa.BimApiLink.Plugin
{
	public class FeaUserSelection
	{
		public ICollection<Identifier<IIdeaNode>> Nodes { get; set; }
			= Array.Empty<Identifier<IIdeaNode>>();

		public ICollection<Identifier<IIdeaMember1D>> Members { get; set; }
			= Array.Empty<Identifier<IIdeaMember1D>>();

		public ICollection<Identifier<IIdeaCombiInput>> Combinations { get; set; }
			= Array.Empty<Identifier<IIdeaCombiInput>>();
	}
}