﻿using IdeaStatica.BimApiLink.BimApi;
using IdeaStatiCa.BimApi;

namespace BimApiLinkFeaExample.BimApi
{
	internal class CrossSectionByName : IdeaCrossSectionByName
	{
		public override IIdeaMaterial Material => Get<IIdeaMaterial>(MaterialNo);

		public int MaterialNo { get; init; }

		public CrossSectionByName(int no) : base(no)
		{
		}
	}
}