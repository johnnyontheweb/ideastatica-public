﻿using IdeaStatiCa.BimApi;
using IdeaStatiCa.BimApi.Results;
using IdeaStatiCa.RamToIdea.BimApi;
using IdeaStatiCa.RamToIdea.Providers;
using IdeaStatiCa.RamToIdea.Utilities;
using MathNet.Numerics;
using RAMDATAACCESSLib;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace IdeaStatiCa.RamToIdea.Factories
{
	internal class ResultsFactory : IResultsFactory
	{
		private readonly IForces1 _forces1;
		private readonly IForces2 _forces2;
		private readonly ILoadsProvider _loadsProvider;
		private readonly IObjectFactory _objectFactory;

		public ResultsFactory(IObjectFactory objectFactory, ILoadsProvider loadsProvider, IForces1 forces1, IForces2 forces2)
		{
			_objectFactory = objectFactory;
			_loadsProvider = loadsProvider;
			_forces1 = forces1;
			_forces2 = forces2;
		}

		public IEnumerable<IIdeaResult> GetResultsForBeam(IBeam ramBeam, IIdeaMember1D ideaMember)
		{
			// Get all load cases
			var loadCases = _loadsProvider.GetLoadCases();

			// Get sections, where results are required
			var locations = GetResultLocations(ideaMember);

			// results for beam - sections - loadcases
			var sections = new List<IIdeaSection>(locations.Count);
			var results = new RamResult
			{
				CoordinateSystemType = IdeaRS.OpenModel.Result.ResultLocalSystemType.Principle,
				Sections = sections,
			};

			if (loadCases.Any())
			{
				foreach (var local in locations)
				{
					var sectionResults = new List<IIdeaSectionResult>(10);
					var section = new RamSection { Position = local.TargetRelPosition, Results = sectionResults, };
					sections.Add(section);
					foreach (var loadCase in loadCases)
					{
						var ideaLoadCase = _objectFactory.GetLoadCase(loadCase.lUID);
						if (ramBeam.eFramingType == EFRAMETYPE.MemberIsLateral)
						{
							double pdAxial = 0.0, pdMajMom = 0.0, pdMinMom = 0.0, pdMajShear = 0.0, pdMinShear = 0.0, pdTorsion = 0.0;
							_forces1.GetLatBeamForcesLeftAt(ramBeam.lUID, loadCase.lAnalyzeNo, local.SourceRelPosition, ref pdAxial, ref pdMajMom, ref pdMinMom, ref pdMajShear, ref pdMinShear, ref pdTorsion);
							sectionResults.Add(new RamSectionResult(ideaLoadCase, CreateInternalForces(pdAxial, pdMinShear, pdMajShear, pdTorsion, pdMajMom, pdMinMom)));
						}
						else if (ramBeam.eFramingType == EFRAMETYPE.MemberIsGravity)
						{
							double pdDeadMoment = 0.0, pdDeadShear = 0.0, pdCDMoment = 0.0, pdCDShear = 0.0, pdCLMoment = 0.0, pdCLShear = 0.0, pdPosLiveMoment = 0.0, pdPosLiveShear = 0.0, pdNegLiveMoment = 0.0, pdNegLiveShear = 0.0;

							// Skip force extraction for concrete gravity columns for now, because of a bug in the RAM API
							if (ramBeam.eMaterial != EMATERIALTYPES.EConcreteMat)
							{
								_forces1.GetGravBeamForcesLeftAt(ramBeam.lUID, local.SourceRelPosition, ref pdDeadMoment, ref pdDeadShear, ref pdCDMoment, ref pdCDShear, ref pdCLMoment, ref pdCLShear, ref pdPosLiveMoment, ref pdPosLiveShear, ref pdNegLiveMoment, ref pdNegLiveShear);
							}

							switch (loadCase.eLoadType)
							{
								case ELoadCaseType.DeadLCa:
									sectionResults.Add(new RamSectionResult(ideaLoadCase, CreateInternalForces(0.0, 0.0, pdDeadShear, 0.0, pdDeadMoment, 0.0)));
									break;

								case ELoadCaseType.LiveLCa:
								case ELoadCaseType.LiveReducibleLCa:
								case ELoadCaseType.LiveRoofLCa:
								case ELoadCaseType.LiveStorageLCa:
								case ELoadCaseType.LiveUnReducibleLCa:
									double liveShear = IsPositiveLoad(loadCase) ? pdPosLiveShear : pdNegLiveShear;
									double liveMoment = IsPositiveLoad(loadCase) ? pdPosLiveMoment : pdNegLiveMoment;

									sectionResults.Add(new RamSectionResult(ideaLoadCase, CreateInternalForces(0.0, 0.0, liveShear, 0.0, liveMoment, 0.0)));
									break;

								case ELoadCaseType.ConstructionDeadLCa:
									sectionResults.Add(new RamSectionResult(ideaLoadCase, CreateInternalForces(0.0, 0.0, pdCDShear, 0.0, pdCDMoment, 0.0)));
									break;

								case ELoadCaseType.ConstructionLiveLCa:
									sectionResults.Add(new RamSectionResult(ideaLoadCase, CreateInternalForces(0.0, 0.0, pdCLShear, 0.0, pdCLMoment, 0.0)));
									break;

								default:
									sectionResults.Add(new RamSectionResult(ideaLoadCase, CreateInternalForces()));
									break;
							}
						}
					}
				}
			}

			return new IIdeaResult[] { results };
		}

		public IEnumerable<IIdeaResult> GetResultsForColumn(IColumn column, IIdeaMember1D ideaMember)
		{
			// Get all load cases
			var loadCases = _loadsProvider.GetLoadCases();

			// Get sections, where results are required
			var locations = GetResultLocations(ideaMember, divideSmoothly: false);

			// Results for beam - sections - loadcases
			var sections = new List<IIdeaSection>(locations.Count);
			var results = new RamResult
			{
				CoordinateSystemType = IdeaRS.OpenModel.Result.ResultLocalSystemType.Principle,
				Sections = sections,
			};

			if (loadCases.Any() && locations.Count > 1)
			{
				// Column has only 2 sections - at the begin and at the end of the member, but we need to interpolate them along all elements
				sections.AddRange(locations.Select(local =>
				{
					var sectionResults = new List<IIdeaSectionResult>(10);
					var section = new RamSection { Position = local.TargetRelPosition, Results = sectionResults, };
					return section;
				}));

				foreach (var loadCase in loadCases)
				{
					var ideaLoadCase = _objectFactory.GetLoadCase(loadCase.lUID);
					if (column.eFramingType == EFRAMETYPE.MemberIsLateral)
					{
						double pdAxialI = 0.0, pdMajMomI = 0.0, pdMinMomI = 0.0, pdMajShearI = 0.0, pdMinShearI = 0.0, pdTorsionI = 0.0, pdAxialJ = 0.0, pdMajMomJ = 0.0, pdMinMomJ = 0.0, pdMajShearJ = 0.0, pdMinShearJ = 0.0, pdTorsionJ = 0.0;

						_forces2.GetColForcesForLCase(column.lUID, loadCase.lAnalyzeNo, ref pdAxialI, ref pdMajMomI, ref pdMinMomI, ref pdMajShearI, ref pdMinShearI, ref pdTorsionI, ref pdAxialJ, ref pdMajMomJ, ref pdMinMomJ, ref pdMajShearJ, ref pdMinShearJ, ref pdTorsionJ);

						BuildInterpolatedResults(
							ideaLoadCase,
							CreateInternalForces(pdAxialI, pdMinShearI, pdMajShearI, pdTorsionI, pdMajMomI, pdMinMomI),
							CreateInternalForces(pdAxialJ, pdMinShearJ, pdMajShearJ, pdTorsionJ, pdMajMomJ, pdMinMomJ),
							sections);
					}
					else if (column.eFramingType == EFRAMETYPE.MemberIsGravity)
					{
						double pdDead = 0.0, pdPosLLRed = 0.0, pdPosLLNonRed = 0.0, pdPosLLStorage = 0.0, pdPosLLRoof = 0.0, pdNegLLRed = 0.0, pdNegLLNonRed = 0.0, pdNegLLStorage = 0.0, pdNegLLRoof = 0.0;

						// Skip force extraction for concrete gravity columns for now, because of a bug in the RAM API
						if (column.eMaterial != EMATERIALTYPES.EConcreteMat)
						{
							_forces1.GetGrvColForcesForLCase(column.lUID, ref pdDead, ref pdPosLLRed, ref pdPosLLNonRed, ref pdPosLLStorage, ref pdPosLLRoof, ref pdNegLLRed, ref pdPosLLNonRed, ref pdNegLLStorage, ref pdNegLLRoof);
						}

						double nx = 0;
						switch (loadCase.eLoadType)
						{
							case ELoadCaseType.DeadLCa:
								nx = pdDead;
								break;

							case ELoadCaseType.LiveLCa:
							case ELoadCaseType.LiveUnReducibleLCa:
								nx = IsPositiveLoad(loadCase) ? pdPosLLNonRed : pdNegLLNonRed;
								break;

							case ELoadCaseType.LiveReducibleLCa:
								nx = IsPositiveLoad(loadCase) ? pdPosLLRed : pdNegLLRed;
								break;

							case ELoadCaseType.LiveRoofLCa:
								nx = IsPositiveLoad(loadCase) ? pdPosLLRoof : pdNegLLRoof;
								break;

							case ELoadCaseType.LiveStorageLCa:
								nx = pdNegLLStorage;
								break;
						}

						BuildInterpolatedResults(
							ideaLoadCase,
							CreateInternalForces(nx),
							CreateInternalForces(nx),
							sections);
					}
				}
			}

			return new IIdeaResult[] { results };
		}

		public IEnumerable<IIdeaResult> GetResultsForHorizontalBrace(IHorizBrace brace, IIdeaMember1D ideaMember)
		{
			return GetBraceResults(brace.lUID, ideaMember);
		}

		public IEnumerable<IIdeaResult> GetResultsForVerticalBrace(IVerticalBrace brace, IIdeaMember1D ideaMember)
		{
			return GetBraceResults(brace.lUID, ideaMember);
		}

		private static void AddResultToSection(IIdeaLoadCase ideaLoadCase, InternalForcesData sectionResult, IIdeaSection section)
		{
			var results = section.Results as IList<IIdeaSectionResult>;
			results.Add(new RamSectionResult(ideaLoadCase, sectionResult));
		}

		private static void BuildInterpolatedResults(IIdeaLoadCase ideaLoadCase, InternalForcesData sectionResults0, InternalForcesData sectionResults1, List<IIdeaSection> sections)
		{
			// Add results at the begin of member
			AddResultToSection(ideaLoadCase, sectionResults0, sections[0]);

			for (int i = 1; i < sections.Count - 1; i++)
			{
				// Interpolate intermediate sections
				var interpolated = Interpolate(0, sectionResults0, 1, sectionResults1, sections[i].Position);

				AddResultToSection(ideaLoadCase, interpolated, sections[i]);
			}

			// Add results at the end of member
			AddResultToSection(ideaLoadCase, sectionResults1, sections.Last());
		}

		private static InternalForcesData CreateInternalForces(double nx = 0, double vy = 0, double vz = 0, double mx = 0, double my = 0, double mz = 0)
		{
			return new InternalForcesData
			{
				N = -nx.KipsToNewtons(),
				Qy = vy.KipsToNewtons(),
				Qz = vz.KipsToNewtons(),
				Mx = mx.KipsToNewtons().InchesToMeters(),
				My = my.KipsToNewtons().InchesToMeters(),
				Mz = mz.KipsToNewtons().InchesToMeters(),
			};
		}

		private static IList<SectionMap> GetResultLocations(IIdeaMember1D ideaMember, bool divideSmoothly = true)
		{
			const double MaxSectionsDistance = 0.5;
			var locations = new List<SectionMap>();
			if (ideaMember.Elements?.Count > 0)
			{
				var points = new List<IIdeaNode>(ideaMember.Elements.Count * 2);
				double length = 0;

				// collect all nodes from elements
				foreach (var e in ideaMember.Elements)
				{
					var geometry = e.Segment;
					length += (geometry.EndNode.ToMNVector() - geometry.StartNode.ToMNVector()).Length;
					points.Add(geometry.StartNode);
					points.Add(geometry.EndNode);
				}

				if (length == 0)
				{
					return locations;
				}

				var builder = new SectionBuilder(length);

				// distinct same nodes
				points = points.Distinct().ToList();
				var firstPoint = points[0].ToMNVector();

				// begin position
				locations.Add(builder.CreateSingleSection(0));

				// intermediate nodes
				for (var i = 1; i < points.Count - 1; ++i)
				{
					var point = points[i].ToMNVector();
					var pos = (point - firstPoint).Length;

					// intermediate possitions means connected beam - we add position a bit before and a bit after section
					locations.AddRange(builder.CreateDoubleSection(pos));
				}

				// position at the end of the member
				locations.Add(builder.CreateSingleSection(length));

				if (divideSmoothly)
				{
					// add some sections, if distance between locations is too big
					var count = locations.Count;
					for (var i = count - 2; i >= 0; --i)
					{
						var dist = locations[i + 1].SourceAbsPosition - locations[i].SourceAbsPosition;
						if (dist > MaxSectionsDistance)
						{
							var n = (int)(dist / MaxSectionsDistance);
							var d = dist / n;
							for (int j = n - 1; j > 0; --j)
							{
								locations.Insert(i + 1, builder.CreateSingleSection(locations[i].SourceAbsPosition + d * j));
							}
						}
					}
				}
			}

			return locations;
		}

		private static InternalForcesData Interpolate(double section0, InternalForcesData value0, double section1, InternalForcesData value1, double sectionX)
		{
			var interpolated = new InternalForcesData
			{
				N = Interpolate(section0, value0.N, section1, value1.N, sectionX, false),
				Qy = Interpolate(section0, value0.Qy, section1, value1.Qy, sectionX, false),
				Qz = Interpolate(section0, value0.Qz, section1, value1.Qz, sectionX, false),
				Mx = Interpolate(section0, value0.Mx, section1, value1.Mx, sectionX, false),
				My = Interpolate(section0, value0.My, section1, value1.My, sectionX, false),
				Mz = Interpolate(section0, value0.Mz, section1, value1.Mz, sectionX, false),
			};
			return interpolated;
		}

		private static double Interpolate(double xa, double ya, double xb, double yb, double x, bool extrapolate)
		{
			if (!xa.AlmostEqual(xb))
			{
				if (!extrapolate)
				{
					if (xa > xb)
					{
						Swap(ref xa, ref xb);
						Swap(ref ya, ref yb);
					}

					if (x <= xa)
					{
						return ya;
					}
					else if (x >= xb)
					{
						return yb;
					}
				}

				return ya + ((yb - ya) / (xb - xa) * (x - xa));
			}
			else
			{
				if (x <= xa)
				{
					return ya;
				}
				else
				{
					return yb;
				}
			}
		}

		private static bool IsPositiveLoad(ILoadCase loadCase)
		{
			return loadCase.eLoadDirectionSubType == EAnalysisSubType.Positive;
		}

		private static void Swap<T>(ref T lhs, ref T rhs)
		{
			T temp;
			temp = lhs;
			lhs = rhs;
			rhs = temp;
		}

		private IEnumerable<IIdeaResult> GetBraceResults(int uid, IIdeaMember1D ideaMember)
		{
			// Get all load cases
			var loadCases = _loadsProvider.GetLoadCases();

			// Get sections, where results are required
			var locations = GetResultLocations(ideaMember, divideSmoothly: false);

			// results for beam - sections - loadcases
			var sections = new List<IIdeaSection>(2);
			var results = new RamResult
			{
				CoordinateSystemType = IdeaRS.OpenModel.Result.ResultLocalSystemType.Principle,
				Sections = sections,
			};

			if (loadCases.Any())
			{
				// for braces are only 2 sections - at the begin and at the end of the member, but we need to interpolate them along all elements
				sections.AddRange(locations.Select(local =>
				{
					var sectionResults = new List<IIdeaSectionResult>(10);
					var section = new RamSection { Position = local.TargetRelPosition, Results = sectionResults, };
					return section;
				}));

				foreach (var loadCase in loadCases)
				{
					var ideaLoadCase = _objectFactory.GetLoadCase(loadCase.lUID);

					double pdAxial = 0.0, pdMajMomTop = 0.0, pdMinMomTop = 0.0, pdMajShearTop = 0.0, pdMinShearTop = 0.0, pdTorsionTop = 0.0, pdMajMomBot = 0.0, pdMinMomBot = 0.0, pdMajShearBot = 0.0, pdMinShearBot = 0.0, pdTorsionBot = 0.0;
					_forces2.GetLatBraceForces(uid, loadCase.lAnalyzeNo, ref pdAxial, ref pdMajMomTop, ref pdMinMomTop, ref pdMajShearTop, ref pdMinShearTop, ref pdTorsionTop, ref pdMajMomBot, ref pdMinMomBot, ref pdMajShearBot, ref pdMinShearBot, ref pdTorsionBot);

					BuildInterpolatedResults(
						ideaLoadCase,
						CreateInternalForces(pdAxial, pdMinShearBot, pdMajShearBot, pdTorsionBot, pdMajMomBot, pdMinMomBot),
						CreateInternalForces(pdAxial, pdMinShearTop, pdMajShearTop, pdTorsionTop, pdMajMomTop, pdMinMomTop),
						sections);
				}
			}

			return new IIdeaResult[] { results };
		}

		internal class SectionBuilder
		{
			private readonly double memberLength;

			public SectionBuilder(double memberLength)
			{
				this.memberLength = memberLength;
			}

			public double MinSourceSectionsDistance { get; set; } = 5e-4;
			public double MinTargetSectionsDistance { get; set; } = 1e-6;

			public IList<SectionMap> CreateDoubleSection(double absolutePosition)
			{
				var before = new SectionMap(absolutePosition - MinSourceSectionsDistance * memberLength, absolutePosition - MinTargetSectionsDistance * memberLength, memberLength);
				var after = new SectionMap(absolutePosition + MinSourceSectionsDistance * memberLength, absolutePosition + MinTargetSectionsDistance * memberLength, memberLength);
				return new SectionMap[] { before, after, };
			}

			public SectionMap CreateSingleSection(double absolutePosition)
			{
				return new SectionMap(absolutePosition, memberLength);
			}
		}

		/// <summary>
		/// Provides mapping of sections in the source FEA application to the target IDEA Checkbot application.
		/// </summary>
		[DebuggerDisplay("{SourceAbsPosition / SourceRelPosition}")]
		internal class SectionMap
		{
			private readonly double memberLength;

			public SectionMap(double uniformAbsPosition, double memberLength) : this(uniformAbsPosition, uniformAbsPosition, memberLength)
			{
			}

			public SectionMap(double sourceAbsPosition, double targetAbsPosition, double memberLength)
			{
				SourceAbsPosition = sourceAbsPosition;
				TargetAbsPosition = targetAbsPosition;
				this.memberLength = memberLength;
			}

			/// <summary>
			/// Gets the section position in the source appliction, that is used for reading of results from the source appliction.
			/// </summary>
			public double SourceAbsPosition { get; }

			public double SourceRelPosition => SourceAbsPosition / memberLength;

			/// <summary>
			/// Gets the section position in the target appliction, that is used for writing of results to the IDEA Checkbot.
			/// </summary>
			public double TargetAbsPosition { get; }

			public double TargetRelPosition => TargetAbsPosition / memberLength;
		}
	}
}