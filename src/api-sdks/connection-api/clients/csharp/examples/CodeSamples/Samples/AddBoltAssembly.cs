﻿using IdeaRS.OpenModel.Material;
using IdeaStatiCa.Api.Connection.Model.Material;
using IdeaStatiCa.ConnectionApi;
namespace CodeSamples
{
	public partial class ClientExamples
	{
		/// <summary>
		/// Get the list of avaliable Bolt Assemblies in a project and Add one to the project. 
		/// </summary>
		/// <param name="conClient">The connected API Client</param>
		public static async Task GetAndAddBoltAssemblies(IConnectionApiClient conClient)
		{
			string filePath = "Inputs/simple cleat connection.ideaCon";
			await conClient.Project.OpenProjectAsync(filePath);

			//Create a map of Name and Id. Used if Prefer to use Name.
			Dictionary<string, int> BoltGradesMap = new Dictionary<string, int>();
			Dictionary<string, int> BoltAssembliesMap = new Dictionary<string, int>();

			//TODO Upgrade to new IOM Bolt Grade Definitions.
			await conClient.Material.GetBoltGradeMaterialsAsync(conClient.ActiveProjectId);

			//GET AND ADD NEW BOLT ASSEMBLIES
			List<BoltAssembly> boltAssemblies = (await conClient.Material.GetBoltAssembliesAsync(conClient.ActiveProjectId)).Cast<BoltAssembly>().ToList();
			boltAssemblies.ForEach(x => BoltAssembliesMap.Add(x.Name, x.Id));

			//List of new bolt Assemblies to Add.
			List<string> boltAssembliesToAdd = new List<string>() { "M16 8.8", "M20 8.8", "M24 8.8" };

			foreach (var assembly in boltAssembliesToAdd)
			{
				//Only Add Assemblies which are not in the model currently.
				if (!BoltAssembliesMap.ContainsKey(assembly))
				{
					//FIX: This should Output the created Bolt Assembly Object. We need the ID.
					await conClient.Material.AddBoltAssemblyAsync(conClient.ActiveProjectId, new ConMprlElement() { MprlName = assembly});
					
					//Console.WriteLine("Successfully Added new Bolt Assembly: " + added.MprlName);

					//Need to check what happens if name is not found...
				}
				else
					Console.WriteLine("Assembly already in project:" + assembly);
			}

			string exampleFolder = GetExampleFolderPathOnDesktop("GetAndAddMaterials");
			string fileName = "simple cleat connection-added materials.ideaCon";
			string saveFilePath = Path.Combine(exampleFolder, fileName);

			//Save the applied template
			await conClient.Project.SaveProjectAsync(conClient.ActiveProjectId, saveFilePath);
			Console.WriteLine("Project saved to: " + saveFilePath);

			//Close the opened project.
			await conClient.Project.CloseProjectAsync(conClient.ActiveProjectId);
		}
	}
}